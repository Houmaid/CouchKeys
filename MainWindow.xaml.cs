using CouchKeys;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using System;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Hardcodet.Wpf.TaskbarNotification;

namespace CouchKeys
{
    public partial class MainWindow : Window
    {
        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYUP = 0x0105;

        #region Win32 Konstanten & Strukturen
        private const int WM_INPUT = 0x00FF;
        private const int RIDEV_INPUTSINK = 0x00000100;
        private const int RID_INPUT = 0x10000003;
        private const int RID_DEVICENAME = 0x20000007;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private TaskbarIcon? _notifyIcon;
        private bool _isAutostartActive = false;
        private const string AutostartRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "CouchKeys";

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWKEYBOARD
        {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VKey;
            public uint Message;
            public uint ExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public UIntPtr dwExtraInfo;
        }

        private const uint PM_REMOVE = 0x0001;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevice, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, StringBuilder? pData, ref uint pcbSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        private const int KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_MENU = 0x12;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        #endregion

        #region Member
        private IntPtr _hwnd = IntPtr.Zero;

        // 1. Behebt CS0103: Deklaration der fehlenden Variable
        private volatile string _lastInputDevicePath = string.Empty;

        // 2. Behebt CS1503: Ändert den Schlüssel-Typ von 'int' auf das Tupel '(string, int)'
        private readonly ConcurrentDictionary<(string DevicePath, int ScanCode), MacroModel> _scanCodeMacroLookup = new();

        private readonly DeviceMonitor _deviceMonitor = new();
        //private Forms.NotifyIcon? _notifyIcon;
        public ObservableCollection<MacroModel> Macros { get; } = new();
        private MacroModel? _selectedMacro;
        private volatile bool _isScanning = false;

        // ✅ PERF: Thread-sicheres Caching der Gerätenamen
        private readonly ConcurrentDictionary<IntPtr, string> _deviceCache = new();


        // ✅ THREAD-SAFETY: Volatile Flag zur Vermeidung von SendKeys-Endlosschleifen
        private volatile bool _isExecutingMacro = false;

        private readonly string _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CouchKeys",
            "macros.json");

        private IntPtr _lowLevelHookId = IntPtr.Zero;
        private LowLevelKeyboardProc _proc = null!;

        // ✅ ROBUSTHEIT: Statische Referenz schützt Hook-Delegate vor der Garbage Collection
        private static LowLevelKeyboardProc? _procGcKeeper;
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LstMacros.ItemsSource = Macros;

            string savedLang = CouchKeys.Properties.Settings.Default.SelectedLanguage;
            if (!string.IsNullOrEmpty(savedLang))
            {
                ApplyLanguage(savedLang);
            }

            CheckInitialAutostartStatus(); // <-- Hier ergänzen

