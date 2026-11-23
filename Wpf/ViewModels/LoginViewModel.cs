using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace BBEdgeAgent.Wpf.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly AppConfig _cfg;
        private string _errorMessage = "";

        public string ErrorMessage   { get => _errorMessage; set => Set(ref _errorMessage, value); }
        public string LangToggleText => L.Current == "en" ? "한국어" : "English";

        public RelayCommand ToggleLangCommand { get; }

        public LoginViewModel(AppConfig cfg)
        {
            _cfg = cfg;
            ToggleLangCommand = new RelayCommand(ToggleLang);
        }

        public bool TryLogin(string pin)
        {
            ErrorMessage = "";
            if (_cfg.VerifyPin(pin)) return true;
            ErrorMessage = L.InvalidPin;
            return false;
        }

        private void ToggleLang()
        {
            L.Current     = L.Current == "en" ? "ko" : "en";
            _cfg.Language = L.Current;
            _cfg.Save();
            var exe = Assembly.GetEntryAssembly()?.Location ?? "";
            if (!string.IsNullOrEmpty(exe)) Process.Start(exe);
            Application.Current.Shutdown();
        }
    }
}
