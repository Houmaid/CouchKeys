using System.Windows;

namespace CouchKeys
{
    public partial class CustomMessageBox : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

        public CustomMessageBox(string message, string title, MessageBoxButton buttons)
        {
            InitializeComponent();
            Title = title;
            TxtMessage.Text = message;
            TxtTitle.Text = title; // <--- Füge diese Zeile hier hinzu

            if (buttons == MessageBoxButton.OK)
            {
                BtnOk.Visibility = Visibility.Visible;
                BtnOk.IsDefault = true;
            }
            else if (buttons == MessageBoxButton.YesNoCancel)
            {
                BtnYes.Visibility = Visibility.Visible;
                BtnNo.Visibility = Visibility.Visible;
                BtnCancel.Visibility = Visibility.Visible;
                BtnYes.IsDefault = true;
            }
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.Yes; Close(); }
        private void BtnNo_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.No; Close(); }
        private void BtnCancel_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.Cancel; Close(); }
        private void BtnOk_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.OK; Close(); }

        public static MessageBoxResult Show(Window owner, string message, string title, MessageBoxButton buttons = MessageBoxButton.OK)
        {
            var msgBox = new CustomMessageBox(message, title, buttons) { Owner = owner };
            msgBox.ShowDialog();
            return msgBox.Result;
        }
    }
}