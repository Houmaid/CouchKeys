using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Windows.Globalization;

namespace AppTemplate.PayPal
{
    public partial class PayPalWindow : Window
    {
        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private const string PayPalUrl = "https://www.paypal.me/DeinName";

        private string _language;
        public PayPalWindow(bool isDark, string language)
        {
            InitializeComponent();
            ApplyThemeToTitleBar(isDark); // Falls hier 'isDark' statt 'isDarkTheme' übergeben wird
            _language = language;

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

        private void BtnOpenPayPal_Click(object sender, RoutedEventArgs e)
        {
            bool isGerman = _language != null && _language.Equals("de", StringComparison.OrdinalIgnoreCase);

            string url = isGerman
                ? "https://www.paypal.de/OmarHoumaid"
                : "https://www.paypal.com/OmarHoumaid";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}