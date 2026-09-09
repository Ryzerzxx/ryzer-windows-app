using System;
using System.Windows;
using Microsoft.Win32;
using RyzerDNS.Services;

namespace RyzerDNS
{
    public partial class App : Application
    {
        private static bool _hasCleanedUpDns = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Hook system shutdown, restart, and logoff events
            SystemEvents.SessionEnding += OnSystemSessionEnding;
            SystemEvents.SessionEnded += OnSystemSessionEnded;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    MessageBox.Show($"خطای سیستمی رخ داد:\n{ex.Message}", "خطای RyzerDNS", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"خطای رخ داده در برنامه:\n{args.Exception.Message}", "خطای RyzerDNS", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
        }

        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            PerformShutdownDnsCleanup();
            base.OnSessionEnding(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            PerformShutdownDnsCleanup();
            base.OnExit(e);
        }

        private static void OnSystemSessionEnding(object sender, SessionEndingEventArgs e)
        {
            PerformShutdownDnsCleanup();
        }

        private static void OnSystemSessionEnded(object sender, SessionEndedEventArgs e)
        {
            PerformShutdownDnsCleanup();
        }

        private static void OnProcessExit(object? sender, EventArgs e)
        {
            PerformShutdownDnsCleanup();
        }

        public static void PerformShutdownDnsCleanup()
        {
            if (_hasCleanedUpDns) return;
            _hasCleanedUpDns = true;

            try
            {
                DnsManagerService.ResetDnsToDhcpFastSync();
            }
            catch { }
        }
    }
}
