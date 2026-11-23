using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using BBEdgeAgent.Models;
using BBEdgeAgent.Services;

namespace BBEdgeAgent.Wpf.ViewModels
{
    public class RfidLogViewModel : ViewModelBase
    {
        private readonly SupabaseService _supabase;
        private int    _rangeIndex = 0; // 0=Today,1=7 Days,2=30 Days
        private bool   _loading;
        private string _statusMsg = "";
        private bool   _statusOk;

        public ObservableCollection<RfidLogEntry> Logs { get; } = new ObservableCollection<RfidLogEntry>();

        public bool   Loading    { get => _loading;    set => Set(ref _loading,    value); }
        public string StatusMsg  { get => _statusMsg;  set => Set(ref _statusMsg,  value); }
        public bool   StatusOk   { get => _statusOk;   set => Set(ref _statusOk,   value); }
        public int    RangeIndex
        {
            get => _rangeIndex;
            set { Set(ref _rangeIndex, value); _ = LoadAsync(); }
        }

        public AsyncRelayCommand RefreshCommand { get; }
        public RelayCommand      CloseCommand   { get; }
        public Action?           CloseRequested;

        public RfidLogViewModel(SupabaseService supabase)
        {
            _supabase      = supabase;
            RefreshCommand = new AsyncRelayCommand(LoadAsync);
            CloseCommand   = new RelayCommand(() => CloseRequested?.Invoke());
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            Loading = true; StatusMsg = "Loading..."; StatusOk = false;
            Logs.Clear();

            if (!_supabase.IsOnline) await _supabase.SignInAsync();
            if (!_supabase.IsOnline)
            {
                StatusMsg = "Offline — cannot load RFID logs.";
                Loading   = false;
                return;
            }

            var now  = DateTime.UtcNow;
            var from = RangeIndex switch
            {
                1 => now.AddDays(-7),
                2 => now.AddDays(-30),
                _ => now.Date,
            };

            var list = await _supabase.GetRfidLogsAsync(from, now.AddDays(1));
            foreach (var entry in list) Logs.Add(entry);
            StatusMsg = $"✓  {Logs.Count} records";
            StatusOk  = true;
            Loading   = false;
        }
    }
}
