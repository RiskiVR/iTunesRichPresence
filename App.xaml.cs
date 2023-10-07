using System;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace iTunesRichPresence_Rewrite {

    public static class Globals {
        public static TextBox LogBox;

        public static void Log(string message) {
            LogBox.Text += $"[{DateTime.Now.ToString(CultureInfo.CurrentCulture)}] {message}\n";
        }
    }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App {
        protected override void OnStartup(StartupEventArgs e) {

            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;

            base.OnStartup(e);
        }

        private void CurrentOnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
            Globals.Log($"An unhandled exception has occurred: {e.Exception.Message}");
        }

        private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e) {
            Globals.Log($"An unhandled exception has occurred: {((Exception)e.ExceptionObject).Message}");
        }
    }
}
