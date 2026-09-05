using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AppTemplate.Help
{
    public partial class HelpWindow : Window
    {
        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public HelpWindow(bool isDarkTheme)
        {
            InitializeComponent();

            // 1. Aktives Theme/Ressourcen aus der Hauptanwendung übernehmen
            this.Resources.MergedDictionaries.Clear();
            foreach (var dict in Application.Current.Resources.MergedDictionaries)
            {
                this.Resources.MergedDictionaries.Add(dict);
            }

            // 2. Titelleisten-Theme für Windows setzen
            ApplyThemeToTitleBar(isDarkTheme);

            // 3. FlowDirection (z.B. für Arabisch RTL) synchronisieren
            if (Application.Current.MainWindow != null)
            {
                this.FlowDirection = Application.Current.MainWindow.FlowDirection;
            }
        }

        private void ApplyThemeToTitleBar(bool isDark)
        {
            this.SourceInitialized += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    int darkMode = isDark ? 1 : 0;
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
                }
            };
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}