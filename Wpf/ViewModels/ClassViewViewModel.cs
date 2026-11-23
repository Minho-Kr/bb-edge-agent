using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BBEdgeAgent.Models;
using BBEdgeAgent.Services;

namespace BBEdgeAgent.Wpf.ViewModels
{
    // Hardcoded class schedule (matches class_schedules table seed data)
    public class ClassSlot : ViewModelBase
    {
        private int _attendedCount;

        public string ClassType      { get; }
        public string ClassName      { get; }
        public string StartTime      { get; }
        public int    TotalCount     { get; set; }
        public int    AttendedCount  { get => _attendedCount; set { Set(ref _attendedCount, value); Notify(nameof(Summary)); } }
        public string Summary        => $"{AttendedCount} / {TotalCount}";

        public ClassSlot(string classType, string className, string startTime)
        {
            ClassType = classType; ClassName = className; StartTime = startTime;
        }
    }

    public class ClassStudentRow : ViewModelBase
    {
        private bool   _attended;
        private string _attendedTime = "";

        public string Id          { get; }
        public string Name        { get; }
        public string BeltName    { get; }
        public bool   Attended    { get => _attended;     set { Set(ref _attended,     value); Notify(nameof(CanCheckIn)); Notify(nameof(StatusText)); } }
        public string AttendedTime{ get => _attendedTime; set { Set(ref _attendedTime, value); Notify(nameof(StatusText)); } }
        public string StatusText  => Attended ? $"✓  {AttendedTime}" : "";
        public bool   CanCheckIn  => !Attended;

        public ClassStudentRow(string id, string name, string beltName)
        { Id = id; Name = name; BeltName = beltName; }
    }

    public class ClassViewViewModel : ViewModelBase
    {
        private readonly SupabaseService _supabase;
        private readonly AppConfig       _cfg;

        // Weekday schedule (Mon–Fri)
        private static readonly (string type, string name, string time)[] WeekdayClasses =
        {
            ("little_tiger",  "Little Tiger",  "4:10 PM"),
            ("beginner",      "Beginner",       "5:50 PM"),
            ("intermediate",  "Intermediate",   "6:45 PM"),
            ("advanced",      "Advanced",       "6:45 PM"),
            ("black_belt",    "Black Belt",     "7:30 PM"),
        };
        // Saturday schedule
        private static readonly (string type, string name, string time)[] SaturdayClasses =
        {
            ("little_tiger",  "Little Tiger",  "9:00 AM"),
            ("beginner",      "Beginner",      "10:00 AM"),
            ("intermediate",  "All Levels",    "11:00 AM"),
        };

        private ClassSlot? _selectedClass;
        private bool       _loading;
        private string     _statusMsg = "";
        private bool       _statusOk;

        public ObservableCollection<ClassSlot>       Classes  { get; } = new ObservableCollection<ClassSlot>();
        public ObservableCollection<ClassStudentRow> Students { get; } = new ObservableCollection<ClassStudentRow>();

        public ClassSlot? SelectedClass
        {
            get => _selectedClass;
            set { Set(ref _selectedClass, value); Notify(nameof(HasSelectedClass)); if (value != null) PopulateStudents(value.ClassType); }
        }