            this.Closed += (s, e) => Application.Current.Shutdown();
            LoadMacros();
            _isExecutingMacro = false;


        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CheckInitialAutostartStatus();
            UpdateAutostartIconVisual();
        }
        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            // Prüfen, ob das Fenster minimiert wurde
            if (WindowState == WindowState.Minimized)
            {
                this.Hide(); // Versteckt das Fenster aus der Taskleiste
                             // Hier ggf. dein Tray-Icon auf sichtbar / Notification setzen, falls nötig
            }
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwnd = new WindowInteropHelper(this).Handle;
            LoadSavedSettings();
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(hwnd);
            source.AddHook(HwndAdapter);

            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[2];
            rid[0].usUsagePage = 0x01; rid[0].usUsage = 0x06; rid[0].dwFlags = RIDEV_INPUTSINK; rid[0].hwndTarget = hwnd;
            rid[1].usUsagePage = 0x0C; rid[1].usUsage = 0x01; rid[1].dwFlags = RIDEV_INPUTSINK; rid[1].hwndTarget = hwnd;

            bool registered = RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(rid[0]));
            //if (!registered) Debug.WriteLine($"RawInput failed: {Marshal.GetLastWin32Error()}");

            _proc = HookCallback;
            _procGcKeeper = _proc; // Verhindert GC-Collection des Delegates
            _lowLevelHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
            //if (_lowLevelHookId == IntPtr.Zero) Debug.WriteLine($"Hook failed: {Marshal.GetLastWin32Error()}");

            string[] args = Environment.GetCommandLineArgs();
            if (Array.Exists(args, arg => arg.Equals("--autostart", StringComparison.OrdinalIgnoreCase)))
            {
                ShowInTaskbar = false;
                Hide();
            }
        }
        private void UpdateActionTypeUI()
        {
            if (RbRunApp == null || RbSendKeys == null || GridAppPath == null || GridSendKeys == null)
                return;

            GridAppPath.Visibility = RbRunApp.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            GridSendKeys.Visibility = RbSendKeys.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }
        private string FormatDevicePath(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath)) return "Kein Gerät";
            var parts = devicePath.Split('#');
            return parts.Length >= 3 ? $"{parts[1]} [{parts[2]}]" : devicePath;
        }
        private IntPtr HwndAdapter(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_INPUT)
            {
                ProcessRawInput(lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void ProcessSingleInputEvent(RawInputEvent evt)
        {
            TxtLastDevice.Text = $"{(_deviceMonitor.IsCouchDevice(evt.DevicePath) ? "Couch" : "Main")}: {FormatDevicePath(evt.DevicePath)}";

            // Aktualisiert TxtLastKey NUR beim Drücken (IsKeyDown) - bleibt stehen bis zur nächsten Taste
            if (evt.IsKeyDown)
            {
                TxtLastKey.Text = evt.Vk.ToString();
            }
            if (_isScanning && _selectedMacro != null && evt.IsKeyDown)
            {
                _isScanning = false;
                _selectedMacro.DevicePath = evt.DevicePath;
                _selectedMacro.TriggerKey = KeyInterop.KeyFromVirtualKey((byte)evt.Vk);
                _selectedMacro.ScanCode = evt.Scan;

                _deviceMonitor.RegisterCouchDevice(evt.DevicePath, (ushort)evt.Scan);
                RebuildMacroLookup();

                TxtTriggerInfo.Text = string.IsNullOrEmpty(_selectedMacro.DevicePath)
                    ? "Keine Taste"
                    : $"{_selectedMacro.TriggerKey} (Scan={_selectedMacro.ScanCode}, {FormatDevicePath(_selectedMacro.DevicePath)})";

                // HIER GEHÖRT ES HIN:
                BtnScan.SetResourceReference(Button.ContentProperty, "StrScanButton");

                // Ausschnitt aus ProcessSingleInputEvent (Scan-Erfolg)
                string msgScan = string.Format(Application.Current.FindResource("MsgCouchDetected") as string ?? "✅ Couch-Gerät erfasst! ScanCode={0}", evt.Scan);
                string titleSuccess = Application.Current.FindResource("TitleSuccess") as string ?? "Erfolg";
                CustomMessageBox.Show(this, msgScan, titleSuccess, MessageBoxButton.OK);
                return;
            }

            if (evt.IsKeyDown && _deviceMonitor.IsCouchScanCode(evt.DevicePath, evt.Scan))
            {
                var macro = Macros.FirstOrDefault(m =>
                    string.Equals(m.DevicePath?.Trim(), evt.DevicePath?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    m.TriggerKey == KeyInterop.KeyFromVirtualKey((byte)evt.Vk));

                if (macro != null)
                    ExecuteMacro(macro);
            }
        }
        private string NormalizeDevicePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            return path.Trim()
                       .Replace(@"\??\", @"\\?\")
                       .ToLowerInvariant();
        }
        private readonly DateTime _startupTime = DateTime.Now;

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
           

            // Ignoriert jeglichen Input in den ersten 500ms nach Programmstart (verhindert den Start-Bug)
            if ((DateTime.Now - _startupTime).TotalMilliseconds < 500)
            {
                return CallNextHookEx(_lowLevelHookId, nCode, wParam, lParam);
            }

            if (_isExecutingMacro)
                return CallNextHookEx(_lowLevelHookId, nCode, wParam, lParam);

            if (nCode >= 0)
            {
                KBDLLHOOKSTRUCT kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                
                bool isInjected = (kbd.flags & 0x10) != 0; // LLKHF_INJECTED
                if (isInjected)
                {
                    return CallNextHookEx(_lowLevelHookId, nCode, wParam, lParam);
                }

                int msg = wParam.ToInt32();
                bool isKeyDown = (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN);
                bool isKeyUp = (msg == WM_KEYUP || msg == WM_SYSKEYUP);

                if (isKeyDown || isKeyUp)
                {
                    try
                    {
                        if (_hwnd != IntPtr.Zero)
                        {
                            while (PeekMessage(out MSG msgStruct, _hwnd, WM_INPUT, WM_INPUT, PM_REMOVE))
                            {
                                ProcessRawInput(msgStruct.lParam);
                            }
                        }

                        if (!string.IsNullOrEmpty(_lastInputDevicePath))
                        {
                            var normalizedLastPath = NormalizeDevicePath(_lastInputDevicePath);

                            int primaryCode = kbd.scanCode != 0 ? kbd.scanCode : kbd.vkCode;
                            
                            //Debug.WriteLine($"[DEBUG] Device: {normalizedLastPath} | Scan: {kbd.scanCode} | VK: {kbd.vkCode} | Primary: {primaryCode}");
                            
                            var keyByScan = (normalizedLastPath, primaryCode);
                            var keyByVk = (normalizedLastPath, kbd.vkCode);

                            //bool hasMacro = _scanCodeMacroLookup.TryGetValue(keyByScan, out var macro) ||
                            //               _scanCodeMacroLookup.TryGetValue(keyByVk, out macro);
                            bool hasMacro = _scanCodeMacroLookup.TryGetValue(keyByScan, out var macro);
                            if (hasMacro)
                            {
                                if (isKeyDown)
                                {
                                    Dispatcher.BeginInvoke(new Action(() => ExecuteMacro(macro!)));
                                }
                                return (IntPtr)1;
                            }

                            bool isCouchDevice = _deviceMonitor.IsCouchDevice(normalizedLastPath);
                            //Debug.WriteLine($"[HOOK] Device: {normalizedLastPath} | VkCode: {kbd.vkCode} | ScanCode: {kbd.scanCode} | Primary: {primaryCode}");
                            if (isCouchDevice && (kbd.scanCode == 56 || kbd.scanCode == 0))
                            {
                                return (IntPtr)1;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        //Debug.WriteLine($"Hook error: {ex}");
                    }
                }
            }


            
            
            return CallNextHookEx(_lowLevelHookId, nCode, wParam, lParam);
        }
        private void RebuildMacroLookup()
        {
            _scanCodeMacroLookup.Clear();
            foreach (var macro in Macros)
            {
                if (!string.IsNullOrWhiteSpace(macro.DevicePath))
                {
                    var normalizedPath = NormalizeDevicePath(macro.DevicePath);

                    if (macro.ScanCode > 0)
                    {
                        _scanCodeMacroLookup[(normalizedPath, macro.ScanCode)] = macro;
                    }

                    if (macro.VkCode > 0)
                    {
                        _scanCodeMacroLookup[(normalizedPath, macro.VkCode)] = macro;
                    }
                }
            }
            // Entferne die alte 'if (string.IsNullOrEmpty(_lastInputDevicePath))'-Logik hier vollständig!
        }




        private string GetDeviceNameCached(IntPtr hDevice)
        {
            return _deviceCache.GetOrAdd(hDevice, device =>
            {
                uint pcbSize = 0;
                GetRawInputDeviceInfo(device, RID_DEVICENAME, null, ref pcbSize);
                if (pcbSize <= 1) return "Unknown";

                StringBuilder sb = new((int)pcbSize);
                GetRawInputDeviceInfo(device, RID_DEVICENAME, sb, ref pcbSize);
                return sb.ToString();
            });
        }


        // ✅ PERF: Vor-Filterung verhindert unnötigen Overhead auf dem UI-Dispatcher
        private unsafe void ProcessRawInput(IntPtr lParam)
        {
            uint dwSize = 0, headerSize = (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER));
            GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, headerSize);

            if (dwSize == 0 || dwSize > 1024) return;

            byte* pBuffer = stackalloc byte[(int)dwSize];

            try
            {
                if (GetRawInputData(lParam, RID_INPUT, (IntPtr)pBuffer, ref dwSize, headerSize) == dwSize)
                {
                    RAWINPUTHEADER* header = (RAWINPUTHEADER*)pBuffer;
                    if (header->dwType == 0x00000001) // Keyboard
                    {
                        RAWKEYBOARD* keyboard = (RAWKEYBOARD*)(pBuffer + headerSize);

                        bool isKeyDown = (keyboard->Flags & 0x01) == 0;
                        int vkCode = (int)keyboard->VKey;
                        int scanCode = keyboard->MakeCode;
                        string devicePath = GetDeviceNameCached(header->hDevice);

                        // NEU: Zuletzt aktives Gerät sofort normalisiert speichern
                        _lastInputDevicePath = NormalizeDevicePath(devicePath);

                        _deviceMonitor.UpdateActiveScanCodes(devicePath, scanCode, isKeyDown);

                        // PERF-FILTER: Nur an UI-Thread delegieren, wenn gescannt wird oder relevante Couch-Events anliegen
                        bool isCouchDevice = _deviceMonitor.IsCouchDevice(devicePath);
                        bool isCouchScanCode = _deviceMonitor.IsCouchScanCode(devicePath, scanCode);

                        if (_isScanning || isCouchDevice || isCouchScanCode)
                        {
                            var evt = new RawInputEvent(devicePath, vkCode, scanCode, isKeyDown, (uint)Environment.TickCount);
                            Dispatcher.BeginInvoke(new Action(() => ProcessSingleInputEvent(evt)));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Debug.WriteLine($"RawInput Error: {ex}");
            }
        }

        private static (string FileName, string Arguments) ParseProcessParameters(string rawParam)
        {
            rawParam = rawParam.Trim();
            if (rawParam.StartsWith("\""))
            {
                int closingQuote = rawParam.IndexOf('"', 1);
                if (closingQuote > 0)
                {
                    string fileName = rawParam.Substring(1, closingQuote - 1);
                    string args = rawParam.Substring(closingQuote + 1).Trim();
                    return (fileName, args);
                }
                return (rawParam.Trim('"'), string.Empty);
            }
            else
            {
                int firstSpace = rawParam.IndexOf(' ');
                if (firstSpace > 0)
                {
                    string fileName = rawParam.Substring(0, firstSpace);
                    string args = rawParam.Substring(firstSpace + 1).Trim();
                    return (fileName, args);
                }
                return (rawParam, string.Empty);
            }
        }

        // ✅ SICHERHEIT & ROBUSTHEIT: Task-Handling abgesichert & Exception-Management erweitert
        private void ExecuteMacro(MacroModel macro)
        {
            if (string.IsNullOrWhiteSpace(macro.ActionParameter)) return;

            try
            {
                _isExecutingMacro = true;

                if (macro.ActionType == MacroActionType.RunApplication)
                {
                    string param = macro.ActionParameter.Trim();

                    if (param.StartsWith("Focus{", StringComparison.OrdinalIgnoreCase) && param.EndsWith("}"))
                    {
                        string processName = param.Substring(6, param.Length - 7).Trim();

                        // ✅ ROBUSTHEIT: Exception-Handling im asynchronen Kontext abgefangen
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await ForceWindowFocusAsync(processName);

                                // NEU: Schließt die Alt-KeyTips im Editor sofort nach dem Fokus-Wechsel
                                await Task.Delay(50);
                                keybd_event(0x1B, 0, 0, UIntPtr.Zero);       // VK_ESCAPE Down
                                keybd_event(0x1B, 0, 0x0002, UIntPtr.Zero);  // VK_ESCAPE Up
                            }
                            catch (Exception ex)
                            {
                                //Debug.WriteLine($"Focus Async Task Error: {ex}");
                            }
                        });
                        return;
                    }

                    var (fileName, arguments) = ParseProcessParameters(param);

                    // Ausschnitt aus ExecuteMacro (Dateisuche, Berechtigung, Fehler)
                    if (Path.IsPathRooted(fileName) && !File.Exists(fileName))
                    {
                        string msgFile = string.Format(Application.Current.FindResource("MsgFileNotFound") as string ?? "Die Datei '{0}' wurde nicht gefunden.", fileName);
                        string titleAbort = Application.Current.FindResource("TitleSecurityAbort") as string ?? "Sicherheitsabbruch";
                        CustomMessageBox.Show(this, msgFile, titleAbort, MessageBoxButton.OK);
                        return;
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        UseShellExecute = true
                    };

                    Process.Start(psi);
                }
                else if (macro.ActionType == MacroActionType.SendKeyboardSequence)
                {
                    string param = macro.ActionParameter.Trim();

                    // Echter Alt+Tab Task-Switch über Win32 (wird von Windows nicht blockiert)
                    if (param.Equals("%{TAB}", StringComparison.OrdinalIgnoreCase))
                    {
                        keybd_event(0x12, 0, 0, UIntPtr.Zero);       // VK_MENU (Alt) Down
                        keybd_event(0x09, 0, 0, UIntPtr.Zero);       // VK_TAB Down
                        keybd_event(0x09, 0, 0x0002, UIntPtr.Zero);  // VK_TAB Up
                        keybd_event(0x12, 0, 0x0002, UIntPtr.Zero);  // VK_MENU (Alt) Up
                        return;
                    }

                    // Für alle anderen normalen Tastensequenzen
                    System.Windows.Forms.SendKeys.SendWait(param);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                string msgPerm = Application.Current.FindResource("MsgNoPermission") as string ?? "Keine Berechtigung zur Ausführung.";
                string titleDenied = Application.Current.FindResource("TitleAccessDenied") as string ?? "Zugriff verweigert";
                CustomMessageBox.Show(this, msgPerm, titleDenied, MessageBoxButton.OK);
            }
            catch (Exception ex)
            {
                string msgMacroErr = string.Format(Application.Current.FindResource("MsgMacroExecError") as string ?? "Fehler bei Makroausführung: {0}", ex.Message);
                string titleError = Application.Current.FindResource("TitleError") as string ?? "Fehler";
                CustomMessageBox.Show(this, msgMacroErr, titleError, MessageBoxButton.OK);
            }
            finally
            {
                _isExecutingMacro = false;
            }
        }

        private async Task ForceWindowFocusAsync(string processName)
        {
            await Task.Run(() =>
            {
                IntPtr targetHwnd = IntPtr.Zero;

                if (processName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                {
                    EnumWindows((hWnd, lParam) =>
                    {
                        if (!IsWindowVisible(hWnd)) return true;

                        StringBuilder className = new StringBuilder(256);
                        GetClassName(hWnd, className, 256);

                        if (className.ToString() == "CabinetWClass")
                        {
                            targetHwnd = hWnd;
                            return false; // Früher Abbruch
                        }

                        return true;
                    }, IntPtr.Zero);

                    if (targetHwnd == IntPtr.Zero)
                    {
                        Process.Start("explorer.exe");
                        return;
                    }
                }

                if (targetHwnd == IntPtr.Zero)
                {
                    var processes = Process.GetProcessesByName(processName);
                    foreach (var p in processes)
                    {
                        if (p.MainWindowHandle != IntPtr.Zero)
                        {
                            targetHwnd = p.MainWindowHandle;
                            break;
                        }
                    }
                }

                if (targetHwnd == IntPtr.Zero)
                {
                    EnumWindows((hWnd, lParam) =>
                    {
                        if (!IsWindowVisible(hWnd)) return true;

                        StringBuilder sb = new StringBuilder(256);
                        GetWindowText(hWnd, sb, 256);
                        string title = sb.ToString();

                        if (string.IsNullOrWhiteSpace(title)) return true;

                        GetWindowThreadProcessId(hWnd, out uint procId);
                        try
                        {
                            var proc = Process.GetProcessById((int)procId);

                            if (string.Equals(proc.ProcessName, processName, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(proc.ProcessName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) ||
                                title.Contains(processName, StringComparison.OrdinalIgnoreCase))
                            {
                                targetHwnd = hWnd;
                                return false; // Früher Abbruch
                            }
                        }
                        catch { }

                        return true;
                    }, IntPtr.Zero);
                }

                if (targetHwnd != IntPtr.Zero)
                {
                    if (IsIconic(targetHwnd))
                    {
                        ShowWindow(targetHwnd, SW_RESTORE);
                    }
                    else
                    {
                        ShowWindow(targetHwnd, SW_SHOW);
                    }

                    keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
                    SetForegroundWindow(targetHwnd);
                    keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
            });
        }
        #region UI-Events & File Operations
        private void BtnImportMacros_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON-Datei (*.json)|*.json|Alle Dateien (*.*)|*.*",
                Title = "Makros wiederherstellen / importieren"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(openFileDialog.FileName);
                    var importedMacros = JsonSerializer.Deserialize<List<MacroModel>>(json);

                    // Ausschnitt aus BtnImportMacros_Click
                    if (importedMacros == null || importedMacros.Count == 0)
                    {
                        string msgNoMacros = Application.Current.FindResource("MsgNoValidMacrosFound") as string ?? "Keine gültigen Makros in der Datei gefunden.";
                        string titleHint = Application.Current.FindResource("TitleHint") as string ?? "Hinweis";
                        CustomMessageBox.Show(this, msgNoMacros, titleHint, MessageBoxButton.OK);
                        return;
                    }

                    string rawPrompt = Application.Current.FindResource("MsgImportPrompt") as string ?? "Es wurden {0} Makros gefunden...";
                    string promptText = string.Format(rawPrompt, importedMacros.Count);
                    string titleImport = Application.Current.FindResource("TitleImportMode") as string ?? "Import-Modus wählen";

                    var result = CustomMessageBox.Show(this, promptText, titleImport, MessageBoxButton.YesNoCancel);

                    if (result == MessageBoxResult.Cancel)
                        return;

                    if (result == MessageBoxResult.No)
                    {
                        Macros.Clear();
                    }

                    foreach (var macro in importedMacros)
                    {
                        Macros.Add(macro);
                    }

                    RebuildMacroLookup();

                    string msgImportOk = string.Format(Application.Current.FindResource("MsgImportSuccess") as string ?? "✅ {0} Makro(s) erfolgreich importiert!", importedMacros.Count);
                    string titleSuccess = Application.Current.FindResource("TitleSuccess") as string ?? "Erfolg";
                    CustomMessageBox.Show(this, msgImportOk, titleSuccess, MessageBoxButton.OK);
                }
                catch (Exception ex)
                {
                    string msgImportErr = string.Format(Application.Current.FindResource("MsgImportError") as string ?? "Fehler beim Importieren der Datei: {0}", ex.Message);
                    string titleError = Application.Current.FindResource("TitleError") as string ?? "Fehler";
                    CustomMessageBox.Show(this, msgImportErr, titleError, MessageBoxButton.OK);
                }
            }
        }

        private void BtnExportMacros_Click(object sender, RoutedEventArgs e)
        {
            // Ausschnitt aus BtnExportMacros_Click
            if (Macros.Count == 0)
            {
                string msgNoExport = Application.Current.FindResource("MsgNoMacrosToExport") as string ?? "Es sind keine Makros zum Exportieren vorhanden.";
                string titleHint = Application.Current.FindResource("TitleHint") as string ?? "Hinweis";
                CustomMessageBox.Show(this, msgNoExport, titleHint, MessageBoxButton.OK);
                return;
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON-Datei (*.json)|*.json|Alle Dateien (*.*)|*.*",
                DefaultExt = "json",
                FileName = $"couchkeys_export_{DateTime.Now:yyyyMMdd_HHmmss}.json",
                Title = "Makros exportieren"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    string json = JsonSerializer.Serialize(Macros, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(saveFileDialog.FileName, json);
                    string msgExportOk = string.Format(Application.Current.FindResource("MsgExportSuccess") as string ?? "✅ {0} Makro(s) erfolgreich exportiert!", Macros.Count);
                    string titleSuccess = Application.Current.FindResource("TitleSuccess") as string ?? "Erfolg";
                    CustomMessageBox.Show(this, msgExportOk, titleSuccess, MessageBoxButton.OK);
                }
                catch (Exception ex)
                {
                    string msgExportErr = string.Format(Application.Current.FindResource("MsgExportError") as string ?? "Fehler beim Exportieren: {0}", ex.Message);
                    string titleError = Application.Current.FindResource("TitleError") as string ?? "Fehler";
                    CustomMessageBox.Show(this, msgExportErr, titleError, MessageBoxButton.OK);
                }
            }
        }

        // ✅ PERF: Wiederholtes String-Splitting reduziert durch FormatDevicePath
        private void LstMacros_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedMacro = LstMacros.SelectedItem as MacroModel;
            if (_selectedMacro == null) return;

            TxtMacroName.DataContext = _selectedMacro;

            TxtTriggerInfo.Text = string.IsNullOrEmpty(_selectedMacro.DevicePath)
                ? ""
                : $"{_selectedMacro.TriggerKey} (Scan={_selectedMacro.ScanCode}, {FormatDevicePath(_selectedMacro.DevicePath)})";

            if (RbRunApp != null && RbSendKeys != null)
            {
                RbRunApp.IsChecked = _selectedMacro.ActionType == MacroActionType.RunApplication;
                RbSendKeys.IsChecked = _selectedMacro.ActionType == MacroActionType.SendKeyboardSequence;
            }

            TxtAppPath.Text = _selectedMacro.ActionType == MacroActionType.RunApplication ? _selectedMacro.ActionParameter : "";
            TxtKeySequence.Text = _selectedMacro.ActionType == MacroActionType.SendKeyboardSequence ? _selectedMacro.ActionParameter : "";
        }

        private void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedMacro == null)
            {
                var newMacro = new MacroModel
                {
                    Name = Application.Current.FindResource("StrNeuMakro") as string ?? "Neues Makro"
                };
                Macros.Add(newMacro);
                LstMacros.SelectedItem = newMacro;
                _selectedMacro = newMacro;
            }

            _isScanning = true;
            // Dynamische Bindung an den Schlüssel für "Bitte Taste drücken"
            BtnScan.SetResourceReference(Button.ContentProperty, "StrTasteDrucken");
        }

        // Ausschnitt aus btnSaveMacro_Click & SaveCurrent
        private void btnSaveMacro_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedMacro == null)
            {
                string msgSelect = Application.Current.FindResource("MsgPleaseSelectMacro") as string ?? "Bitte Makro auswählen!";
                string titleError = Application.Current.FindResource("TitleError") as string ?? "Fehler";
                CustomMessageBox.Show(this, msgSelect, titleError, MessageBoxButton.OK);
                return;
            }

            SaveCurrent();
            string msgSaved = Application.Current.FindResource("MsgMacroSaved") as string ?? "✅ Makro gespeichert!";
            string titleSuccess = Application.Current.FindResource("TitleSuccess") as string ?? "Erfolg";
            CustomMessageBox.Show(this, msgSaved, titleSuccess, MessageBoxButton.OK);
        }

        private void BtnAddMacro_Click(object sender, RoutedEventArgs e)
        {
            var m = new MacroModel
            {
                Name = Application.Current.FindResource("StrNeuMakro") as string
            };
            Macros.Add(m);
            LstMacros.SelectedItem = m;
            _selectedMacro = m;
        }

        private void BtnDeleteMacro_Click(object sender, RoutedEventArgs e)
        {
            if (LstMacros.SelectedItem is MacroModel m)
            {
                Macros.Remove(m);
                RebuildMacroLookup();
            }
        }


        private void BtnBrowseApp_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Exe-Dateien (*.exe)|*.exe|Skripte (*.bat, *.ps1)|*.bat;*.ps1|Alle Dateien|*.*"
            };
            if (dlg.ShowDialog() == true)
                TxtAppPath.Text = dlg.FileName;
        }

        private void ActionType_Changed(object sender, RoutedEventArgs e)
        {
            UpdateActionTypeUI();
        }

        private void SaveCurrent()
        {
            if (_selectedMacro == null) return;

            try
            {
                _selectedMacro.Name = TxtMacroName.Text;
                _selectedMacro.ActionType = RbRunApp?.IsChecked == true
                    ? MacroActionType.RunApplication
                    : MacroActionType.SendKeyboardSequence;
                _selectedMacro.ActionParameter = RbRunApp?.IsChecked == true
                    ? TxtAppPath.Text
                    : TxtKeySequence.Text;

                RebuildMacroLookup();
                SaveMacrosToFile();
                LstMacros.Items.Refresh();
            }
            catch (Exception ex)
            {
                string msgSaveErr = string.Format(Application.Current.FindResource("MsgSaveError") as string ?? "Fehler beim Speichern: {0}", ex.Message);
                string titleError = Application.Current.FindResource("TitleError") as string ?? "Fehler";
                CustomMessageBox.Show(this, msgSaveErr, titleError, MessageBoxButton.OK);
            }
        }

        private void LoadMacros()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    string json = File.ReadAllText(_filePath);
                    var list = JsonSerializer.Deserialize<ObservableCollection<MacroModel>>(json);
                    if (list != null)
                    {
                        Macros.Clear();
                        foreach (var m in list)
                        {
                            Macros.Add(m);
                            if (!string.IsNullOrEmpty(m.DevicePath) && m.ScanCode > 0)
                            {
                                _deviceMonitor.RegisterCouchDevice(m.DevicePath, (ushort)m.ScanCode);
                            }
                        }
                        RebuildMacroLookup();
                    }
                }
                catch (Exception ex)
                {
                    //Debug.WriteLine($"Load error: {ex}");
                }
            }
        }

        private void SaveMacrosToFile()
        {
            try
            {
                string? directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string tempPath = _filePath + ".tmp";
                string json = JsonSerializer.Serialize(Macros, new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _filePath, overwrite: true);
            }
            catch (Exception ex)
            {
                //Debug.WriteLine($"Save file error: {ex}");
            }
        }



        private void CleanupResources()
        {
            if (_lowLevelHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_lowLevelHookId);
                _lowLevelHookId = IntPtr.Zero;
            }

            _notifyIcon?.Dispose();
        }


        #endregion





        private void LoadSavedSettings()
        {
            // Sprache laden & anwenden
            string savedLang = CouchKeys.Properties.Settings.Default.SelectedLanguage;
            if (string.IsNullOrEmpty(savedLang)) savedLang = "de";
            ApplyLanguage(savedLang);

            // Theme laden & anwenden
            string savedTheme = CouchKeys.Properties.Settings.Default.SelectedTheme;
            if (string.IsNullOrEmpty(savedTheme)) savedTheme = "ThemeLight";
            SwitchTheme(savedTheme);
        }

        // Allgemeines Öffnen von ContextMenus bei Button-Klick
        private void BtnContextMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string themeTag)
            {
                SwitchTheme(themeTag);
            }
        }

        public void SwitchTheme(string themeTag)
        {
            string themeFile = $"{themeTag}.xaml";
            bool isDark = IsDarkTheme(themeTag);

            // Wenn "ThemeSystem" gewählt wurde, Windows-Design abfragen
            if (themeTag == "ThemeSystem")
            {
                bool isSystemDark = IsWindowsInDarkMode();
                themeFile = isSystemDark ? "ThemeDark.xaml" : "ThemeLight.xaml";
                isDark = isSystemDark;
            }

            // Theme laden und in MergedDictionaries ersetzen
            var newThemeDict = new ResourceDictionary
            {
                Source = new Uri($"Themes/{themeFile}", UriKind.Relative)
            };

            ReplaceResourceDictionary("Themes/", newThemeDict);

            // Häkchen & Titelleiste aktualisieren
            UpdateThemeMenuCheckmark(themeTag);
            UpdateTitleBarTheme(isDark);

            // Einstellung dauerhaft speichern
            CouchKeys.Properties.Settings.Default.SelectedTheme = themeTag;
            CouchKeys.Properties.Settings.Default.Save();
        }

        // Hilfsmethode: Fragt den Windows 10/11 System-Designmodus ab
        private bool IsWindowsInDarkMode()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object registryValue = key.GetValue("AppsUseLightTheme");
                        if (registryValue != null)
                        {
                            return (int)registryValue == 0; // 0 = Dark Mode, 1 = Light Mode
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        private void SwitchLanguage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string langCode)
            {
                ApplyLanguage(langCode);
            }
        }

        private void ApplyLanguage(string langCode)
        {
            // 1. Alten Standard-Namen vor dem Wechsel ermitteln (falls ein Makro diesen Namen trägt)
            string oldDefaultName = Application.Current.FindResource("StrNeuMakro") as string;

            this.FlowDirection = (langCode == "ar") ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            var resourceUri = new Uri($"/Languages/Strings.{langCode}.xaml", UriKind.Relative);
            var newDict = new ResourceDictionary { Source = resourceUri };

            ReplaceResourceDictionary("/Languages/", newDict);
            UpdateLanguageMenuCheckmark(langCode);

            // 2. Neuen Standard-Namen aus der frisch geladenen Sprache holen
            string newDefaultName = Application.Current.FindResource("StrNeuMakro") as string ?? "Neues Makro";

            // 3. Alle Makros durchgehen: Wenn der Name dem alten Standard entspricht, auf die neue Sprache aktualisieren
            if (!string.IsNullOrEmpty(oldDefaultName))
            {
                foreach (var macro in Macros)
                {
                    if (macro.Name == oldDefaultName || macro.Name == "Neues Makro" || macro.Name == "New Macro" || macro.Name == "Nouveau macro")
                    {
                        macro.Name = newDefaultName;
                    }
                }
                // UI der Liste aktualisieren
                LstMacros.Items.Refresh();
            }

            // Button-Text je nach Scan-Zustand anpassen
            if (_isScanning)
            {
                BtnScan.SetResourceReference(Button.ContentProperty, "StrTasteDrucken");
            }
            else
            {
                BtnScan.SetResourceReference(Button.ContentProperty, "StrScanButton");
            }

            CouchKeys.Properties.Settings.Default.SelectedLanguage = langCode;
            CouchKeys.Properties.Settings.Default.Save();
        }
        private void UpdateThemeMenuCheckmark(string activeTheme)
        {
            // Sucht das ContextMenu beim Theme-Button (BtnTheme oder aus Template)
            if (BtnTheme?.ContextMenu != null)
            {
                foreach (MenuItem item in BtnTheme.ContextMenu.Items.OfType<MenuItem>())
                {
                    item.IsChecked = (item.Tag?.ToString() == activeTheme);
                }
            }
        }

        private void UpdateLanguageMenuCheckmark(string activeLang)
        {
            if (BtnLanguage?.ContextMenu != null)
            {
                foreach (MenuItem item in BtnLanguage.ContextMenu.Items.OfType<MenuItem>())
                {
                    item.IsChecked = (item.Tag?.ToString() == activeLang);
                }
            }
        }

        private bool IsDarkTheme(string themeTag)
        {
            if (themeTag == "ThemeSystem")
            {
                return IsWindowsInDarkMode();
            }

            string lower = themeTag.ToLower();

            // Nur Themes eintragen, die eine DUNKLE Titelleiste haben sollen
            return lower.Contains("dark") ||
                   lower.Contains("nord") ||
                   lower.Contains("emerald") ||
                   lower.Contains("amber") ||
                   lower.Contains("purple") ||
                   lower.Contains("dracula") ||
                   lower.Contains("monokai") ||
                   lower.Contains("gruvbox");
        }
        private void UpdateTitleBarTheme(bool isDark)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int darkMode = isDark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
            }
        }

        private void ReplaceResourceDictionary(string pathPrefix, ResourceDictionary newDict)
        {
            var existingDict = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains(pathPrefix));

            if (existingDict != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(existingDict);
            }

            Application.Current.Resources.MergedDictionaries.Add(newDict);
        }

        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            string activeTheme = CouchKeys.Properties.Settings.Default.SelectedTheme;
            bool isDark = IsDarkTheme(activeTheme);

            var helpWin = new AppTemplate.Help.HelpWindow(isDark);
            helpWin.Owner = this;
            helpWin.ShowDialog();
        }

        private void BtnPP_Click(object sender, RoutedEventArgs e)
        {
            string activeTheme = CouchKeys.Properties.Settings.Default.SelectedTheme;
            bool isDark = IsDarkTheme(activeTheme);

            // Angenommen, deine aktuelle Sprache ist in einer Variablen oder Eigenschaft gespeichert:
            string currentLang = CouchKeys.Properties.Settings.Default.SelectedLanguage; // (Oder deine entsprechende Variable)

            var paypalWin = new AppTemplate.PayPal.PayPalWindow(isDark, currentLang);
            paypalWin.Owner = this;
            paypalWin.ShowDialog();
        }

        private void MenuOpen_Click(object sender, RoutedEventArgs e)
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            // Ruft indirekt das OnClosing-Event auf und schließt die Anwendung sauber
            Application.Current.Shutdown();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // 1. Tray-Icon aus der Taskleiste entfernen
            MyNotifyIcon?.Dispose();

            // 2. Deine Hooks, Mutex oder sonstige Ressourcen freigeben
            CleanupResources();

            base.OnClosing(e);
            // HIER KEIN Application.Current.Shutdown() AUFRUFEN!
        }
        private void CheckInitialAutostartStatus()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(AutostartRegistryKey, false))
                {
                    var value = key?.GetValue(AppName);
                    _isAutostartActive = (value != null);
                }
            }
            catch
            {
                _isAutostartActive = false;
            }
            UpdateAutostartIconVisual();
        }

        private void BtnAutostartIcon_Click(object sender, RoutedEventArgs e)
        {
            _isAutostartActive = !_isAutostartActive;
            SetWindowsAutostart(_isAutostartActive);
            UpdateAutostartIconVisual();
        }

        private void SetWindowsAutostart(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(AutostartRegistryKey, true))
                {
                    if (key == null) return;

                    if (enable)
                    {
                        string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                        key.SetValue(AppName, $"\"{exePath}\" --autostart");
                    }
                    else
                    {
                        if (key.GetValue(AppName) != null)
                        {
                            key.DeleteValue(AppName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Debug.WriteLine($"Autostart error: {ex}");
                string msgAutoErr = string.Format(Application.Current.FindResource("MsgAutostartError") as string ?? "Fehler beim Ändern des Autostarts: {0}", ex.Message);
                string titleError = Application.Current.FindResource("TitleError") as string ?? "Fehler";
                CustomMessageBox.Show(this, msgAutoErr, titleError, MessageBoxButton.OK);
            }
        }

        private void UpdateAutostartIconVisual()
        {
            if (PathRocket == null || BtnAutostartIcon == null) return;

            string lightningOnly = "M11,15H6L13,1V9H18L11,23V15Z";
            string circleAndLightning = "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2 M11,15H6L13,1V9H18L11,23V15Z";

            if (_isAutostartActive)
            {
                PathRocket.Data = System.Windows.Media.Geometry.Parse(circleAndLightning);
                BtnAutostartIcon.SetResourceReference(ToolTipProperty, "bAutoStart0");
            }
            else
            {
                PathRocket.Data = System.Windows.Media.Geometry.Parse(lightningOnly);
                BtnAutostartIcon.SetResourceReference(ToolTipProperty, "bAutoStart1");
            }
        }
    }
}