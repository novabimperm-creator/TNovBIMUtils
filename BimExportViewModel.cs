using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TNovCommon;

namespace TNovBIMUtils
{
    public class BimExportViewModel : INotifyPropertyChanged
    {
        private string _folder = @"\\fs-nova\NOVA\01_ПРОЕКТИРОВАНИЕ\02_Уфа_Промсвязь\03_BIM_Отчеты коллизий\Этап 1\_RVT";
        public string folder { get => _folder; set { _folder = value; OnPropertyChanged(); } }
        private string _folderRS = "";
        public string folderRS { get => _folderRS; set { _folderRS = value; OnPropertyChanged(); } }
        [JsonIgnore] public ObservableCollection<Node> Nodes { get; set; }
        private string _namefilter = "";
        [JsonIgnore] public string namefilter { get => _namefilter; set { _namefilter = value; OnPropertyChanged(); } }

        private string _folder2 = @"\\fs-nova\NOVA\01_ПРОЕКТИРОВАНИЕ\02_Уфа_Промсвязь\03_BIM_Отчеты коллизий\Этап 1\_NWC";
        public string folder2 { get => _folder2; set { _folder2 = value; OnPropertyChanged(); } }

        private string _folder3 = @"\\fs-nova\NOVA\01_ПРОЕКТИРОВАНИЕ\02_Уфа_Промсвязь\06_Выдача";
        public string folder3 { get => _folder3; set { _folder3 = value; OnPropertyChanged(); } }

        private bool _RVTcheck = true;
        public bool RVTcheck { get => _RVTcheck; set { _RVTcheck = value; OnPropertyChanged(); } }

        private bool _RVT = true;
        public bool RVT { get => _RVT; set { _RVT = value; OnPropertyChanged(); } }
        private bool _fromRVT = false;
        public bool fromRVT { get => _fromRVT; set { _fromRVT = value; OnPropertyChanged(); } }
        private bool _fromRS = true;
        public bool fromRS { get => _fromRS; set { _fromRS = value; OnPropertyChanged(); } }
        private bool _NWC = true;
        public bool NWC { get => _NWC; set { _NWC = value; OnPropertyChanged(); } }

        private bool _NWC2 = true;
        public bool NWC2 { get => _NWC2; set { _NWC2 = value; OnPropertyChanged(); } }

        private bool _NWCNova = false;
        public bool NWCNova { get => _NWCNova; set { _NWCNova = value; OnPropertyChanged(); } }

        private bool _AR = true;
        public bool AR { get => _AR; set { _AR = value; OnPropertyChanged(); } }
        private bool _ST = true;
        public bool ST { get => _ST; set { _ST = value; OnPropertyChanged(); } }
        private bool _VK = true;
        public bool VK { get => _VK; set { _VK = value; OnPropertyChanged(); } }
        private bool _OV = true;
        public bool OV { get => _OV; set { _OV = value; OnPropertyChanged(); } }
        private bool _EL = true;
        public bool EL { get => _EL; set { _EL = value; OnPropertyChanged(); } }
        private bool _SS = true;
        public bool SS { get => _SS; set { _SS = value; OnPropertyChanged(); } }

        public BimExportViewModel(IEnumerable<string> existingModels)
        {
            List<string> filePaths = File.ReadAllLines(nova.novaserver + "_TNov/RS.txt").ToList();
            Nodes = TreeBuilder.BuildTree(filePaths, existingModels);
        }
        public event EventHandler CloseRequest;
        private void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }


    }
}
