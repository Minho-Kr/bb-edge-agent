using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using BBEdgeAgent.Models;
using BBEdgeAgent.Services;

namespace BBEdgeAgent.Wpf.ViewModels
{
    public class TagItem
    {
        public string TagId     { get; }
        public string TagUid    { get; }
        public string Note      { get; }
        public string Assigned  { get; }

        public TagItem(string tagId, string tagUid, string note, DateTime assignedAt)
        {
            TagId    = tagId;
            TagUid   = tagUid;
            Note     = note;
            Assigned = assignedAt.ToLocalTime().ToString("yyyy-MM-dd");
        }
    }

    public class TagMappingViewModel : ViewModelBase
    {
        private readonly RfidService     _rfid;
        private readonly SupabaseService _supabase;
        private readonly LocalDbService  _db;
        private readonly AppConfig       _cfg;

        // ── Students ───────────────────────────────────────────
        private string  _searchText = "";
        private Student? _selectedStudent;
        private string  _statusMsg  = "";
        private bool    _statusOk;
        private bool    _scanMode;

        public ObservableCollection<Student>  Students { get; } = new();
        public ObservableCollection<TagItem>  Tags     { get; } = new();

        public string   SearchText
        {
            get => _searchText;
            set { Set(ref _searchText, value); FilterStudents(); }
        }

        public Student? SelectedStudent
        {
            get => _selectedStudent;
            set { Set(ref _selectedStudent, value); _ = LoadTagsAsync(); }
        }

        public string   StatusMsg   { get => _statusMsg;  set => Set(ref _statusMsg,  value); }
        public bool     StatusOk    { get => _statusOk;   set => Set(ref _statusOk,   value); }
        public bool     ScanMode    { get => _scanMode;   set => Set(ref _scanMode,   value); }

        public TagItem? SelectedTag { get; set; }

        // ── Commands ───────────────────────────────────────────
        public AsyncRelayCommand RefreshCommand    { get; }
        public AsyncRelayCommand EnterScanCommand  { get; }
        public AsyncRelayCommand RemoveTagCommand  { get; }
        public RelayCommand      CloseCommand      { get; }

        public Action? CloseRequested;

        private System.Collections.Generic.List<Student> _allStudents = new();

        public TagMappingViewModel(RfidService rfid, SupabaseService supabase,
                                   LocalDbService db, AppConfig cfg)
        {
            _rfid     = rfid;
            _supabase = supabase;
            _db       = db;
            _cfg      = cfg;

            RefreshCommand   = new AsyncRelayCommand(RefreshAsync);
            EnterScanCommand = new AsyncRelayCommand(EnterScanModeAsync);
            RemoveTagCommand = new AsyncRelayCommand(RemoveTagAsync);
            CloseCommand     = new RelayCommand(() => CloseRequested?.Invoke());

            _ = RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            StatusMsg = L.LoadingStudents; StatusOk = false;
            _allStudents.Clear();

            if (!_supabase.IsOnline)
                await _supabase.SignInAsync();

            if (_supabase.IsOnline)
            {
                var list = await _supabase.GetStudentsAsync();
                _db.CacheStudents(list);
                _allStudents.AddRange(list);
                if (list.Count == 0)
                {
                    var bid = _cfg.BranchId;
                    StatusMsg = string.IsNullOrEmpty(bid)
                        ? "No Branch ID set — please configure Settings."
                        : "No students found. Check that a Service Role Key is used in Settings (Settings → Supabase).";
                }
            }
            else
            {
                var cached = _db.GetCachedStudents();
                _allStudents.AddRange(cached);
                StatusMsg = cached.Count == 0
                    ? "Offline & no cache. Open Settings → Supabase and save with a valid Service Role Key."
                    : "";
            }

            FilterStudents();
            if (_allStudents.Count > 0) StatusMsg = "";
        }

        private void FilterStudents()
        {
            var q = _searchText.Trim().ToLower();
            Students.Clear();
            foreach (var s in _allStudents)
                if (string.IsNullOrEmpty(q) || s.FullName.ToLower().Contains(q))
                    Students.Add(s);
        }

        private async Task LoadTagsAsync()
        {
            Tags.Clear();
            if (SelectedStudent == null || !_supabase.IsOnline) return;
            var list = await _supabase.GetTagsForStudentAsync(SelectedStudent.Id);
            foreach (var (id, uid, note, at) in list)
                Tags.Add(new TagItem(id, uid, note, at));
        }

        private async Task EnterScanModeAsync()
        {
            if (SelectedStudent == null) { StatusMsg = L.SelectStudent; StatusOk = false; return; }
            StatusMsg = L.ScanWaiting; StatusOk = false;
            ScanMode  = true;
            _rfid.TagRead += OnTagRead;
        }

        private async void OnTagRead(object? sender, TagReadEventArgs e)
        {
            _rfid.TagRead -= OnTagRead;
            ScanMode = false;

            if (SelectedStudent == null) return;
            var ok = await _supabase.AssignTagAsync(e.Epc, SelectedStudent.Id);
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
            {
                if (ok)
                {
                    StatusMsg = L.TagAssignOk; StatusOk = true;
                    _db.CacheRfidTags(await _supabase.GetRfidTagsAsync());
                    await LoadTagsAsync();
                }
                else
                {
                    StatusMsg = "Failed to assign tag."; StatusOk = false;
                }
            }));
        }

        private async Task RemoveTagAsync()
        {
            if (SelectedTag == null) { StatusMsg = L.SelectTag; StatusOk = false; return; }
            var ok = await _supabase.DeactivateTagAsync(SelectedTag.TagId);
            if (ok)
            {
                StatusMsg = L.TagRemoveOk; StatusOk = true;
                _db.CacheRfidTags(await _supabase.GetRfidTagsAsync());
                await LoadTagsAsync();
            }
            else
            {
                StatusMsg = "Failed to remove tag."; StatusOk = false;
            }
        }
    }
}
