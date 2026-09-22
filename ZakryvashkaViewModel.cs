using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace TNovBIMUtils
{
    public class OpenDocItem : INotifyPropertyChanged
    {
        private bool _isChecked;

        public OpenDocItem(Document document, string name)
        {
            Document = document;
            Name = name;
        }

        public Document Document { get; }
        public string Name { get; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ZakryvashkaViewModel : INotifyPropertyChanged
    {
        private bool _selectAllModels;
        private bool _selectAllFamilies;
        private bool _updatingModels;
        private bool _updatingFamilies;
        private bool _syncAndSave = true;
        private bool _closeDocuments = true;

        public ObservableCollection<OpenDocItem> Models { get; } = new ObservableCollection<OpenDocItem>();
        public ObservableCollection<OpenDocItem> Families { get; } = new ObservableCollection<OpenDocItem>();

        public bool SelectAllModels
        {
            get => _selectAllModels;
            set
            {
                if (_selectAllModels == value) return;
                _selectAllModels = value;
                OnPropertyChanged();
                if (_updatingModels) return;
                _updatingModels = true;
                foreach (var item in Models)
                    item.IsChecked = value;
                _updatingModels = false;
            }
        }

        public bool SelectAllFamilies
        {
            get => _selectAllFamilies;
            set
            {
                if (_selectAllFamilies == value) return;
                _selectAllFamilies = value;
                OnPropertyChanged();
                if (_updatingFamilies) return;
                _updatingFamilies = true;
                foreach (var item in Families)
                    item.IsChecked = value;
                _updatingFamilies = false;
            }
        }

        public bool SyncAndSave
        {
            get => _syncAndSave;
            set { _syncAndSave = value; OnPropertyChanged(); }
        }

        public bool CloseDocuments
        {
            get => _closeDocuments;
            set { _closeDocuments = value; OnPropertyChanged(); }
        }

        public void AddModel(OpenDocItem item)
        {
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(OpenDocItem.IsChecked))
                    RefreshSelectAllModels();
            };
            Models.Add(item);
        }

        public void AddFamily(OpenDocItem item)
        {
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(OpenDocItem.IsChecked))
                    RefreshSelectAllFamilies();
            };
            Families.Add(item);
        }

        void RefreshSelectAllModels()
        {
            if (_updatingModels) return;
            _updatingModels = true;
            bool all = Models.Count > 0 && Models.All(m => m.IsChecked);
            if (_selectAllModels != all)
            {
                _selectAllModels = all;
                OnPropertyChanged(nameof(SelectAllModels));
            }
            _updatingModels = false;
        }

        void RefreshSelectAllFamilies()
        {
            if (_updatingFamilies) return;
            _updatingFamilies = true;
            bool all = Families.Count > 0 && Families.All(m => m.IsChecked);
            if (_selectAllFamilies != all)
            {
                _selectAllFamilies = all;
                OnPropertyChanged(nameof(SelectAllFamilies));
            }
            _updatingFamilies = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
