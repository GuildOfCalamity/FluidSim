using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FluidSim
{
    public partial class App : Application
    {
        static Mutex _mutex;
        public static bool FullScreenMode { get; private set; } = false;
        public static bool DebugMode { get; private set; } = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;

            if (e.Args != null && e.Args.Length > 0)
            {
                FullScreenMode = e.Args.Any(arg => arg.EndsWith("fullscreen", StringComparison.OrdinalIgnoreCase));
                DebugMode = e.Args.Any(arg => arg.EndsWith("debug", StringComparison.OrdinalIgnoreCase));
            }
            if ($"{System.Reflection.Assembly.GetExecutingAssembly().Location}".EndsWith(".scr", StringComparison.OrdinalIgnoreCase) || 
                $"{System.Reflection.Assembly.GetEntryAssembly().Location}".EndsWith(".scr", StringComparison.OrdinalIgnoreCase))
                FullScreenMode = true;

            #region [Only run one instance]
            //if (System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > 1)
            //    App.Current.Shutdown();

            bool isNewInstance = false;
            _mutex = new Mutex(true, "FluidSimByTheGuild", out isNewInstance);
            if (!isNewInstance)
            {
                App.Current.Shutdown();
            }
            #endregion

            //App.Current.Dispatcher.Invoke((Action)delegate ()
            //{
            //    MainWindow w = new MainWindow();
            //    w.Show();
            //}, System.Windows.Threading.DispatcherPriority.Background);

            base.OnStartup(e);
        }

        void CurrentDomain_FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Exception.Message) &&
                !e.Exception.Message.StartsWith("A task was canceled", StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"[WARNING] First chance exception: {e.Exception.Message}");
            }
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Debug.WriteLine($"[WARNING] Unhandled exception: {((Exception)e.ExceptionObject).Message}");
                MessageBox.Show(((Exception)e.ExceptionObject).Message, "FluidSim UnhandledException");
                //System.Diagnostics.EventLog.WriteEntry(SystemTitle, $"Unhandled exception thrown:\r\n{((Exception)e.ExceptionObject).ToString()}");
            }
            catch (Exception) { }
        }

        void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                Debug.WriteLine($"[WARNING] Dispatcher unhandled exception: {e.Exception.Message}");
                e.Handled = true;
            }
            catch (Exception) { }
        }
    }
}
