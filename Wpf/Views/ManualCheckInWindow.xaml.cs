using System.Windows;
using BBEdgeAgent.Services;
using BBEdgeAgent.Wpf.ViewModels;

namespace BBEdgeAgent.Wpf.Views
{
    public partial class ManualCheckInWindow : Window
    {
        private readonly ManualCheckInViewModel _vm;

        public ManualCheckInWindow(LocalDbService db, AttendanceService attendance, MainViewModel mainVm)
        {
            _vm = new ManualCheckInViewModel(db, attendance);
            InitializeComponent();
            DataContext = _vm;

            _vm.CloseRequested    = () => Close();
            _vm.CheckInCompleted  = result => mainVm.ShowResult(result);
        }
    }
}
