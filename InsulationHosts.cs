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
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Хосты изоляции";
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            string docName = doc.Title.ToString(); docName = docName.Replace(",", " ");
            string userName = rvtApp.Username; userName = userName.Replace(",", "");
            string docNameUserName = "_" + userName; docName = docName.Replace(docNameUserName, "");
            docName = docName.Replace(",", "");
            #endregion

            TNovConfig config = TNovConfigLoad.LoadConfig(DBCommandName, TNovVersion); if (config == null) return Result.Failed;

            #region Настройки логов
            // создание log - файла
            Logger.Initialize(DBCommandName, dateTime, TNovVersion);

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
            #endregion

# region Сбор элементов
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
            #endregion

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

            bool unhandledError = false;

            #region Основной код
            using (Transaction transaction = new Transaction(doc))
            {
                try 
                { 
                    transaction.Start("TNov - Хосты изоляции");
                    Logger.Log("Открываем транзакцию", 1);
                    foreach (ElementId id in ids)
                    {
                        Element elem = doc.GetElement(id);
                        if (elem != null & elem.Name != null)
                        {
                            string value = "Не определено";

                            Logger.Log("Элемент " + IdLongValue(id).ToString(),2);
                            long catId = IdLongValue(elem.Category.Id);

                            if (catId == -2008122) //изоляция труб PipeInsulation
                            {
                                PipeInsulation pipeInsulation = (PipeInsulation)elem;
                                if (pipeInsulation.HostElementId != null && IdLongValue(pipeInsulation.HostElementId) != -1)
                                {
                                    Element host = doc.GetElement(pipeInsulation.HostElementId);
                                    int hostCatId = (int)IdLongValue(host.Category.Id);
                                    if (hostCatId == -2008049 || hostCatId == -2008055) value = "Фитинги и арматура труб";
                                    else if (hostCatId == -2008044) value = "Трубы";
                                }
                            }
                            else if (IdLongValue(elem.Category.Id) == -2008123) //изоляция возд DuctInsulation
                            {
                                DuctInsulation ductInsulation = (DuctInsulation)elem;
                                if (ductInsulation.HostElementId != null && IdLongValue(ductInsulation.HostElementId) != -1)
                                {
                                    Element host = doc.GetElement(ductInsulation.HostElementId);
                                    int hostCatId = (int)IdLongValue(host.Category.Id);
                                    if (hostCatId == -2008010 || hostCatId == -2008016) value = "Фитинги и арматура воздуховодов";
                                    else if (hostCatId == -2008000) value = "Воздуховоды";
                                }
                            }
                            else if (IdLongValue(elem.Category.Id) == -2008124) //внутр изол возд DuctLining
                            {
                                DuctLining ductLining = (DuctLining)elem;
                                if (ductLining.HostElementId != null && IdLongValue(ductLining.HostElementId) != -1)
                                {
                                    Element host = doc.GetElement(ductLining.HostElementId);
                                    int hostCatId = (int)IdLongValue(host.Category.Id);
                                    if (hostCatId == -2008010 || hostCatId == -2008016) value = "Фитинги и арматура воздуховодов";
                                    else if (hostCatId == -2008000) value = "Воздуховоды";
                                }
                            }
                            try
                            {
                                elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(value);
                                Logger.Log("   успешно",2);
                            }
                            catch (Exception e) { Logger.Log("Элемент " + IdLongValue(id).ToString()+" ошибка: "+e.Message, 4); }

                            PBCount++;
                            this.bimExportProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.bimExportProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                            this.bimExportProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.bimExportProgressBar.value.Text = PBCount.ToString()));
                        }
                    }
                    transaction.Commit(); Logger.Log("Закрываем транзакцию", 1);
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка: " + ex.Message, 4);
                    unhandledError = true;
                    new InfoWindow280("Ошибка: + ex.Message").ShowDialog();
                }
                finally
                {
                    CloseProgressBarSafely();
                }
            }
#endregion

            if (unhandledError)
            {
                Logger.Log("Завершение работы с ошибкой.", 4);
                return Result.Succeeded;
            }

            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }
        private void CloseProgressBarSafely()
        {
            if (bimExportProgressBar != null &&
                bimExportProgressBar.Dispatcher != null &&
                !bimExportProgressBar.Dispatcher.HasShutdownStarted)
            {
                bimExportProgressBar.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (bimExportProgressBar.IsLoaded)
                        bimExportProgressBar.Close();
                    // Завершаем цикл сообщений диспетчера, чтобы поток завершился
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }));
            }
        }

        private long IdLongValue (ElementId id)
        {
#if R2022
            return id.IntegerValue;
#else
            return id.Value;
#endif
        }
    }
}
