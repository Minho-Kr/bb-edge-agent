using System.Windows;
using System.Windows.Controls;
using BBEdgeAgent.Wpf.ViewModels;

namespace BBEdgeAgent.Wpf.Views
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _vm;

        // Expose text for XAML bindings
        public string LoginTitle    => L.LoginTitle;
        public string LoginBtnText  => L.LoginBtn;
        public string DefaultPinHint => L.DefaultPinHint;

        public LoginWindow(AppConfig cfg)
        {
            _vm = new LoginViewModel(cfg);
            InitializeComponent();
            DataContext = _vm;
            LangBtn.Content = L.Current == "en" ? "한국어" : "English";
            Loaded += (_, __) => Pin1.Focus();
        }

        // ── PIN box auto-advance ─────────────────────────────

        private void Pin1_Changed(object sender, RoutedEventArgs e)
        {
            if (Pin1.Password.Length == 1) Pin2.Focus();
            ErrorText.Text = "";
        }

        private void Pin2_Changed(object sender, RoutedEventArgs e)
        {
            if (Pin2.Password.Length == 1) Pin3.Focus();
            else if (Pin2.Password.Length == 0) Pin1.Focus();
            ErrorText.Text = "";
        }

        private void Pin3_Changed(object sender, RoutedEventArgs e)
        {
            if (Pin3.Password.Length == 1) Pin4.Focus();
            else if (Pin3.Password.Length == 0) Pin2.Focus();
            ErrorText.Text = "";
        }

        private void Pin4_Changed(object sender, RoutedEventArgs e)
        {
            if (Pin4.Password.Length == 0) { Pin3.Focus(); return; }
            TryLogin();
        }

        private void LoginBtn_Click(object sender, RoutedEventArgs e) => TryLogin();

        private void LangBtn_Click(object sender, RoutedEventArgs e) => _vm.ToggleLangCommand.Execute(null);

        private void TryLogin()
        {
            var pin = Pin1.Password + Pin2.Password + Pin3.Password + Pin4.Password;
            if (pin.Length < 4) { ErrorText.Text = "Enter all 4 digits."; return; }

            if (_vm.TryLogin(pin))
            {
                DialogResult = true;  // WPF가 자동으로 창을 닫음
            }
            else
            {
                ErrorText.Text = _vm.ErrorMessage;
                ClearPin();
            }
        }

        private void ClearPin()
        {
            Pin1.Clear(); Pin2.Clear(); Pin3.Clear(); Pin4.Clear();
            Pin1.Focus();
        }
    }
}
