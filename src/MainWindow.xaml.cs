using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FluidSim
{
    public partial class MainWindow : Window
    {
        #region [Local Members]
        // Simulation parameters
        int N = 100;                // grid size/resolution (always square)
        float dt = 0.08f;           // timestep
        float viscosity = 0.04f;
        float diffusion = 0.0001f;
        float buoyancy = 0.1f; // Fall from top => buoyancy=2.5f;
        float injectStrength = 300f;
        float temperatureDecay = 0.09f;
        double gammaCorrection = 0.7d;
        bool addSmokeSource = false;

        // Grid arrays: sized (N+2) * (N+2) to allow boundaries.
        int size; // (N+2)
        float[] u, v, uPrev, vPrev;
        float[] dens, densPrev;
        float[] temp, tempPrev;

        // Pixel rendering
        WriteableBitmap bmp;
        int bmpWidth, bmpHeight;
        int stride;
        byte[] pixels;
        byte blueTint = 30;

        // Interaction
        CancellationTokenSource simCts;
        bool paused = false;
        bool mouseDown = false;
        Point lastMousePos;

        // Extras
        readonly Random _rand = new Random();
        DispatcherTimer tmrDispatch = null;
        double currentX = 1;
        bool leftToRight = true;
        bool topToBottom = false;
        bool initComplete = false;
        long frame = 0;
        ValueStopwatch vsw;
        #endregion

        /// <summary>
        /// Main constructor
        /// </summary>
        public MainWindow()
        {
            vsw = ValueStopwatch.StartNew();
            InitializeComponent();
            //CompositionTarget.Rendering += UpdateFrame;
            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    paused = true;
                    simCts?.Cancel();
                    App.Current.Shutdown();
                }
            };
        }

        #region [Events]
        void Window_Loaded(object sender, RoutedEventArgs e)
        {
            #region [slider events]
            SliderGrid.ValueChanged += (s, ev) => 
            { 
                N = Math.Max(32, (int)SliderGrid.Value); 
                NeedReset(); 
            };
            SliderDt.ValueChanged += (s, ev) =>
            {
                dt = (float)SliderDt.Value;
            };
            SliderVisc.ValueChanged += (s, ev) =>
            {
                viscosity = (float)SliderVisc.Value;
            };
            SliderDiff.ValueChanged += (s, ev) =>
            {
                diffusion = (float)SliderDiff.Value;
            };
            SliderBuoy.ValueChanged += (s, ev) =>
            {
                buoyancy = (float)SliderBuoy.Value;
            };
            SliderInject.ValueChanged += (s, ev) =>
            {
                injectStrength = (float)SliderInject.Value;
            };
            SliderDecay.ValueChanged += (s, ev) =>
            {
                temperatureDecay = (float)SliderDecay.Value;
            };
            SliderColor.ValueChanged += (s, ev) =>
            {
                blueTint = (byte)SliderColor.Value;
            };
            SimImage.MouseLeftButtonDown += SimImage_MouseLeftButtonDown;
            SimImage.MouseLeftButtonUp += SimImage_MouseLeftButtonUp;
            SimImage.MouseMove += SimImage_MouseMove;
            SimImage.MouseRightButtonUp += SimImage_MouseRightButtonUp;
            #endregion

            SetInitialParameters();
            InitSimulation();
            StartSimulationLoop();

            //Create the timer for our instigator
            if (tmrDispatch == null)
            {
                tmrDispatch = new DispatcherTimer();
                tmrDispatch.Interval = TimeSpan.FromMilliseconds(30);
                tmrDispatch.Tick += timer_Tick;
                tmrDispatch.Start();
            }
            else
            {
                if (!tmrDispatch.IsEnabled)
                    tmrDispatch.Start();
            }

            if (App.FullScreenMode)
            {
                // Remove the control panel and maximize
                mainGrid.ColumnDefinitions[1].Width = new GridLength(0);
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Maximized;
            }
            else
            {
                // Standard setup for user control
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.WindowState = WindowState.Normal;
            }
        }

        void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            simCts?.Cancel();
        }

        void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            paused = !paused;
            BtnPause.Content = paused ? "Resume" : "Pause";
            StatusText.Text = paused ? "Status: Paused" : "Status: Running";
        }

        void BtnReset_Click(object sender, RoutedEventArgs e) => ResetSimulation();

        void BtnToggle_Click(object sender, RoutedEventArgs e)
        {
            topToBottom = !topToBottom;
            BtnToggle.Content = topToBottom ? "Top to bottom" : "Bottom to top";
            SetInitialParameters(); // auto-pick best starting conditions on flip
        }

        void SimImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            mouseDown = true;
            lastMousePos = e.GetPosition(SimImage);
            SimImage.CaptureMouse();
        }

        void SimImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            mouseDown = false;
            SimImage.ReleaseMouseCapture();
        }

        void SimImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouseDown)
            {
                var p = e.GetPosition(SimImage);
                // flip Y to match image
                double flip = SimImage.Height - p.Y;
                if (flip >= 0) { p.Y = flip; }
                InjectFromMouse(p);
                lastMousePos = p;
            }
        }

        /// <summary>
        /// Switches the control panel on/off
        /// </summary>
        void SimImage_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var current = mainGrid.ColumnDefinitions[1].Width;
            if (current.Value == 0)
                mainGrid.ColumnDefinitions[1].Width = new GridLength(310);
            else
                mainGrid.ColumnDefinitions[1].Width = new GridLength(0);
        }

        /// <summary>
        /// Testing CompositionTarget rendering
        /// </summary>
        void UpdateFrame(object sender, EventArgs e)
        {
            // Only update every 16 frames
            if (initComplete && !paused && (++frame > 16))
            {
                frame = 0;
                if (addSmokeSource)
                    AddSmokeSource();

                // Revisit this since the frame rate can be too high
                // so that Step() has not had a chance to complete.
                Step();

                RenderToBitmap();
            }
        }
        #endregion

        #region [Simulation]
        /// <summary>
        /// Timer event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void timer_Tick(object sender, EventArgs e)
        {
            if (!paused)
            {
                if (leftToRight && currentX < (SimImage.ActualWidth - 40))
                    currentX += _rand.Next(0, 15);
                else
                    leftToRight = false;

                if (!leftToRight && currentX > 40)
                    currentX -= _rand.Next(0, 15);
                else
                    leftToRight = true;

                double currentY = 0;
                if (topToBottom)
                    currentY = SimImage.ActualHeight - 2;
                else
                    currentY = 4;

                InjectFromMouse(new Point
                {
                    X = currentX,
                    Y = currentY,
                });
            }
        }

        /// <summary>
        /// Must be done under the UI thread.
        /// </summary>
        void NeedReset()
        {
            Dispatcher.BeginInvoke(new Action(() => { ResetSimulation(); }));
        }

        void InitSimulation()
        {
            // include boundary
            size = N + 2;

            // allocate arrays
            u = new float[size * size];
            v = new float[size * size];
            uPrev = new float[size * size];
            vPrev = new float[size * size];

            dens = new float[size * size];
            densPrev = new float[size * size];

            temp = new float[size * size];
            tempPrev = new float[size * size];

            // create bitmap sized to N x N but scaled by image stretch
            bmpWidth = N;
            bmpHeight = N;
            stride = bmpWidth * 4;
            pixels = new byte[bmpHeight * stride];
            bmp = new WriteableBitmap(bmpWidth, bmpHeight, 96, 96, PixelFormats.Bgra32, null);
            SimImage.Source = bmp;
            SimImage.Width = ActualWidth - ControlPanel.ActualWidth; // account for control panel on the right
            SimImage.Height = ActualHeight;
            
            initComplete = true;
        }

        void SetInitialParameters()
        {
            if (topToBottom)
            {
                dt = 0.08f;
                viscosity = 0.024f;
                buoyancy = 2.6f;
                diffusion = 0.0001f;
                injectStrength = 300f;
                temperatureDecay = 0.05f;
            }
            else
            {
                dt = 0.02f;
                viscosity = 0.018f;
                buoyancy = 0.1f;
                diffusion = 0.0001f;
                injectStrength = 300f;
                temperatureDecay = 0.31f;
                if (App.FullScreenMode)
                {
                    N = 50; // go easy on the CPU
                    dt = 0.02f;
                    viscosity = 0.012f;
                    temperatureDecay = 0.6f;
                    this.Topmost = true;
                }
            }

            // Insurance in the event that we're called under a non-UI thread.
            mainGrid.Dispatcher.Invoke(delegate ()
            {
                SliderGrid.Value = (double)N;
                SliderDt.Value = (double)dt;
                SliderVisc.Value = (double)viscosity;
                SliderDiff.Value = (double)diffusion;
                SliderBuoy.Value = (double)buoyancy;
                SliderInject.Value = (double)injectStrength;
                SliderDecay.Value = (double)temperatureDecay;
                SliderColor.Value = (double)blueTint;
            });
        }

        void ResetSimulation()
        {
            simCts?.Cancel();
            InitSimulation();
            StartSimulationLoop();
        }

        void InjectFromMouse(Point p)
        {
            // map image space to grid coords
            Image img = SimImage;
            double iw = bmpWidth;
            double ih = bmpHeight;

            // SimImage may be scaled, map relative coords
            var controlSize = new Size(img.ActualWidth, img.ActualHeight);
            if (controlSize.Width <= 0 || controlSize.Height <= 0) 
                return;

            double sx = p.X / controlSize.Width;
            double sy = p.Y / controlSize.Height;
            int i = (int)(sx * (N)) + 1;
            int j = (int)(sy * (N)) + 1;
            i = Math.Max(1, Math.Min(N, i));
            j = Math.Max(1, Math.Min(N, j));
            int idx = IX(i, j);
            dens[idx] += injectStrength * 0.8f;
            temp[idx] += injectStrength * 0.03f + 25f;

            // add a small upward velocity
            v[idx] -= injectStrength * 0.01f;
        }

        void StartSimulationLoop()
        {
            simCts = new CancellationTokenSource();
            var token = simCts.Token;

            Task.Run(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                while (!token.IsCancellationRequested)
                {
                    if (initComplete && !paused)
                    {
                        if (addSmokeSource)
                        {
                            // Continuous injection near bottom/top center to simulate fire source
                            AddSmokeSource();
                        }
                        
                        // Step the simulation
                        Step();
                        
                        // Render the current state to bitmap
                        RenderToBitmap();
                        
                        frame++;
                        if (stopwatch.ElapsedMilliseconds >= 1000)
                        {
                            // Don't use BeginInvoke as the frame count could be
                            // reset by the time the Dispatcher renders the text.
                            StatusText.Dispatcher.Invoke(delegate()
                            {
                                StatusText.Text = $"Running at {frame} frames per second";
                            });
                            //Debug.WriteLine($"[INFO] FPS ⇒ {frame}");
                            stopwatch.Restart();
                            frame = 0;
                        }
                    }

                    // We'll target ~30 FPS, but don't block if heavy.
                    await Task.Delay(15, token).ContinueWith(t => { });
                }
            }, token);
        }

        /// <summary>
        /// Adds a steady source along top/bottom center
        /// </summary>
        void AddSmokeSource()
        {
            int cx = N / 2;
            int radius = Math.Max(1, N / 20);
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = 0; dy <= radius; dy++)
                {
                    int i = cx + dx;
                    int j = N - dy;
                    
                    if (i < 1 || i > N || j < 1 || j > N) 
                        continue;
                    
                    int idx = IX(i, j);
                    float r = 1.0f - (Math.Abs(dx) / (float)(radius + 1));
                    dens[idx] += injectStrength * 0.01f * r;
                    temp[idx] += injectStrength * 0.0006f * (1 + r * 3);
                }
            }
        }

        /// <summary>
        /// High-level simulation step.
        /// Includes velocities, projection, diffusion, advection & density/temperature.
        /// </summary>
        /// <remarks>
        /// This is where most of the time is taken in the simulation. If N=100 ⇒ approx 20 milliseconds.
        /// </remarks>
        void Step()
        {
            // Copy dt, visc, diff from fields so local variables used (micro-optimizations)
            float localDt = dt;
            float localVisc = viscosity;
            float localDiff = diffusion;
            float localBuoy = buoyancy;

            if (App.DebugMode)
                vsw = ValueStopwatch.StartNew();

            // Buoyancy: add vertical force proportional to temperature and density
            for (int j = 1; j <= N; j++)
            {
                for (int i = 1; i <= N; i++)
                {
                    int idx = IX(i, j);
                    // upward force = buoyancy * temp - small density term (hotter rises)
                    float f = localBuoy * temp[idx] - 0.1f * dens[idx];
                    v[idx] -= f * localDt;
                }
            }

            // Velocity steps
            Diffuse(1, uPrev, u, localVisc, localDt);
            Diffuse(2, vPrev, v, localVisc, localDt);

            Project(uPrev, vPrev, u, v);

            // Calculate the transfer of heat or matter by the flow (horizontally)
            Advect(1, u, uPrev, uPrev, vPrev, localDt);
            Advect(2, v, vPrev, uPrev, vPrev, localDt);

            Project(u, v, uPrev, vPrev);

            // Temperature diffusion & advection
            Diffuse(0, tempPrev, temp, localDiff, localDt);
            Advect(0, temp, tempPrev, u, v, localDt);

            // decay temperature
            for (int k = 0; k < temp.Length; k++)
                temp[k] = Math.Max(0f, temp[k] - temperatureDecay * localDt * temp[k]);

            // Density diffusion & advection
            Diffuse(0, densPrev, dens, localDiff, localDt);
            Advect(0, dens, densPrev, u, v, localDt);

            // density decay (combustion)
            for (int k = 0; k < dens.Length; k++) 
                dens[k] = Math.Max(0f, dens[k] - temperatureDecay * 0.5f * localDt * dens[k]);

            if (App.DebugMode)
                Debug.WriteLine($"[INFO] Step calc took {vsw.GetElapsedTime().TotalMilliseconds} ms");
        }

        /// <summary>
        /// Linear solver for diffusion
        /// </summary>
        void Diffuse(int b, float[] x, float[] x0, float diffCoef, float dtLocal)
        {
            float a = dtLocal * diffCoef * N * N;
            LinSolve(b, x, x0, a, 1 + 4 * a);
        }

        void LinSolve(int b, float[] x, float[] x0, float a, float c)
        {
            for (int k = 0; k < 20; k++)
            {
                for (int j = 1; j <= N; j++)
                {
                    for (int i = 1; i <= N; i++)
                    {
                        try
                        {
                            x[IX(i, j)] = (x0[IX(i, j)] + a * (
                            x[IX(i - 1, j)] + x[IX(i + 1, j)] +
                            x[IX(i, j - 1)] + x[IX(i, j + 1)])) / c;
                        }
                        catch (IndexOutOfRangeException)
                        {
                            // Handle out-of-bounds access gracefully
                            x[IX(i, j)] = 0;
                        }
                    }
                }
                SetBounds(b, x);
            }
        }

        void SetBounds(int b, float[] x)
        {
            for (int i = 1; i <= N; i++)
            {
                x[IX(i, 0)] = b == 2 ? -x[IX(i, 1)] : x[IX(i, 1)];
                x[IX(i, N + 1)] = b == 2 ? -x[IX(i, N)] : x[IX(i, N)];
            }
            for (int j = 1; j <= N; j++)
            {
                x[IX(0, j)] = b == 1 ? -x[IX(1, j)] : x[IX(1, j)];
                x[IX(N + 1, j)] = b == 1 ? -x[IX(N, j)] : x[IX(N, j)];
            }

            x[IX(0, 0)] = 0.5f * (x[IX(1, 0)] + x[IX(0, 1)]);
            x[IX(0, N + 1)] = 0.5f * (x[IX(1, N + 1)] + x[IX(0, N)]);
            x[IX(N + 1, 0)] = 0.5f * (x[IX(N, 0)] + x[IX(N + 1, 1)]);
            x[IX(N + 1, N + 1)] = 0.5f * (x[IX(N, N + 1)] + x[IX(N + 1, N)]);
        }

        void Project(float[] uArr, float[] vArr, float[] p, float[] div)
        {
            float h = 1.0f / N;
            for (int j = 1; j <= N; j++)
            {
                for (int i = 1; i <= N; i++)
                {
                    div[IX(i, j)] = -0.5f * h * (uArr[IX(i + 1, j)] - uArr[IX(i - 1, j)] + vArr[IX(i, j + 1)] - vArr[IX(i, j - 1)]);
                    p[IX(i, j)] = 0;
                }
            }
            SetBounds(0, div);
            SetBounds(0, p);

            for (int k = 0; k < 20; k++)
            {
                for (int j = 1; j <= N; j++)
                {
                    for (int i = 1; i <= N; i++)
                    {
                        try
                        {
                            p[IX(i, j)] = (div[IX(i, j)] + p[IX(i - 1, j)] + p[IX(i + 1, j)] + p[IX(i, j - 1)] + p[IX(i, j + 1)]) / 4;
                        }
                        catch (IndexOutOfRangeException)
                        {   // Handle out-of-bounds access gracefully
                            p[IX(i, j)] = 0;
                        }
                    }
                }
                SetBounds(0, p);
            }

            for (int j = 1; j <= N; j++)
            {
                for (int i = 1; i <= N; i++)
                {
                    uArr[IX(i, j)] -= 0.5f * (p[IX(i + 1, j)] - p[IX(i - 1, j)]) / h;
                    vArr[IX(i, j)] -= 0.5f * (p[IX(i, j + 1)] - p[IX(i, j - 1)]) / h;
                }
            }
            SetBounds(1, uArr);
            SetBounds(2, vArr);
        }

        /// <summary>
        /// Advection is a calculation of the transfer of heat or matter by the flow, typically on the horizontal plane.
        /// </summary>
        void Advect(int b, float[] d, float[] d0, float[] uArr, float[] vArr, float dtLocal)
        {
            float dt0 = dtLocal * N;
            for (int j = 1; j <= N; j++)
            {
                for (int i = 1; i <= N; i++)
                {
                    float x = i - dt0 * uArr[IX(i, j)];
                    float y = j - dt0 * vArr[IX(i, j)];
                    if (x < 0.5f) { x = 0.5f; }
                    if (x > N + 0.5f) { x = N + 0.5f; }
                    int i0 = (int)x;
                    int i1 = i0 + 1;
                    if (y < 0.5f) { y = 0.5f; }
                    if (y > N + 0.5f) { y = N + 0.5f; }
                    int j0 = (int)y;
                    int j1 = j0 + 1;
                    float s1 = x - i0;
                    float s0 = 1 - s1;
                    float t1 = y - j0;
                    float t0 = 1 - t1;
                    try
                    {
                        d[IX(i, j)] = s0 * (t0 * d0[IX(i0, j0)] + t1 * d0[IX(i0, j1)]) +
                                      s1 * (t0 * d0[IX(i1, j0)] + t1 * d0[IX(i1, j1)]);
                    }
                    catch (IndexOutOfRangeException)
                    {   // Handle out-of-bounds access gracefully
                        d[IX(i, j)] = 0;
                    }
                }
            }
            SetBounds(b, d);
        }

        /// <summary>
        /// Rendering: map density & temperature to fire colors
        /// </summary>
        void RenderToBitmap()
        {
            // We'll map each cell to a pixel in the bitmap (1:1)
            // Color mapping: temperature drives color (black -> red -> orange -> yellow -> white)
            // Density multiplies brightness.
            #region [if N=100 ⇒ approx 2.5 milliseconds]
            int w = bmpWidth, h = bmpHeight;
            for (int j = 0; j < N; j++) // ~2.5 milliseconds
            {
                for (int i = 0; i < N; i++)
                {
                    int srcI = i + 1;
                    int srcJ = N - j; // flip vertically: grid bottom maps to bottom pixels
                    int idx = IX(srcI, srcJ);

                    float tVal = temp[idx];
                    float dVal = dens[idx];

                    // combine temperature and density to get intensity
                    float intensity = Math.Min(1f, (tVal * 0.04f) + (dVal * 0.01f));
                    
                    // apply small gamma correction
                    intensity = (float)Math.Pow(intensity, gammaCorrection);

                    // Map temperature to color
                    Color col = TemperatureToColor(tVal, intensity);
                    int px = i + j * w;
                    int pi = px * 4;
                    pixels[pi + 0] = col.B;
                    pixels[pi + 1] = col.G;
                    pixels[pi + 2] = col.R;
                    pixels[pi + 3] = col.A;
                }
            }
            #endregion

            // Copy pixel buffer to WriteableBitmap (~100 microseconds)
            Dispatcher.Invoke(() =>
            {
                try
                {
                    #region [if N=100 ⇒ approx 100 microseconds]
                    bmp.Lock();
                    
                    // Marshal.Copy() is specifically designed to copy between unmanaged and managed memory
                    // without having to drop into an unsafe block. It wraps the pointer access and performs
                    // the appropriate pinning, bounds checks, and memory operations internally.
                    System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bmp.BackBuffer, pixels.Length);
                    
                    bmp.AddDirtyRect(new Int32Rect(0, 0, bmpWidth, bmpHeight));
                    bmp.Unlock();
                    #endregion
                }
                catch { }
            });
        }

        /// <summary>
        /// Index helper (i, j) ⇒ index in arrays
        /// </summary>
        int IX(int i, int j) => i + j * size;

        Color TemperatureToColor(float tempVal, float intensity)
        {
            // tempVal scalar ⇒ map to palette clamp & normalize
            float t = Math.Max(0f, tempVal * 0.05f); // tuning
            t = Math.Min(1f, t);

            // Palette interpolation: black ⇒ red ⇒ orange ⇒ yellow ⇒ white
            // 0.00 ⇒ black (after gamma correction)
            // 0.25 ⇒ dark red
            // 0.50 ⇒ red/orange
            // 0.75 ⇒ yellow
            // 1.00 ⇒ white

            byte a = (byte)Math.Min(255, (int)(255 * intensity));
            if (t <= 0.25f)
            {
                float s = t / 0.25f;
                byte r = (byte)(s * 180 + 20 * (1 - s));
                return Color.FromArgb(a, r, 0, blueTint);
            }
            else if (t <= 0.5f)
            {
                float s = (t - 0.25f) / 0.25f;
                byte r = (byte)(255);
                byte g = (byte)(s * 120);
                return Color.FromArgb(a, r, (byte)(g * intensity), blueTint);
            }
            else if (t <= 0.75f)
            {
                float s = (t - 0.5f) / 0.25f;
                byte r = 255;
                byte g = (byte)(120 + s * 135);
                return Color.FromArgb(a, r, (byte)(g * intensity), blueTint);
            }
            else
            {
                float s = (t - 0.75f) / 0.25f;
                byte r = 255;
                byte g = 255;
                byte b = 0;
                if (blueTint < 220)
                    b = (byte)(s * 255);
                else // get rid of any remaining yellow values
                    b = blueTint;
                return Color.FromArgb(a, (byte)(r * intensity), (byte)(g * intensity), (byte)(b * intensity));
            }
        }
        #endregion
    }

    #region [Original Version]
    /*
     *  This was my original version of the fire/fluid simulation code - it renders a Bunsen-burner-style 
     *  inner flame cone and outer flame cone when clicking at the bottom of the main window's edge.
     */

    //public partial class MainWindow : Window
    //{
    //readonly int _width = 400;
    //readonly int _height = 200;
    //readonly float[,] _heat;
    //readonly WriteableBitmap _bitmap;
    //readonly Random _rand = new Random();

    //bool _loaded;
    //bool _isMouseDown;
    //Point _mousePos;

    //// Auto-fire state
    //double _autoX, _autoY;
    //double _targetX, _targetY;
    //long _targetChangeTimeTicks;
    //readonly double _autoSpeed = 0.02; // interpolation factor per frame

    //public MainWindow()
    //{
    //InitializeComponent();
    //WindowState = WindowState.Maximized;

    //_heat = new float[_width, _height];
    //_bitmap = new WriteableBitmap(_width, _height, 96, 96, PixelFormats.Bgra32, null);
    //SimImage.Source = _bitmap;

    //// Start auto-fire in the center
    //_autoX = _width / 2.0;
    //_autoY = _height / 2.0;
    ////_autoY = _height * 0.75; // start lower in the grid
    //PickNewTarget();

    //CompositionTarget.Rendering += UpdateFrame; // better framerate than DispatcherTimer (approx 60 FPS)
    //}

    //void PickNewTarget()
    //{
    //_targetX = _rand.Next(10, _width - 10);
    //_targetY = _rand.Next(50, _height / 2); // bias toward top half for fire
    ////_targetY = _rand.Next(_height / 3, _height - 10);
    //_targetChangeTimeTicks = DateTime.Now.AddSeconds(_rand.Next(1, 4)).Ticks;
    //}

    //void UpdateFrame(object sender, EventArgs e)
    //{
    //if (!_loaded) 
    //return;

    //// Smoothly move auto-fire toward its target
    //_autoX += (_targetX - _autoX) * _autoSpeed;
    //_autoY += (_targetY - _autoY) * _autoSpeed;

    //// Time to pick a new wander target?
    //if (DateTime.Now.Ticks > _targetChangeTimeTicks)
    //PickNewTarget();

    //// Inject heat from auto-fire
    //AddHeat((int)_autoX, (int)_autoY, 40);

    //// If mouse is down, inject heat there too
    //if (_isMouseDown)
    //AddHeat((int)_mousePos.X, (int)_mousePos.Y, 40);

    //UpdateSimulation();
    //DrawBitmap();
    //}

    //void AddHeat(int x, int y, float amount)
    //{
    //const int radius = 5;
    //for (int dy = -radius; dy <= radius; dy++)
    //{
    //for (int dx = -radius; dx <= radius; dx++)
    //{
    //int px = x + dx;
    //int py = y + dy;
    //if (px >= 0 && px < _width && py >= 0 && py < _height)
    //_heat[px, py] = Math.Min(255, _heat[px, py] + amount);
    //}
    //}
    //}

    //void UpdateSimulation()
    //{
    //// Basic diffusion and decay
    //for (int y = _height - 2; y >= 1; y--)
    //{
    //for (int x = 1; x < _width - 1; x++)
    //{
    //if ((y + 2 >= _height) || (x + 1 >= _width))
    //continue;
    //float decay = _rand.Next(1, 3);
    //_heat[x, y] = ((_heat[x - 1, y + 1] +
    //_heat[x, y + 1] +
    //_heat[x + 1, y + 1] +
    //_heat[x, y + 2]) / 4) - decay;
    //if (_heat[x, y] < 0) _heat[x, y] = 0;
    //}
    //}
    //}

    //void DrawBitmap()
    //{
    //_bitmap.Lock();
    //unsafe
    //{
    //int* buffer = (int*)_bitmap.BackBuffer;
    //for (int y = 0; y < _height; y++)
    //{
    //for (int x = 0; x < _width; x++)
    //{
    //byte heat = (byte)_heat[x, y];
    //int color = ColorFromHeat(heat);
    //buffer[y * _width + x] = color;
    //}
    //}
    //}
    //_bitmap.AddDirtyRect(new Int32Rect(0, 0, _width, _height));
    //_bitmap.Unlock();
    //}

    //int ColorFromHeatGreen(byte heat)
    //{
    //// Simple fire gradient mapping
    //byte r = heat;
    //byte g = (byte)Math.Min(255, heat * 2);
    //byte b = (byte)(heat / 4);
    //return (255 << 24) | (r << 16) | (g << 8) | b;
    //}

    //int ColorFromHeat(byte heat)
    //{
    //byte b = heat;                                // full blue at high heat
    //byte g = (byte)Math.Min(255, heat * 2 / 3);   // green rises slower
    //byte r = (byte)(heat / 8);                    // very little red
    //return (255 << 24) | (r << 16) | (g << 8) | b;
    //}

    //int ColorFromHeat_Lame(byte heat)
    //{
    //// Base cold color
    //byte r = 0;
    //byte g = 0;
    //byte b = 0;

    //// As heat rises, add more green/red to shift toward white
    //double t = heat / 255.0;
    //r = (byte)(t * 255);
    //g = (byte)(t * 255);
    //b = 255; // keep blue maxed

    //return (255 << 24) | (r << 16) | (g << 8) | b;
    //}

    // void SimImage_MouseDown(object sender, MouseButtonEventArgs e)
    //{
    //_isMouseDown = true;
    ////_mousePos = e.GetPosition(SimImage);
    //_mousePos = TransformMouseToSim(e.GetPosition(SimImage));
    //}

    // void SimImage_MouseMove(object sender, MouseEventArgs e)
    //{
    //if (_isMouseDown)
    //{
    ////_mousePos = e.GetPosition(SimImage);
    //_mousePos = TransformMouseToSim(e.GetPosition(SimImage));
    //}
    //}

    //Point TransformMouseToSim(Point p)
    //{
    //double scaleX = _width / SimImage.ActualWidth;
    //double scaleY = _height / SimImage.ActualHeight;
    //return new Point(p.X * scaleX, p.Y * scaleY);
    //}

    //void SimImage_MouseUp(object sender, MouseButtonEventArgs e)
    //{
    //_isMouseDown = false;
    //}

    //oid Window_Loaded(object sender, RoutedEventArgs e)
    //{
    //_loaded = true;
    //}

    // void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    //{
    //_loaded = false;
    //}
    //}
    #endregion
}

