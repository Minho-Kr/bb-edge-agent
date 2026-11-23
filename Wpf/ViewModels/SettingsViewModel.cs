using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using BBEdgeAgent.Services;

namespace BBEdgeAgent.Wpf.ViewModels
{
    public class BranchItem
    {
        public string Id   { get; }
        public string Name { get; }
        public BranchItem(string id, string name) { Id = id; Name = name; }
        public override string ToString() => Name.Length > 0
            ? $"{Name}  ({Id.Substring(0, Math.Min(8, Id.Length))}…)"
            : Id;
    }

    public class SettingsViewModel : ViewModelBase
    {
        private readonly AppConfig       _cfg;
        private readonly RfidService     _rfid;
        private readonly SupabaseService _supabase;

        // ── Reader ─────────────────────────────────────────────
        private string _readerIp   = "";
        private string _readerPort = "";
        private string _readerMsg  = "";
        private bool   _readerOk;

        public string ReaderIp   { get => _readerIp;   set => Set(ref _readerIp,   value); }
        public string ReaderPort { get => _readerPort; set => Set(ref _readerPort, value); }
        public string ReaderMsg  { get => _readerMsg;  set => Set(ref _readerMsg,  value); }
        public bool   ReaderOk   { get => _readerOk;   set => Set(ref _readerOk,   value); }

        // ── Supabase ───────────────────────────────────────────
        private string _supabaseUrl  = "";
        private string _supabaseKey  = "";
        private string _edgeEmail    = "";
        private string _branchId     = "";
        private string _branchName   = "";
        private string _supMsg       = "";
        private bool   _supOk;
        private bool   _loadingBranches;
        private BranchItem? _selectedBranch;

        public string      SupabaseUrl    { get => _supabaseUrl;  set => Set(ref _supabaseUrl,  value); }
        public string      SupabaseKey    { get => _supabaseKey;  set => Set(ref _supabaseKey,  value); }
        public string      EdgeEmail      { get => _edgeEmail;    set => Set(ref _edgeEmail,    value); }
        public string      BranchId      { get => _branchId;     set => Set(ref _branchId,     value); }
        public string      BranchName    { get => _branchName;   set => Set(ref _branchName,   value); }
        public string      SupMsg        { get => _supMsg;        set => Set(ref _supMsg,        value); }
        public bool        SupOk         { get => _supOk;         set => Set(ref _supOk,         value); }
        public bool        LoadingBranches { get => _loadingBranches; set => Set(ref _loadingBranches, value); }
        public ObservableCollection<BranchItem> Branches { get; } = new();

        public BranchItem? SelectedBranch
        {
            get => _selectedBranch;
            set
            {
                Set(ref _selectedBranch, value);
                if (value != null) { BranchId = value.Id; BranchName = value.Name; }
            }
        }

        // Edge password is handled via PasswordBox binding in code-behind
        public string EdgePassword { get; set; } = "";

        // ── Attendance ─────────────────────────────────────────
        private string _debounce      = "3";
        private bool   _autoStart     = true;

        public string Debounce   { get => _debounce;   set => Set(ref _debounce,   value); }
        public bool   AutoStart  { get => _autoStart;  set => Set(ref _autoStart,  value); }

        // ── Security ───────────────────────────────────────────
        public string NewPin     { get; set; } = "";
        public string ConfirmPin { get; set; } = "";
        private string _pinMsg = "";
        private bool   _pinOk;

        public string PinMsg { get => _pinMsg; set => Set(ref _pinMsg, value); }
        public bool   PinOk  { get => _pinOk;  set => Set(ref _pinOk,  value); }

        // ── Commands ───────────────────────────────────────────
        public AsyncRelayCommand TestReaderCommand    { get; }
        public AsyncRelayCommand TestSupabaseCommand  { get; }
        public AsyncRelayCommand LoadBranchesCommand  { get; }
        public RelayCommand      SaveCommand          { get; }
        public RelayCommand      CancelCommand        { get; }
        public RelayCommand      ChangePinCommand     { get; }

        public Action? CloseRequested;
        public Action? SavedSuccessfully;

        public SettingsViewModel(AppConfig cfg, RfidService rfid, SupabaseService supabase)
        {
            _cfg      = cfg;
            _rfid     = rfid;
            _supabase = supabase;

            TestReaderCommand   = new AsyncRelayCommand(TestReader);
            TestSupabaseCommand = new AsyncRelayCommand(TestSupabase);
            LoadBranchesCommand = new AsyncRelayCommand(LoadBranches);
            SaveCommand         = new RelayCommand(Save);
            CancelCommand       = new RelayCommand(() => CloseRequested?.Invoke());
            ChangePinCommand    = new RelayCommand(ChangePin);

            LoadValues();
        }

