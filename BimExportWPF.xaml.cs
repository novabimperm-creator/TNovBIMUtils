using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using TNovCommon;

namespace TNovBIMUtils
{
    /// <summary>
    /// Логика взаимодействия для BimExportWPF.xaml
    /// </summary>
    public partial class BimExportWPF : Window
    {
        public string initialDirectory = "C:\\";
        public string RSPath = "";
        List<string> links0 = new List<string>();
        public BimExportWPF(BimExportViewModel viewModel,List<string>linksString)
        {
            InitializeComponent();
            textBox1.Focus();
            DataContext = viewModel;
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
            rvt.IsChecked = true;
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
            rs.IsChecked = true;
            
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

        private void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            string commandText = @"https://portal.talan.group/knowledge/proektirovanie/eksportmodeleyvnavisworks/";
            var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = commandText;
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
        }
    }
}
