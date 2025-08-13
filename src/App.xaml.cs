using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FluidSim
{
    public partial class App : Application
    {
        public static bool FullScreenMode { get; private set; } = false;
        static Mutex _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            if (e.Args != null && e.Args.Length > 0)
            {
                FullScreenMode = e.Args.Any(arg => string.Equals(arg, "-fullscreen", StringComparison.OrdinalIgnoreCase));
            }

            //if (System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > 1)
            //    App.Current.Shutdown();
            bool isNewInstance = false;
            _mutex = new Mutex(true, "FluidSimByTheGuild", out isNewInstance);
            if (!isNewInstance)
            {
                App.Current.Shutdown();
            }

            //App.Current.Dispatcher.Invoke((Action)delegate ()
            //{
            //    MainWindow w = new MainWindow();
            //    w.Show();
            //}, System.Windows.Threading.DispatcherPriority.Background);

            base.OnStartup(e);
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Unhandled exception thrown: {((Exception)e.ExceptionObject).Message}");
                MessageBox.Show(((Exception)e.ExceptionObject).Message, "FluidSim UnhandledException");
                //System.Diagnostics.EventLog.WriteEntry(SystemTitle, $"Unhandled exception thrown:\r\n{((Exception)e.ExceptionObject).ToString()}");
            }
            catch (Exception) { }
        }
    }
}
