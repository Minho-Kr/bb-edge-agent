using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BBEdgeAgent.Services;
using BBEdgeAgent.Wpf.ViewModels;

namespace BBEdgeAgent.Wpf.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel  _vm;
        private readonly RfidService    _rfid;
        private readonly SupabaseService _supabase;
        private readonly LocalDbService  _db;
        private readonly AttendanceService _attendance;

        private static readonly Color IdleColor     = Color.FromRgb(30, 42, 62);
        private static readonly Color SuccessColor  = Color.FromRgb(10, 40, 30);
        private static readonly Color AlreadyColor  = Color.FromRgb(45, 37, 5);
        private static readonly Color NotFoundColor = Color.FromRgb(45, 10, 10);

        public MainWindow(AppConfig cfg, RfidService rfid, SupabaseService supabase,
                         LocalDbService db, AttendanceService attendance)
        {
            _rfid       = rfid;
            _supabase   = supabase;
            _db         = db;
            _attendance = attendance;

            _vm = new MainViewModel(cfg, rfid, supabase, db, attendance);
            InitializeComponent();
            DataContext = _vm;

            _vm.OpenClassViewRequested     = OpenClassView;
            _vm.OpenTagMappingRequested    = OpenTagMapping;
            _vm.OpenManualCheckInRequested = OpenManualCheckIn;
            _vm.OpenSettingsRequested      = OpenSettings;
            _vm.OpenRfidLogRequested       = OpenRfidLog;
            _vm.CloseRequested             = () => Close();
            _vm.ScanStateChanged           += AnimateScanBg;

            LangBtn.Content = L.Current == "en" ? "한국어" : "English";
            Closed += (_, __) => _vm.Cleanup();
        }

        private void AnimateScanBg(ScanState state)
        {
            var target = state switch
            {
                ScanState.Success  => SuccessColor,
                ScanState.Already  => AlreadyColor,
                ScanState.NotFound => NotFoundColor,
                _                  => IdleColor,
            };
            ScanBg.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(target, new Duration(TimeSpan.FromMilliseconds(300))));
        }

        private void OpenClassView()
        {
            new ClassViewWindow(_vm.Cfg, _supabase) { Owner = this }.ShowDialog();
        }

        private void OpenTagMapping()
        {
            bool was = _vm.IsScanning;
            _vm.StopScan();
            new TagMappingWindow(_vm.Cfg, _rfid, _supabase, _db) { Owner = this }.ShowDialog();
            if (was) _vm.StartScan();
        }

        private void OpenManualCheckIn()
        {
            var dlg = new ManualCheckInWindow(_db, _attendance, _vm) { Owner = this };
            dlg.ShowDialog();
        }

        private void OpenSettings()
        {
            bool was = _vm.IsScanning;
            _vm.StopScan();
            new SettingsWindow(_vm.Cfg, _rfid, _supabase, _vm) { Owner = this }.ShowDialog();
            if (was) _vm.StartScan();
        }

        private void OpenRfidLog()
        {
            new RfidLogWindow(_supabase) { Owner = this }.ShowDialog();
        }

        private void LangBtn_Click(object sender, RoutedEventArgs e)
            => _vm.ToggleLangCommand.Execute(null);
    }
}
