using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace DaxStudio.Controls.Tests.Helpers
{
    /// <summary>
    /// Singleton helper to manage a shared WPF Application instance for tests
    /// </summary>
    public static class WpfTestHelper
    {
        private static readonly object _lockObj = new object();
        private static bool _isInitialized = false;

        /// <summary>
        /// Initializes the WPF environment for testing if not already initialized
        /// </summary>
        public static void InitializeWpfEnvironment()
        {
            // Use a lock to prevent race conditions between tests
            lock (_lockObj)
            {
                if (_isInitialized)
                    return;

                // Create application if not exists
                if (Application.Current == null)
                {
                    // Create STA thread for WPF
                    var thread = new Thread(() =>
                    {
                        // Create application and Dispatcher
                        var app = new Application();
                        
                        // Load the control resources
                        app.Resources.MergedDictionaries.Add(new ResourceDictionary
                        {
                            Source = new Uri("/DaxStudio.Controls;component/Themes/Generic.xaml", UriKind.Relative)
                        });
                        
                        app.Dispatcher.Invoke(() => { _isInitialized = true; });
                        
                        // Start dispatcher for this thread
                        Dispatcher.Run();
                    });

                    // Set thread as STA (required for WPF)
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.IsBackground = true;
                    thread.Start();

                    // Wait for initialization to complete
                    while (!_isInitialized)
                    {
                        Thread.Sleep(10);
                    }
                }
                else
                {
                    _isInitialized = true;
                }
            }
        }

        /// <summary>
        /// Processes all pending WPF messages to ensure UI operations complete
        /// </summary>
        public static void DoEvents()
        {
            // Ensure we have an application and dispatcher
            InitializeWpfEnvironment();

            // Process all pending messages in the message queue
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new DispatcherOperationCallback(ExitFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        private static object ExitFrame(object frame)
        {
            ((DispatcherFrame)frame).Continue = false;
            return null;
        }

        /// <summary>
        /// Resets the WPF state, clearing any application-level state that might affect tests
        /// </summary>
        public static void ResetWpfState()
        {
            if (Application.Current != null)
            {
                // Clear any application-level state that might affect tests
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Clear resources that might be causing issues
                    foreach (var key in new List<object>(Application.Current.Resources.Keys.Cast<object>()))
                    {
                        if (key.ToString().Contains("Expander") || key.ToString().Contains("Tree"))
                        {
                            // Optionally reset or remove problematic resources
                            // Application.Current.Resources[key] = null;
                        }
                    }
                });
            }
        }
    }
}