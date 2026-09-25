using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TNovBIMUtils
{
    /// <summary>Ввод номера секции. DialogResult = true только при непустом значении.</summary>
    public partial class SectionNumberWPF : Window
    {
        public string Value => (valueBox.Text ?? "").Trim();

        public SectionNumberWPF(string initialValue)
        {
            InitializeComponent();
            valueBox.Text = initialValue ?? "";
            Loaded += (s, e) => { valueBox.Focus(); valueBox.SelectAll(); };
        }

        private void valueBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            acceptButton.IsEnabled = Value.Length > 0;
        }

        private void acceptButton_Click(object sender, RoutedEventArgs e)
        {
            if (Value.Length == 0) return;
            DialogResult = true;
            Close();
        }

        private void escButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
