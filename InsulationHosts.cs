using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using TNovCommon;

namespace TNovBIMUtils
{
    [Transaction(TransactionMode.Manual)]
    public class InsulationHosts : IExternalCommand
    {
        private TNovProgressBar bimExportProgressBar;
        private void ThreadStartingPoint()
        {
            this.bimExportProgressBar = new TNovProgressBar();
            this.bimExportProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Хосты изоляции"; DateTime dateTime = DateTime.Now; string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;

            //проверка подключения, запись в журнал
            if(ServerUtils.CheckConnection(TNovClassName, TNovVersion)==false) return Result.Failed;

            // создание log - файла
            Logger.Initialize(TNovClassName,dateTime,TNovVersion);

            var viewModel0 = new AppVersionViewModel();

            string jsonpath0 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovSettings.json");
            viewModel0 = JsonConvert.DeserializeObject<AppVersionViewModel>(File.ReadAllText(jsonpath0));

            if (viewModel0.extendedLogs)

            {
                var qViewModel = new QuestionWindowViewModel();
                qViewModel.headtxt = "Включены расширенные логи. " +
                    "Плагин будет работать медленнее, но соберет больше данных. " +
                    "Выключить расширенные логи для ускорения работы?";
                var qwpfview = new QuestionWindow280(qViewModel);
                qViewModel.CloseRequest += (s, e) => qwpfview.Close();
                bool? qok = qwpfview.ShowDialog();
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log("Расширенные логи вкл", 2);
            }

            //сбор элементов
            List<Element> VnIsolVozd = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctLinings)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();
            List<Element> IsolVozd = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctInsulations)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();
            List<Element> IsolTrub = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PipeInsulations)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();

            List<ElementId> ids = new List<ElementId>();
            foreach (var elem in VnIsolVozd) ids.Add(elem.Id); foreach (var elem in IsolVozd) ids.Add(elem.Id); foreach (var elem in IsolTrub) ids.Add(elem.Id);

            int allcount = ids.Count;

            Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Thread.Sleep(100);

            int PBCount = 0;
            this.bimExportProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.bimExportProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
            this.bimExportProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.bimExportProgressBar.value.Text = PBCount.ToString()));
            this.bimExportProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.bimExportProgressBar.TNov_ProgressBar.Maximum = allcount));
            this.bimExportProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.bimExportProgressBar.maxvalue.Text = allcount.ToString()));

            //назначение параметров
            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start("TNov - Хосты изоляции");
                Logger.Log("Открываем транзакцию", 1);
                foreach (ElementId id in ids)
                {
                    Element elem = doc.GetElement(id);
                    if (elem != null & elem.Name != null)
                    {
                        string value = "Не определено";
                        Logger.Log("Элемент " + id.IntegerValue.ToString(),2);
                        if (elem.Category.Id.IntegerValue == -2008122) //изоляция труб PipeInsulation
                        {
                            PipeInsulation pipeInsulation = (PipeInsulation)elem;
                            if (pipeInsulation.HostElementId != null && pipeInsulation.HostElementId.IntegerValue != -1)
                            {
                                Element host = doc.GetElement(pipeInsulation.HostElementId);
                                int hostCatId = host.Category.Id.IntegerValue;
                                if (hostCatId == -2008049 || hostCatId == -2008055) value = "Фитинги и арматура труб";
                                else if (hostCatId == -2008044) value = "Трубы";
                            }
                        }
                        else if (elem.Category.Id.IntegerValue == -2008123) //изоляция возд DuctInsulation
                        {
                            DuctInsulation ductInsulation = (DuctInsulation)elem;
                            if (ductInsulation.HostElementId != null && ductInsulation.HostElementId.IntegerValue != -1)
                            {
                                Element host = doc.GetElement(ductInsulation.HostElementId);
                                int hostCatId = host.Category.Id.IntegerValue;
                                if (hostCatId == -2008010 || hostCatId == -2008016) value = "Фитинги и арматура воздуховодов";
                                else if (hostCatId == -2008000) value = "Воздуховоды";
                            }
                        }
                        else if (elem.Category.Id.IntegerValue == -2008124) //внутр изол возд DuctLining
                        {
                            DuctLining ductLining = (DuctLining)elem;
                            if (ductLining.HostElementId != null && ductLining.HostElementId.IntegerValue != -1)
                            {
                                Element host = doc.GetElement(ductLining.HostElementId);
                                int hostCatId = host.Category.Id.IntegerValue;
                                if (hostCatId == -2008010 || hostCatId == -2008016) value = "Фитинги и арматура воздуховодов";
                                else if (hostCatId == -2008000) value = "Воздуховоды";
                            }
                        }
                        try
                        {
                            elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(value);
                            Logger.Log("   успешно",2);
                        }
                        catch (Exception e) { Logger.Log("Элемент " + id.IntegerValue.ToString()+" ошибка: "+e.Message, 4); }

                        PBCount++;
                        this.bimExportProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.bimExportProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                        this.bimExportProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.bimExportProgressBar.value.Text = PBCount.ToString()));
                    }
                }
                transaction.Commit(); Logger.Log("Закрываем транзакцию", 1);
            }
            this.bimExportProgressBar.Dispatcher.Invoke((System.Action)(() => this.bimExportProgressBar.Close()));

            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }
    }
}
