using System.Windows;
using System.Windows.Controls;
using BBEdgeAgent.Services;
using BBEdgeAgent.Wpf.ViewModels;

namespace BBEdgeAgent.Wpf.Views
{
    public partial class TagMappingWindow : Window
    {
        private readonly TagMappingViewModel _vm;

        public TagMappingWindow(AppConfig cfg, RfidService rfid, SupabaseService supabase, LocalDbService db)
        {
            _vm = new TagMappingViewModel(rfid, supabase, db, cfg);
            InitializeComponent();
            DataContext = _vm;

            _vm.CloseRequested = () => Close();
            TagList.SelectionChanged += (_, __) => _vm.SelectedTag =
                TagList.SelectedItem as BBEdgeAgent.Wpf.ViewModels.TagItem;
        }
    }
}
