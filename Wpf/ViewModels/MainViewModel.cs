using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using BBEdgeAgent.Models;
using BBEdgeAgent.Services;

namespace BBEdgeAgent.Wpf.ViewModels
{
    public enum ScanState { Idle, Success, Already, NotFound }

    public class MainViewModel : ViewModelBase
    {
        // Services
        public  readonly AppConfig         Cfg;
        private readonly RfidService       _rfid;
        private readonly SupabaseService   _supabase;
        private readonly LocalDbService    _db;
        private readonly AttendanceService _attendance;

        // ── Status bar ─────────────────────────────────────────
        private bool   _readerOnline;
        private bool   _cloudOnline;
        private int    _todayCount;
        private int    _queueCount;
        private string _timeDisplay = "";
        private string _branchName  = "";

        public bool   ReaderOnline  { get => _readerOnline; private set => Set(ref _readerOnline, value); }
        public bool   CloudOnline   { get => _cloudOnline;  private set => Set(ref _cloudOnline,  value); }
        public int    TodayCount    { get => _todayCount;   private set => Set(ref _todayCount,   value); }
        public int    QueueCount    { get => _queueCount;   private set => Set(ref _queueCount,   value); }
        public string TimeDisplay   { get => _timeDisplay;  private set => Set(ref _timeDisplay,  value); }
        public string BranchName    { get => _branchName;   private set => Set(ref _branchName,   value); }

        // ── Scan display ───────────────────────────────────────
        private ScanState _scanState = ScanState.Idle;
        private string _scanIcon    = "⊙";
        private string _scanTitle   = "";
        private string _scanBelt    = "";
        private string _scanSub     = "";
        private bool   _isScanning  = false;

        public ScanState ScanState  { get => _scanState; private set => Set(ref _scanState, value); }
        public string ScanIcon      { get => _scanIcon;  private set => Set(ref _scanIcon,  value); }
        public string ScanTitle     { get => _scanTitle; private set => Set(ref _scanTitle, value); }
        public string ScanBelt      { get => _scanBelt;  private set => Set(ref _scanBelt,  value); }
        public string ScanSub       { get => _scanSub;   private set => Set(ref _scanSub,   value); }
        public bool   IsScanning    { get => _isScanning; private set { Set(ref _isScanning, value); ToggleScanCommand.RaiseCanExecuteChanged(); } }

        // ── Recent log ─────────────────────────────────────────
        public ObservableCollection<AttendanceLogItem> RecentLog { get; } = new ObservableCollection<AttendanceLogItem>();

        // ── Commands ───────────────────────────────────────────
        public AsyncRelayCommand ToggleScanCommand       { get; }
        public RelayCommand      ClassViewCommand        { get; }
        public RelayCommand      TagMappingCommand       { get; }
        public RelayCommand      ManualCheckInCommand    { get; }
        public AsyncRelayCommand SyncNowCommand          { get; }
        public RelayCommand      SettingsCommand         { get; }
        public RelayCommand      RfidLogCommand          { get; }
        public RelayCommand      LogoutCommand           { get; }
        public RelayCommand      ToggleLangCommand       { get; }

        // Events for view animations
        public event Action<ScanState>? ScanStateChanged;

