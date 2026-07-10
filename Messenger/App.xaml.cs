using System;
using System.Threading;
using System.Windows;

namespace Messenger
{
    /// <summary>
    /// Class for the WPF application. This class is responsible for managing the application's lifecycle, including startup and exit events. It ensures that only a single instance of the application can run at a time by using a mutex.
    /// If another instance is already running, it brings the existing instance to the front and shuts down the new instance.
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private static Mutex? _mutex;



        /// <summary>
        /// Overrides the OnStartup method to implement single instance behavior.
        /// If another instance of the application is already running, it brings that instance to the front and shuts down the new instance.
        /// Otherwise, it allows the application to start normally.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception ex = (Exception)args.ExceptionObject;
                System.IO.File.WriteAllText("crash_log.txt", ex.ToString());
            };

            // Single instance enforcement using a named mutex.
            // The mutex is created with a unique name, and if it already exists, it indicates that another instance of the application is running.
            _mutex = new Mutex(true, "Messenger.SingleInstance", out bool createdNew);

            if (!createdNew)
            {
                SingleInstanceHelper.BringExistingToFront();
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        /// <summary>
        /// Overrides the OnExit method to clean up resources when the application exits. It disposes of the mutex used for single instance enforcement.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
