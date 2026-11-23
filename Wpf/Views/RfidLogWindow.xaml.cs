using System.Windows;
using BBEdgeAgent.Services;
using BBEdgeAgent.Wpf.ViewModels;

namespace BBEdgeAgent.Wpf.Views
{
    public partial class RfidLogWindow : Window
    {
        public RfidLogWindow(SupabaseService supabase)
        {
            var vm = new RfidLogViewModel(supabase);
            InitializeComponent();
            DataContext = vm;
            vm.CloseRequested = () => Close();
        }
    }
}