        // ── Timers ─────────────────────────────────────────────
        private readonly DispatcherTimer _uiTimer     = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1)  };
        private readonly DispatcherTimer _syncTimer   = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        private readonly DispatcherTimer _onlineTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        private readonly DispatcherTimer _resetTimer  = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4)  };

        public MainViewModel(AppConfig cfg, RfidService rfid, SupabaseService supabase,
                             LocalDbService db, AttendanceService attendance)
        {
            Cfg         = cfg;
            _rfid       = rfid;
            _supabase   = supabase;
            _db         = db;
            _attendance = attendance;

            BranchName  = cfg.BranchName;

            ToggleScanCommand    = new AsyncRelayCommand(ToggleScan);
            ClassViewCommand     = new RelayCommand(() => OpenClassViewRequested?.Invoke());
            TagMappingCommand    = new RelayCommand(() => OpenTagMappingRequested?.Invoke());
            ManualCheckInCommand = new RelayCommand(() => OpenManualCheckInRequested?.Invoke());
            SyncNowCommand       = new AsyncRelayCommand(SyncNow);
            SettingsCommand      = new RelayCommand(() => OpenSettingsRequested?.Invoke());
            RfidLogCommand       = new RelayCommand(() => OpenRfidLogRequested?.Invoke());
            LogoutCommand        = new RelayCommand(Logout);
            ToggleLangCommand    = new RelayCommand(ToggleLang);

            _uiTimer.Tick    += (_, __) => OnUiTick();
            _syncTimer.Tick  += async (_, __) => await SyncNow();
            _onlineTimer.Tick += async (_, __) => await CheckOnlineAsync();
            _resetTimer.Tick += (_, __) => { _resetTimer.Stop(); ResetScan(); };
            _resetTimer.IsEnabled = false;

            _rfid.TagRead       += OnTagRead;
            _rfid.StatusChanged += OnReaderStatus;

            _uiTimer.Start();
            _syncTimer.Start();
            _onlineTimer.Start();

            ResetScan();
            _ = InitAsync();
        }

        // ── Navigation requests (set by View) ─────────────────
        public Action?       OpenClassViewRequested;
        public Action?       OpenTagMappingRequested;
        public Action?       OpenManualCheckInRequested;
        public Action?       OpenSettingsRequested;
        public Action?       OpenRfidLogRequested;
        public Action?       CloseRequested;

        // ── Init ───────────────────────────────────────────────

        private async Task InitAsync()
        {
            var online = await _supabase.SignInAsync();
            CloudOnline = online;
            if (online)
            {
                var students = await _supabase.GetStudentsAsync();
                _db.CacheStudents(students);
                var tags = await _supabase.GetRfidTagsAsync();
                _db.CacheRfidTags(tags);
            }
            if (Cfg.IsReaderConfigured) _rfid.Connect();
            if (Cfg.AutoStartScanning)
            {
                await Task.Delay(2000);
                StartScan();
            }
        }

        // ── Scan ───────────────────────────────────────────────

        private Task ToggleScan()
        {
            if (IsScanning) StopScan(); else StartScan();
            return Task.CompletedTask;
        }

        public void StartScan()
        {
            if (!_rfid.IsConnected) _rfid.Connect();
            _rfid.StartScan();
            IsScanning = true;
        }

        public void StopScan()
        {
            _rfid.StopScan();
            IsScanning = false;
        }

        private async void OnTagRead(object? sender, TagReadEventArgs e)
        {
            var result = await _attendance.ProcessTagAsync(e.Epc);
            Application.Current.Dispatcher.BeginInvoke(new Action(() => ShowResult(result)));
        }

        private void OnReaderStatus(object? sender, string status)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ReaderOnline = status.StartsWith("connected");
                if (!ReaderOnline) { IsScanning = false; }
            }));
        }

        public void ShowResult(CheckInOutcome result)
        {
            _resetTimer.Stop();
            switch (result.Result)
            {
                case CheckInResult.Success:
                    SetScan(ScanState.Success, "✓",
                        result.StudentName ?? "", result.BeltName ?? "",
                        DateTime.Now.ToString("HH:mm:ss"));
                    TodayCount++;
                    if (result.Record != null)
                    {
                        RecentLog.Insert(0, new AttendanceLogItem(result.Record));
                        if (RecentLog.Count > 30) RecentLog.RemoveAt(30);
                    }
                    break;
                case CheckInResult.AlreadyCheckedIn:
                    SetScan(ScanState.Already, "◐",
                        result.StudentName ?? "", "",
                        $"{result.MinutesAgo} {L.AlreadyTime}");
                    break;
                case CheckInResult.TagNotFound:
                    SetScan(ScanState.NotFound, "✗",
                        L.TagNotFound, "", "");
                    break;
            }
            _resetTimer.Start();
        }

        private void SetScan(ScanState state, string icon, string title, string belt, string sub)
        {
            ScanState = state;
            ScanIcon  = icon;
            ScanTitle = title;
            ScanBelt  = belt;
            ScanSub   = sub;
            ScanStateChanged?.Invoke(state);
        }

        private void ResetScan()
        {
            SetScan(ScanState.Idle, "⊙", L.ScanPrompt, "", L.ScanPromptSub);
        }

        // ── Sync / Online ──────────────────────────────────────

        private async Task SyncNow()
        {
            if (!_supabase.IsOnline) return;
            await _attendance.SyncQueueAsync();
            RefreshQueueCount();
        }

        private async Task CheckOnlineAsync()
        {
            await _supabase.CheckOnlineAsync();
            CloudOnline = _supabase.IsOnline;
            RefreshQueueCount();
        }

        // ── UI tick ────────────────────────────────────────────

        private void OnUiTick()
        {
            TimeDisplay = DateTime.Now.ToString("HH:mm");
            CloudOnline = _supabase.IsOnline;
            RefreshQueueCount();
        }

        private void RefreshQueueCount()
        {
            QueueCount = _db.GetUnsyncedCount();
        }

        // ── Misc commands ──────────────────────────────────────

        private void Logout()
        {
            StopScan();
            _rfid.Disconnect();
            CloseRequested?.Invoke();
        }

        private void ToggleLang()
        {
            L.Current     = L.Current == "en" ? "ko" : "en";
            Cfg.Language  = L.Current;
            Cfg.Save();
            var exe = Assembly.GetEntryAssembly()?.Location ?? "";
            if (!string.IsNullOrEmpty(exe)) Process.Start(exe);
            Application.Current.Shutdown();
        }

        public async void OnSettingsSaved()
        {
            BranchName = Cfg.BranchName;
            var online = await _supabase.SignInAsync();
            CloudOnline = online;
            if (online)
            {
                var students = await _supabase.GetStudentsAsync();
                _db.CacheStudents(students);
                var tags = await _supabase.GetRfidTagsAsync();
                _db.CacheRfidTags(tags);
            }
        }

        public void Cleanup()
        {
            _uiTimer.Stop(); _syncTimer.Stop(); _onlineTimer.Stop(); _resetTimer.Stop();
            _rfid.TagRead       -= OnTagRead;
            _rfid.StatusChanged -= OnReaderStatus;
        }
    }
}
