using System.Windows;
using System.Windows.Input;

namespace TNovBIMUtils
{
    public partial class TDiamExceptionsWPF : Window
    {
        public TDiamExceptionsWPF(TDiamExceptionsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var vm = DataContext as TDiamExceptionsViewModel;
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
