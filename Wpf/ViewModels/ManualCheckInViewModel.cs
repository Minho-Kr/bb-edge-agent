using System.Collections.ObjectModel;
using System.Threading.Tasks;
using BBEdgeAgent.Models;
using BBEdgeAgent.Services;

namespace BBEdgeAgent.Wpf.ViewModels
{
    public class ManualCheckInViewModel : ViewModelBase
    {
        private readonly LocalDbService    _db;
        private readonly AttendanceService _attendance;

        private string   _searchText   = "";
        private Student? _selectedStudent;
        private string   _statusMsg    = "";
        private bool     _statusOk;

        public ObservableCollection<Student> Students { get; } = new();

        public string   SearchText
        {
            get => _searchText;
            set { Set(ref _searchText, value); FilterStudents(); }
        }

        public Student? SelectedStudent
        {
            get => _selectedStudent;
            set => Set(ref _selectedStudent, value);
        }

        public string StatusMsg { get => _statusMsg; set => Set(ref _statusMsg, value); }
        public bool   StatusOk  { get => _statusOk;  set => Set(ref _statusOk,  value); }

        public AsyncRelayCommand ConfirmCommand { get; }
        public RelayCommand      CancelCommand  { get; }

        public System.Action<CheckInOutcome>? CheckInCompleted;
        public System.Action?                 CloseRequested;

        private System.Collections.Generic.List<Student> _allStudents = new();

        public ManualCheckInViewModel(LocalDbService db, AttendanceService attendance)
        {
            _db         = db;
            _attendance = attendance;

            ConfirmCommand = new AsyncRelayCommand(Confirm);
            CancelCommand  = new RelayCommand(() => CloseRequested?.Invoke());

            LoadStudents();
        }

        private void LoadStudents()
        {
            _allStudents = _db.GetCachedStudents();
            FilterStudents();
        }

        private void FilterStudents()
        {
            var q = _searchText.Trim().ToLower();
            Students.Clear();
            foreach (var s in _allStudents)
                if (string.IsNullOrEmpty(q) || s.FullName.ToLower().Contains(q))
                    Students.Add(s);
        }

        private async Task Confirm()
        {
            if (SelectedStudent == null) { StatusMsg = L.SelectStudent; StatusOk = false; return; }
            var result = await _attendance.ManualCheckInAsync(SelectedStudent);
            if (result.Result == CheckInResult.Success)
            {
                StatusMsg = L.ManualOk; StatusOk = true;
                await Task.Delay(600);
                CheckInCompleted?.Invoke(result);
                CloseRequested?.Invoke();
            }
            else if (result.Result == CheckInResult.AlreadyCheckedIn)
            {
                StatusMsg = $"{L.AlreadyCheckedIn} ({result.MinutesAgo} {L.AlreadyTime})";
                StatusOk  = false;
            }
        }
    }
}
