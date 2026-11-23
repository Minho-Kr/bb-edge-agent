using System.Windows;
using BBEdgeAgent.Services;
using BBEdgeAgent.Wpf.ViewModels;

namespace BBEdgeAgent.Wpf.Views
{
    public partial class ClassViewWindow : Window
    {
        public ClassViewWindow(AppConfig cfg, SupabaseService supabase)
        {
            var vm = new ClassViewViewModel(supabase, cfg);
            InitializeComponent();
            DataContext = vm;
            vm.CloseRequested = () => Close();
        }
    }
}
