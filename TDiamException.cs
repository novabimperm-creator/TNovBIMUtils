using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TNovBIMUtils
{
    public class TDiamException : INotifyPropertyChanged
    {
        public const string SourceSize = "Размер";
        public const string SourceArticle = "Артикул";

        private string _match;
        private string _source = SourceSize;

        public string Match
        {
            get => _match;
            set { _match = value; OnPropertyChanged(); }
        }

        public string Source
        {
            get => _source;
            set { _source = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
