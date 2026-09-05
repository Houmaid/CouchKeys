using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CouchKeys
{
    public static class InputSimulator
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_ALT = 0x12;
        private const byte VK_TAB = 0x09;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public static void SendAltTab()
        {
            try
            {
                // Alt drücken
                keybd_event(VK_ALT, 0, 0, UIntPtr.Zero);
                // Tab drücken
                keybd_event(VK_TAB, 0, 0, UIntPtr.Zero);
                // Tab loslassen
                keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                // Alt loslassen
                keybd_event(VK_ALT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SendAltTab error: {ex.Message}");
            }
        }
    }
}