        private void LoadValues()
        {
            ReaderIp   = _cfg.ReaderIp;
            ReaderPort = _cfg.ReaderPort.ToString();
            SupabaseUrl  = _cfg.SupabaseUrl;
            SupabaseKey  = _cfg.SupabaseKey;
            EdgeEmail    = _cfg.EdgeEmail;
            EdgePassword = _cfg.EdgePassword;
            BranchId     = _cfg.BranchId;
            BranchName   = _cfg.BranchName;
            Debounce     = _cfg.DebounceMinutes.ToString();
            AutoStart    = _cfg.AutoStartScanning;
        }

        private async Task TestReader()
        {
            ReaderMsg = L.Connecting; ReaderOk = false;
            _rfid.UpdateConfig(new AppConfig
            {
                ReaderIp   = ReaderIp.Trim(),
                ReaderPort = int.TryParse(ReaderPort, out var p) ? p : 49152,
            });
            _rfid.Connect();
            await Task.Delay(2500);
            ReaderOk  = _rfid.IsConnected;
            ReaderMsg = ReaderOk ? "✓  " + L.ConnOk : "✗  " + L.ConnFail;
        }

        private async Task TestSupabase()
        {
            SupMsg = L.Connecting; SupOk = false;
            ApplyTempConfig();
            SupOk  = await _supabase.SignInAsync();
            SupMsg = SupOk ? "✓  " + L.ConnOk : "✗  " + L.ConnFail;
        }

        private async Task LoadBranches()
        {
            SupMsg = "Loading branches..."; SupOk = false;
            LoadingBranches = true;
            ApplyTempConfig();
            await _supabase.SignInAsync();
            var list = await _supabase.GetBranchesAsync();
            Branches.Clear();
            foreach (var (id, name) in list) Branches.Add(new BranchItem(id, name));
            LoadingBranches = false;
            if (list.Count > 0)
            {
                SupMsg = $"✓  {list.Count} branch(es) found";
                SupOk  = true;
                SelectedBranch = Branches[0];
            }
            else
            {
                SupMsg = "No branches found. Use the service role key (Supabase Dashboard → Settings → API).";
                SupOk  = false;
            }
        }

        private void ApplyTempConfig()
        {
            _supabase.UpdateConfig(new AppConfig
            {
                SupabaseUrl  = SupabaseUrl.TrimEnd('/'),
                SupabaseKey  = SupabaseKey.Trim(),
                EdgeEmail    = EdgeEmail.Trim(),
                EdgePassword = EdgePassword,
                BranchId     = BranchId.Trim().Length > 0 ? BranchId.Trim() : "00000000-0000-0000-0000-000000000000",
            });
        }

        private void Save()
        {
            _cfg.ReaderIp         = ReaderIp.Trim();
            _cfg.ReaderPort       = int.TryParse(ReaderPort, out var p) ? p : 49152;
            _cfg.SupabaseUrl      = SupabaseUrl.TrimEnd('/');
            _cfg.SupabaseKey      = SupabaseKey.Trim();
            _cfg.EdgeEmail        = EdgeEmail.Trim();
            _cfg.EdgePassword     = EdgePassword;
            _cfg.BranchId         = BranchId.Trim();
            _cfg.BranchName       = BranchName.Trim();
            _cfg.DebounceMinutes  = int.TryParse(Debounce, out var d) ? Math.Max(1, Math.Min(30, d)) : 3;
            _cfg.AutoStartScanning = AutoStart;
            _cfg.Save();
            _rfid.UpdateConfig(_cfg);
            _supabase.UpdateConfig(_cfg);
            SavedSuccessfully?.Invoke();
            CloseRequested?.Invoke();
        }

        private void ChangePin()
        {
            if (NewPin.Length != 4)     { PinMsg = "PIN must be 4 digits."; PinOk = false; return; }
            if (NewPin != ConfirmPin)   { PinMsg = L.PinMismatch; PinOk = false; return; }
            _cfg.SetPin(NewPin);
            _cfg.Save();
            PinMsg = L.PinChanged; PinOk = true;
            NewPin = ConfirmPin = "";
            Notify(nameof(NewPin)); Notify(nameof(ConfirmPin));
        }
    }
}
