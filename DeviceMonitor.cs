// 📁 DeviceMonitor.cs – KORRIGIERT (funktioniert mit Fernbedienungen!)
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CouchKeys
{
    public record RawInputEvent(string DevicePath, int Vk, int Scan, bool IsKeyDown, uint Time);

    public class DeviceMonitor
    {
        private readonly ConcurrentQueue<RawInputEvent> _inputQueue = new();
        private readonly Dictionary<string, HashSet<int>> _couchDeviceScanCodes = new(); // devicePath → scanCodes
        private readonly HashSet<int> _activeCouchScanCodes = new(); // aktive scanCodes für Blockierung

        public ConcurrentQueue<RawInputEvent> InputQueue => _inputQueue;

        public void RegisterCouchDevice(string devicePath, int scanCode)
        {
            lock (_couchDeviceScanCodes)
            {
                if (!_couchDeviceScanCodes.ContainsKey(devicePath))
                    _couchDeviceScanCodes[devicePath] = new HashSet<int>();

                _couchDeviceScanCodes[devicePath].Add(scanCode);
                Debug.WriteLine($"[DeviceMonitor] Couch-Gerät registriert: {devicePath}, ScanCode={scanCode}");
            }
        }

        public bool IsCouchDevice(string devicePath)
        {
            lock (_couchDeviceScanCodes)
                return _couchDeviceScanCodes.ContainsKey(devicePath);
        }

        public bool IsCouchScanCode(string devicePath, int scanCode)
        {
            lock (_couchDeviceScanCodes)
            {
                return _couchDeviceScanCodes.TryGetValue(devicePath, out var scanCodes)
                    && scanCodes.Contains(scanCode);
            }
        }

        public void UpdateActiveScanCodes(string devicePath, int scanCode, bool isDown)
        {
            if (isDown && IsCouchScanCode(devicePath, scanCode))
            {
                _activeCouchScanCodes.Add(scanCode);
            }
            else
            {
                _activeCouchScanCodes.Remove(scanCode);
            }
        }
        public bool IsCouchScanCodeByAnyDevice(int scanCode)
        {
            lock (_couchDeviceScanCodes)
            {
                foreach (var pair in _couchDeviceScanCodes)
                {
                    if (pair.Value.Contains(scanCode))
                        return true;
                }
                return false;
            }
        }
        public bool IsCouchScanCodeActive(int scanCode) => _activeCouchScanCodes.Contains(scanCode);
    }
}
