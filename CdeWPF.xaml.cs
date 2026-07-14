using System.Windows;
using System.Windows.Input;
using TNovCommon;

namespace TNovBIMUtils
{
    public partial class CdeWPF : Window
    {
        public CdeWPF()
        {
            InitializeComponent();

            TNovConfig config = TNovConfigLoad.LoadConfig();
            string filePath = config.ServerPath + "CDE.txt";
            DataContext = new CdeViewModel(filePath);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var vm = DataContext as CdeViewModel;
            if (vm != null && !vm.SaveIfDirty())
                e.Cancel = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