        public bool HasSelectedClass => _selectedClass != null;
        public bool   Loading   { get => _loading;   set => Set(ref _loading,   value); }
        public string StatusMsg { get => _statusMsg; set => Set(ref _statusMsg, value); }
        public bool   StatusOk  { get => _statusOk;  set => Set(ref _statusOk,  value); }
        public string TodayHeader => DateTime.Now.ToString("dddd, MMMM d", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

        public AsyncRelayCommand             RefreshCommand { get; }
        public RelayCommand<ClassStudentRow> CheckInCommand { get; }
        public RelayCommand                  CloseCommand   { get; }
        public Action?                       CloseRequested;

        private Dictionary<string, DateTime> _todayAtt    = new Dictionary<string, DateTime>();
        private List<Student>                _allStudents = new List<Student>();

        public ClassViewViewModel(SupabaseService supabase, AppConfig cfg)
        {
            _supabase = supabase;
            _cfg      = cfg;

            RefreshCommand = new AsyncRelayCommand(LoadAsync);
            CheckInCommand = new RelayCommand<ClassStudentRow>(row => { if (row != null) _ = CheckInAsync(row); });
            CloseCommand   = new RelayCommand(() => CloseRequested?.Invoke());

            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            Loading = true; StatusMsg = "Loading..."; StatusOk = false;
            Students.Clear(); Classes.Clear();

            if (!_supabase.IsOnline) await _supabase.SignInAsync();
            if (!_supabase.IsOnline)
            {
                StatusMsg = "Offline — cannot load class data.";
                Loading = false;
                return;
            }

            _todayAtt    = await _supabase.GetTodayAttendanceAsync();
            _allStudents = await _supabase.GetStudentsAsync();

            int dow = (int)DateTime.Now.DayOfWeek; // 0=Sun,1=Mon,...,6=Sat
            var schedule = dow == 6 ? SaturdayClasses : WeekdayClasses;

            foreach (var (type, name, time) in schedule)
            {
                // 오늘 요일 수업 학생만 (class_days 비어있으면 모든 요일)
                var studs = _allStudents.Where(s =>
                    s.ClassType == type &&
                    (s.ClassDays.Count == 0 || s.ClassDays.Contains(dow))
                ).ToList();
                if (studs.Count == 0) continue;
                var slot           = new ClassSlot(type, name, time);
                slot.TotalCount    = studs.Count;
                slot.AttendedCount = studs.Count(s => _todayAtt.ContainsKey(s.Id));
                Classes.Add(slot);
            }

            StatusMsg = Classes.Count == 0 ? "No students scheduled today." : $"✓  Loaded {_allStudents.Count} students";
            StatusOk  = Classes.Count > 0;
            Loading   = false;

            if (Classes.Count > 0) SelectedClass = Classes[0];
        }

        private void PopulateStudents(string classType)
        {
            Students.Clear();
            int dow = (int)DateTime.Now.DayOfWeek;
            var studs = _allStudents
                .Where(s =>
                    (s.ClassType == classType || (classType == "intermediate" && s.ClassType == "advanced")) &&
                    (s.ClassDays.Count == 0 || s.ClassDays.Contains(dow)))
                .OrderBy(s => _todayAtt.ContainsKey(s.Id) ? 0 : 1)
                .ThenBy(s => s.LastName)
                .ToList();

            foreach (var s in studs)
            {
                var row = new ClassStudentRow(s.Id, $"{s.FirstName} {s.LastName}", s.BeltName);
                if (_todayAtt.TryGetValue(s.Id, out var at))
                {
                    row.Attended     = true;
                    row.AttendedTime = at.ToString("HH:mm");
                }
                Students.Add(row);
            }
        }

        private async Task CheckInAsync(ClassStudentRow row)
        {
            if (row.Attended || !_supabase.IsOnline) return;
            var now = DateTime.UtcNow;
            var rec = new AttendanceRecord
            {
                Id         = Guid.NewGuid().ToString(),
                StudentId  = row.Id,
                BranchId   = _cfg.BranchId,
                AttendedAt = now,
                Source     = "manual",
            };
            var ok = await _supabase.PostAttendanceAsync(rec);
            if (ok)
            {
                var local         = now.ToLocalTime();
                _todayAtt[row.Id] = local;
                row.Attended      = true;
                row.AttendedTime  = local.ToString("HH:mm");
                if (SelectedClass != null) SelectedClass.AttendedCount++;
                StatusMsg = $"✓  {row.Name}  checked in at {local:HH:mm}";
                StatusOk  = true;
            }
            else { StatusMsg = $"Failed to check in {row.Name}."; StatusOk = false; }
        }
    }
}
