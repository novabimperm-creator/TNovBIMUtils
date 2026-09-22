using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using TNovCommon;

namespace TNovBIMUtils
{
    public class TDiamExceptionsViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<TDiamException> _entries;
        private TDiamException _selected;
        private bool _isDirty;
        private string _filePath;

        public ObservableCollection<TDiamException> Entries
        {
            get => _entries;
            set { _entries = value; OnPropertyChanged(nameof(Entries)); }
        }

        public TDiamException Selected
        {
            get => _selected;
            set { _selected = value; OnPropertyChanged(nameof(Selected)); }
        }

        public bool IsDirty
        {
            get => _isDirty;
            set { _isDirty = value; OnPropertyChanged(nameof(IsDirty)); }
        }

        public string FilePath
        {
            get => _filePath;
            set { _filePath = value; OnPropertyChanged(nameof(FilePath)); }
        }

        public string[] Sources { get; } = { TDiamException.SourceSize, TDiamException.SourceArticle };

        public ICommand AddCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SaveCommand { get; }

        public TDiamExceptionsViewModel()
        {
            FilePath = TDiamExceptionStore.GetFilePath() ?? "";
            Entries = new ObservableCollection<TDiamException>(TDiamExceptionStore.Load());
            foreach (TDiamException entry in Entries)
                entry.PropertyChanged += Entry_PropertyChanged;
            Entries.CollectionChanged += (s, e) => IsDirty = true;

            AddCommand = new RelayCommand3(AddEntry);
            DeleteCommand = new RelayCommand3(DeleteEntry, () => Selected != null);
            SaveCommand = new RelayCommand3(SaveEntries, () => IsDirty);
            IsDirty = false;
        }

        void AddEntry()
        {
            var entry = new TDiamException { Match = "", Source = TDiamException.SourceSize };
            entry.PropertyChanged += Entry_PropertyChanged;
            Entries.Add(entry);
            Selected = entry;
            IsDirty = true;
        }

        void DeleteEntry()
        {
            if (Selected == null) return;
            Selected.PropertyChanged -= Entry_PropertyChanged;
            Entries.Remove(Selected);
            Selected = null;
            IsDirty = true;
        }

        void SaveEntries()
        {
            TDiamExceptionStore.Save(Entries);
            IsDirty = false;
        }

        void Entry_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            IsDirty = true;
        }

        public bool SaveIfDirty()
        {
            if (!IsDirty) return true;

            MessageBoxResult result = MessageBox.Show(
                "Сохранить изменения в файле исключений?",
                "Исключения Т_Диаметр",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    SaveEntries();
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Исключения Т_Диаметр", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            if (result == MessageBoxResult.No) return true;
            return false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
