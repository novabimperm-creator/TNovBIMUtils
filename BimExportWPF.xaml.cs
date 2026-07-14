using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TNovCommon;

namespace TNovBIMUtils
{
    /// <summary>
    /// Логика взаимодействия для BimExportWPF.xaml
    /// </summary>
    public partial class BimExportWPF : Window
    {
        private const double BaseWidth = 800;
        private const double ExtraPanelWidth = 260;

        public string initialDirectory = "C:\\";
        public string RSPath = "";
        List<string> links0 = new List<string>();
        private List<ProjectsBoxItem> _projects = new List<ProjectsBoxItem>();
        public BimExportWPF(BimExportViewModel viewModel,List<string>linksString)
        {
            InitializeComponent();

            //заполнение combobox
            _projects.Add(new ProjectsBoxItem
            {
                Key = "Не выбран",
                Value = "Не выбран"
            });

            string[] CDElines = File.ReadAllLines(@"\\fs-nova\Distr\0.For Admin\_TNov\CDE.txt");
            
            foreach (string line in CDElines)
            {
                string[] pParts = line.Split(',');
                string nwcPath = line.Replace(pParts[0] + ",", "").Replace("," + pParts[pParts.Length - 1], "");
                _projects.Add(new ProjectsBoxItem
                {
                    Key = pParts[0].Trim(),
                    Value = nwcPath.Trim()
                });
            }
            NWCbox.ItemsSource = _projects;
            NWCbox.SelectedIndex = 0;


            textBox1.Focus();
            DataContext = viewModel;
            initialDirectory = viewModel.folder;
            foreach(string link in linksString) links0.Add(link);
        }

        private void acceptButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            this.Close(); // закрытие окна
        }

        private void escButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close(); // закрытие окна
        }

        private void browseButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is BimExportViewModel vm)
                vm.fromRVT = true;

            var dialog = new FolderSelectDialog
            {
                InitialDirectory = initialDirectory,
                Title = "Выберите папку"
            };
            if (dialog.Show())
            {
                textBox1.Text = dialog.FileName; textBox1.Focus(); initialDirectory = dialog.FileName;
            }
        }
        private void browseButtonRS_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is BimExportViewModel vm)
                vm.fromRS = true;
        }
        
        private void browseButton2_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderSelectDialog
            {
                InitialDirectory = initialDirectory,
                Title = "Выберите папку"
            };
            if (dialog.Show())
            {
                textBox2.Text = dialog.FileName; textBox2.Focus(); initialDirectory = dialog.FileName;
            }
        }

        private void browseButton3_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderSelectDialog
            {
                InitialDirectory = initialDirectory,
                Title = "Выберите папку"
            };
            if (dialog.Show())
            {
                textBox3.Text = dialog.FileName; textBox3.Focus(); initialDirectory = dialog.FileName;
            }
        }

        private void rvtExportButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", @"C:\Program Files\Autodesk\Revit 2022\RevitServerToolCommand\");
        }

        private void textBox1_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            string commandText = HelpLinks.GetHelpLink("BIM Экспорт");
            var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = commandText;
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void ExtraSettingsToggle_Checked(object sender, RoutedEventArgs e)
        {
            ExtraColumn.Width = new GridLength(ExtraPanelWidth);
            ExtraSettingsPanel.Visibility = System.Windows.Visibility.Visible;
            MinWidth = BaseWidth + ExtraPanelWidth;
            Width = BaseWidth + ExtraPanelWidth;
            extSettings_text.Text = "< Дополнительные настройки";
        }

        private void ExtraSettingsToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            ExtraColumn.Width = new GridLength(0);
            ExtraSettingsPanel.Visibility = System.Windows.Visibility.Collapsed;
            MinWidth = BaseWidth;
            Width = BaseWidth;
            extSettings_text.Text = "Дополнительные настройки >";
        }

        private void cdeButton_Click(object sender, RoutedEventArgs e)
        {
            string userName = UserNameHelper.GetCurrentUserName(true);
            TNovConfig config = TNovConfigLoad.LoadConfig();
            string userDepartment = "";
            string[] rolesFile = File.ReadAllLines($"{config.ServerPath}roles.txt");
            foreach (string role in rolesFile)
            {
                if (role.Contains(userName))
                {
                    string[] line = role.Split(',');
                    userDepartment = line[1];
                    break;
                }
            }
            switch (userDepartment)
            {
                case "BIM":
                    CdeWPF wpfView = new CdeWPF();
                    wpfView.Owner = this;
                    wpfView.ShowDialog();
                    break;
                default: 
                    new InfoWindow280("Только BIM-специалист может изменять эти данные.").ShowDialog();
                    break;
            }
            
        }

        private void NWCbox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateTextBox();
        }
        private void UpdateTextBox()
        {
            
            if (NWCbox.SelectedItem is ProjectsBoxItem selectedItem)
            {
                textBox2.Text = selectedItem.Value;
                initialDirectory = selectedItem.Value;
            }
            else
            {
                textBox2.Text = "Не выбран";
            }
            
        }

        private void autoButton_Click(object sender, RoutedEventArgs e)
        {
            TNovConfig config = TNovConfigLoad.LoadConfig();
            if (File.Exists(config.ServerPath + "NwcExport.log"))
            {
                string autoJournal = File.ReadAllText(config.ServerPath + "NwcExport.log");
                new InfoWindow400(autoJournal).ShowDialog();
            }
            else new InfoWindow280("Отсутствует файл логов автоматического экспорта.").ShowDialog();
        }

        private void textBox2_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void AR_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
    class ProjectsBoxItem
    {
        public string Key { get; set; }
        public string Value { get; set; }

        // Для отображения в ComboBox будет использоваться ключ
        public override string ToString()
        {
            return Key;
        }
    }
}
