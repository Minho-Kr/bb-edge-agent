using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BBEdgeAgent.Services;
using BBEdgeAgent.Wpf.ViewModels;

namespace BBEdgeAgent.Wpf.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _vm;

        public SettingsWindow(AppConfig cfg, RfidService rfid, SupabaseService supabase, MainViewModel mainVm)
        {
            _vm = new SettingsViewModel(cfg, rfid, supabase);
            InitializeComponent();
            DataContext = _vm;

            PassBox.Password = cfg.EdgePassword;

            _vm.CloseRequested    = () => Close();
            _vm.SavedSuccessfully = () => mainVm.OnSettingsSaved();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ScrollTextBoxesToStart(this);
        }

        private static void ScrollTextBoxesToStart(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is TextBox tb) { tb.CaretIndex = 0; tb.ScrollToHome(); }
                ScrollTextBoxesToStart(child);
            }
        }

        private void PassBox_Changed(object sender, RoutedEventArgs e)
            => _vm.EdgePassword = ((PasswordBox)sender).Password;

        private void NewPin_Changed(object sender, RoutedEventArgs e)
            => _vm.NewPin = ((PasswordBox)sender).Password;

        private void ConfirmPin_Changed(object sender, RoutedEventArgs e)
            => _vm.ConfirmPin = ((PasswordBox)sender).Password;
    }
}
