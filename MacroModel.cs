using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CouchKeys
{
    public enum MacroActionType
    {
        RunApplication,
        SendKeyboardSequence
    }

    public class MacroModel : INotifyPropertyChanged
    {
        private string _name = "";
        private string _devicePath = "";
        private Key? _triggerKey;
        private int _scanCode;
        private MacroActionType _actionType = MacroActionType.RunApplication;
        private string _actionParameter = "";

        public int VkCode { get; set; }
        public string? DeviceName { get; set; }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string DevicePath
        {
            get => _devicePath;
            set { _devicePath = value; OnPropertyChanged(); }
        }

        public Key? TriggerKey
        {
            get => _triggerKey;
            set
            {
                _triggerKey = value ?? Key.None;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TriggerKeyDisplay)); // Aktualisiert die Anzeige automatisch
            }
        }

        // Neue Property für die fehlerfreie Bindung im XAML
        public string TriggerKeyDisplay => $"Taste: {TriggerKey}";

        public int ScanCode
        {
            get => _scanCode;
            set { _scanCode = value; OnPropertyChanged(); }
        }

        public MacroActionType ActionType
        {
            get => _actionType;
            set { _actionType = value; OnPropertyChanged(); }
        }

        public string ActionParameter
        {
            get => _actionParameter;
            set { _actionParameter = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}