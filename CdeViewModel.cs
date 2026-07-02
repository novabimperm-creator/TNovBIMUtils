using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TNovCommon;

namespace TNovBIMUtils
{
    public class CdeViewModel : INotifyPropertyChanged
    {
        private readonly string _filePath;
        private ObservableCollection<CdeEntry> _entries;
        private bool _isDirty;

        public ObservableCollection<CdeEntry> Entries
        {
            get => _entries;
            set { _entries = value; OnPropertyChanged(); }
        }

        public bool IsDirty
        {
            get => _isDirty;
            set { _isDirty = value; OnPropertyChanged(); }
        }

        // Список доступных статусов для ComboBox
        public string[] Statuses { get; } = { "active", "stop" };

        public ICommand AddCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand BrowseFolderCommand { get; }

        public CdeViewModel(string filePath)
        {
            _filePath = filePath;
            Entries = new ObservableCollection<CdeEntry>();
            LoadEntries();

            AddCommand = new RelayCommand3(AddEntry);
            SaveCommand = new RelayCommand3(SaveEntries, () => IsDirty);
            BrowseFolderCommand = new RelayCommand3<CdeEntry>(BrowseFolder);

            // Подписка на изменения коллекции
            Entries.CollectionChanged += (s, e) => IsDirty = true;

            // Подписка на изменения свойств каждого элемента
            foreach (var entry in Entries)
                entry.PropertyChanged += Entry_PropertyChanged;
        }

        private void Entry_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            IsDirty = true;
        }

        /// <summary>
        /// Загрузка данных из файла. Путь собирается из всех частей между первой и последней запятой.
        /// </summary>
        private void LoadEntries()
        {
            if (!File.Exists(_filePath))
                return;

            var lines = File.ReadAllLines(_filePath, Encoding.UTF8);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(',');
                if (parts.Length < 3)
                    continue; // или можно выбросить исключение

                var code = parts[0].Trim();
                var status = parts[parts.Length - 1].Trim();
                // Путь – всё, что между кодом и статусом (может содержать запятые)
                var path = string.Join(",", parts.Skip(1).Take(parts.Length - 2)).Trim();

                var entry = new CdeEntry { Code = code, Path = path, Status = status };
                Entries.Add(entry);
            }

            IsDirty = false;
        }

        private void AddEntry()
        {
            var entry = new CdeEntry { Status = "stop" };
            entry.PropertyChanged += Entry_PropertyChanged;
            Entries.Add(entry);
            IsDirty = true;
        }

        private void SaveEntries()
        {
            var lines = Entries.Select(e => $"{e.Code},{e.Path},{e.Status}").ToArray();
            File.WriteAllLines(_filePath, lines, Encoding.UTF8);
            IsDirty = false;
        }

        private void BrowseFolder(CdeEntry entry)
        {
            if (entry == null) return;

            var dialog = new VistaFolderBrowserDialog
            {
                Description = "Выберите папку",
                ShowNewFolderButton = true,
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() == true)
            {
                entry.Path = dialog.SelectedPath;
            }
        }

        /// <summary>
        /// Вызывается перед закрытием окна. Возвращает true, если можно закрыть.
        /// </summary>
        public bool SaveIfDirty()
        {
            if (!IsDirty)
                return true;

            var result = MessageBox.Show(
                "Сохранить изменения в файле?",
                "Подтверждение",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SaveEntries();
                return true;
            }
            else if (result == MessageBoxResult.No)
            {
                return true; // закрыть без сохранения
            }
            else // Cancel
            {
                return false; // отменить закрытие
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

