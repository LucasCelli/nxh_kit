using System;
using System.Windows;
using System.Windows.Threading;

namespace NHX_Kit
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            // Removido: AppDomain.CurrentDomain.UnhandledException (gera popups em shutdown)
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.ToString(), "Erro (UI thread)", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
