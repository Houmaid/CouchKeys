using System.Diagnostics;
using System.Windows;

namespace CouchKeys
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Prüft, ob CouchKeys bereits im Task-Manager läuft
            if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
            {
                // Beendet die 2. Instanz sofort beim Start
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }
    }
}