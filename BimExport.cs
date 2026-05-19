using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Shapes;
using System.Windows.Threading;
using TNovCommon;
using Document = Autodesk.Revit.DB.Document;

namespace TNovBIMUtils
{
    
    [Transaction(TransactionMode.Manual)]
    public class BimExport : IExternalCommand
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
            string DBCommandName = "BIM Экспорт";
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


            
            FailureAndWarningHandler andWarningHandler = new FailureAndWarningHandler();
            rvtApp.FailuresProcessing += new EventHandler<FailuresProcessingEventArgs>(andWarningHandler.OnFailuresProcessing);

            #region Сбор элементов
            //связи
            List<RevitLinkInstance> links0 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_RvtLinks)      //фильтр по категории Связи
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .Cast<RevitLinkInstance>()         //элементы категории Связи
                                                                         .ToList();                         //формируем список

            List<string> linksString = new List<string>();
            if (links0 == null || links0.Count == 0) linksString.Add("-----");
            else
            {
                Logger.Log("Существующие связи: ", 2);
                foreach (var link in links0)
                {
                    if (link.GetLinkDocument() != null) //выгруженные связи не страшны
                    {
                        string[] nameparts = link.Name.Split(new char[] { ':' });
                        linksString.Add(nameparts[0]);
                        Logger.Log("   " + nameparts[0], 2);
                    }
                }
                if(linksString.Count==0) linksString.Add("-----");
            }
            #endregion

            #region Диалог
            Logger.Log("Диалоговое окно",1);
            var viewModel = new BimExportViewModel();
            // Десериализация
            string jsonpath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/bimexport.json");
            string jsonText = File.ReadAllText(jsonpath, Encoding.UTF8);
            //if (jsonText.Contains(@"\\\\")) jsonText = jsonText.Replace(@"\\", @"\");
            //jsonText = jsonText.Replace(@"\", "/"); 
            try
            {
                viewModel = JsonConvert.DeserializeObject<BimExportViewModel>(jsonText);
                viewModel.BuildTree(linksString, config.ServerPath);
                Logger.Log("Десериализация прошла успешно",1);
            }
            catch (Exception ex)
            {
                Logger.Log("Ошибка при десериализации: " + ex.Message,4);
            }
            var wpfview = new BimExportWPF(viewModel, linksString);
            
            if (linksString.First()!= "-----") 
            {
                Logger.Log("Пропускаемые связи: ", 2);
                foreach(var link in linksString) Logger.Log("   "+link, 2);
            }
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != null && ok == true) { }
            else { Logger.Log("Запуск отменен пользователем. Завершение работы.", 3); return Result.Cancelled; }
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel), Encoding.UTF8);
                Logger.Log("Сериализация прошла успешно",1);
            }
            catch (Exception ex) { Logger.Log("Ошибка при сериализации: " + ex.Message, 4); }

            if (viewModel.NWC == false && viewModel.RVT == false && viewModel.NWC2 == false) 
            { Logger.Log("Все галочки сняты. Завершение работы.", 3); return Result.Cancelled; }
            #endregion

            string log = "Журнал запуска:";

            #region Модели в работу
            string rvtPath = viewModel.folder;
            string rvtPath2 = viewModel.folder3;
            string nwcPath = viewModel.folder2;
            string nwcPathD = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            bool useRevitServer = viewModel.fromRS;

            List<string> rvtFiles = new List<string>();

            string rvtPathRS = "";
            string RSfilePath = File.ReadAllText(config.ServerPath + "RSpath.txt");
            if (viewModel.Nodes.Count > 0)
            {
                List<Node> allNodes = GetAllNodes(viewModel.Nodes).ToList();
                foreach (var node in allNodes)
                {
                    if (node.IsChecked && node.IsModel && node.IsLocked == false) rvtPathRS += @"RSN:\\" + RSfilePath + @"\" + node.Path + "|";
                }

            }
            if (rvtPathRS.Length > 0)
            {
                rvtPathRS = rvtPathRS.Substring(0, rvtPathRS.Length - 1);
            }

            if (useRevitServer)
            {
                Logger.Log("Используется Revit Server", 2);
                if(rvtPathRS.Length<3)
                {
                    new InfoWindow280("Модели в дереве Revit Server не были выбраны.").ShowDialog();
                    Logger.Log("Не выбраны модели на RS. Завершение работы.",3);
                    return Result.Cancelled;
                }
                string[] strings = rvtPathRS.Split('|');
                foreach(string s in strings) rvtFiles.Add(s);
            }
            else
            {
                string[] rvtFilesFromPath = Directory.GetFiles(rvtPath, "*.rvt"); 
                foreach (string rvtFile in rvtFilesFromPath) rvtFiles.Add( rvtFile );
                if (rvtFiles.Count == 0)
                {
                    new InfoWindow280("Файлы не выбраны.").ShowDialog(); return Result.Failed;
                }

                


                int oldFilesCount = 0; string oldFilesNames = "";
                foreach (string rvtFile in rvtFiles)
                {
                    var fileInfo = new FileInfo(rvtFile);
                    if (fileInfo.LastWriteTime < DateTime.Now.AddDays(-1))
                    {
                        oldFilesCount++; oldFilesNames += fileInfo.Name + ", ";
                    }

                }
                if (oldFilesCount > 0)
                {
                    var qViewModel = new QuestionWindowViewModel();
                    qViewModel.headtxt = "В папке _RVT имеются устаревшие модели: " + oldFilesNames + "Продолжить работу?";
                    var qwpfview = new QuestionWindow280(qViewModel);
                    qViewModel.CloseRequest += (s, e) => qwpfview.Close();
                    bool? qok = qwpfview.ShowDialog();
                    if (qok != null && qok == true) { } else { return Result.Cancelled; }
                }
            }
            #endregion

            Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Thread.Sleep(100);

            int PBCount = 0;
            this.bimExportProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.bimExportProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
            this.bimExportProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.bimExportProgressBar.value.Text = PBCount.ToString()));
            this.bimExportProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.bimExportProgressBar.TNov_ProgressBar.Maximum = (double)rvtFiles.Count));
            this.bimExportProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.bimExportProgressBar.maxvalue.Text = rvtFiles.Count.ToString()));

            int desktopNWCcount = 0;

            #region Основной код
            try
            {
                foreach (string rvtFile in rvtFiles)
                {

                    string strForReplace = rvtPath + @"\";
                    string fileName = rvtFile.Replace(strForReplace, ""); fileName = fileName.Replace(".rvt", "");
                    if (useRevitServer)
                    {
                        string name0 = rvtFile.Replace(@"\", @"/");
                        string[] nameParts = name0.Split('/');
                        fileName = nameParts[nameParts.Length - 1];
                        fileName = fileName.Replace(".rvt", "");
                    }
                    if (fileName.Contains("-АР") && viewModel.AR == false) continue;
                    if (fileName.Contains("-ПОФ") && viewModel.AR == false) continue;
                    if (fileName.Contains("-КЖ") && viewModel.ST == false) continue;
                    if (fileName.Contains("-ВК") && viewModel.VK == false) continue;
                    if (fileName.Contains("-ОВ") && viewModel.OV == false) continue;
                    if (fileName.Contains("-ЭЛ") && viewModel.EL == false) continue;
                    if (fileName.Contains("-ЭО") && viewModel.EL == false) continue;
                    if (fileName.Contains("-СС") && viewModel.SS == false) continue;
                    if (fileName.Contains("_АР") && viewModel.AR == false) continue;
                    if (fileName.Contains("_ПОФ") && viewModel.AR == false) continue;
                    if (fileName.Contains("_КЖ") && viewModel.ST == false) continue;
                    if (fileName.Contains("_ВК") && viewModel.VK == false) continue;
                    if (fileName.Contains("_ОВ") && viewModel.OV == false) continue;
                    if (fileName.Contains("_ЭЛ") && viewModel.EL == false) continue;
                    if (fileName.Contains("_ЭО") && viewModel.EL == false) continue;
                    if (fileName.Contains("_СС") && viewModel.SS == false) continue;
                    if (viewModel.namefilter != null && viewModel.namefilter != "")
                    {
                        if (fileName.Contains(viewModel.namefilter)) { } else continue;
                    }
                    this.bimExportProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.bimExportProgressBar.info.Text = fileName + ": открытие модели"));

                    Logger.Log(fileName, 1);
                    string filePath = rvtFile;
                    OpenOptions openOptions = new OpenOptions();
                    openOptions.DetachFromCentralOption = DetachFromCentralOption.DetachAndDiscardWorksets;
                    WorksetConfiguration worksetConfiguration1 = new WorksetConfiguration(WorksetConfigurationOption.CloseAllWorksets);
                    openOptions.SetOpenWorksetsConfiguration(worksetConfiguration1);
                    //ModelPathUtils.ConvertUserVisiblePathToModelPath(new FileInfo(Path.Combine(filePath)).FullName);
                    ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);
                    ModelPath modelPath1 = ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);
                    try
                    {
                        Document document = uiApp.Application.OpenDocumentFile(modelPath1, openOptions);

                        if (viewModel.NWC || viewModel.NWC2)
                        {
                            this.bimExportProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.bimExportProgressBar.info.Text = fileName +
                            ": экспорт NWC"));

                            //наличие геометрии в модели
                            int elemsCount = 0;
                            List<Autodesk.Revit.DB.Floor> foundations = new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_StructuralFoundation)   //Фундаменты
                                                                                     .WhereElementIsNotElementType()
                                                                                     .OfClass(typeof(Autodesk.Revit.DB.Floor))  //отсеиваем модели в контексте
                                                                                     .Cast<Autodesk.Revit.DB.Floor>()
                                                                                     .ToList();
                            if (foundations.Count > 0)
                            {
                                elemsCount += foundations.Count; Logger.Log("фундаменты: " + foundations.Count.ToString(), 2);
                            }
                            List<Wall> walls = new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_Walls)
                                                                                     .WhereElementIsNotElementType()
                                                                                     .OfClass(typeof(Wall))
                                                                                     .Cast<Wall>()
                                                                                     .ToList();
                            if (walls.Count > 0)
                            {
                                elemsCount += walls.Count; Logger.Log("стены: " + walls.Count.ToString(), 2);
                            }
                            List<Autodesk.Revit.DB.Floor> floors = new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_Floors)
                                                                                     .WhereElementIsNotElementType()
                                                                                     .OfClass(typeof(Autodesk.Revit.DB.Floor))
                                                                                     .Cast<Autodesk.Revit.DB.Floor>()
                                                                                     .ToList();
                            if (floors.Count > 0)
                            {
                                elemsCount += floors.Count; Logger.Log("плиты: " + floors.Count.ToString(), 2);
                            }
                            List<CableTray> CTList = new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_CableTray)
                                                                         .WhereElementIsNotElementType()
                                                                         .Cast<CableTray>()
                                                                         .ToList();
                            if (CTList.Count > 0)
                            {
                                elemsCount += CTList.Count; Logger.Log("лотки: " + CTList.Count.ToString(), 2);
                            }
                            List<Element> Vozd = new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_DuctCurves)
                            .WhereElementIsNotElementType()
                            .Cast<Element>()
                            .ToList();
                            if (Vozd.Count > 0)
                            {
                                elemsCount += Vozd.Count; Logger.Log("воздуховоды: " + Vozd.Count.ToString(), 2);
                            }
                            List<Element> Obor = new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_MechanicalEquipment)
                            .WhereElementIsNotElementType()
                            .Cast<Element>()
                            .ToList();
                            if (Obor.Count > 0)
                            {
                                elemsCount += Obor.Count; Logger.Log("оборудование: " + Obor.Count.ToString(), 2);
                            }
                            List<Element> Trub = new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_PipeCurves)
                            .WhereElementIsNotElementType()
                            .Cast<Element>()
                            .ToList();
                            if (Trub.Count > 0)
                            {
                                elemsCount += Trub.Count; Logger.Log("трубы: " + Trub.Count.ToString(), 2);
                            }
                            List<FamilyInstance> GMs = new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_GenericModel)
                                                                                    .WhereElementIsNotElementType()
                                                                                 .OfClass(typeof(FamilyInstance))
                                                                                 .Cast<FamilyInstance>()
                                                                                 .ToList();
                            if (GMs.Count > 0)
                            {
                                elemsCount += GMs.Count; Logger.Log("обобщенные модели: " + GMs.Count.ToString(), 2);
                            }
                            bool modelConsistsElements = elemsCount > 0;
                            //настройки экспорта в NWC
                            NavisworksExportOptions navisworksExportOptions = new NavisworksExportOptions();
                            navisworksExportOptions.ExportScope = NavisworksExportScope.View;
                            navisworksExportOptions.ConvertElementProperties = true;
                            navisworksExportOptions.ExportLinks = false; //добавлено 03.26
                            navisworksExportOptions.ConvertLinkedCADFormats = false;
                            navisworksExportOptions.DivideFileIntoLevels = false;
                            navisworksExportOptions.FindMissingMaterials = false;
                            List<Element> list = ((IEnumerable<Element>)new FilteredElementCollector(document)
                                .WhereElementIsNotElementType()
                                .OfCategory(BuiltInCategory.OST_Views))
                                .Where<Element>((System.Func<Element, bool>)(fi => fi.Name == "Talan" || fi.Name == "Navisworks"))
                                .ToList<Element>();
                            if (viewModel.NWCNova)
                            {
                                Logger.Log("экспортируем с вида Nova", 2);
                                List<Element> listN = ((IEnumerable<Element>)new FilteredElementCollector(document)
                                .WhereElementIsNotElementType()
                                .OfCategory(BuiltInCategory.OST_Views))
                                .Where<Element>((System.Func<Element, bool>)(fi => fi.Name == "Nova"))
                                .ToList<Element>();
                                if (listN.Count > 0) list.Insert(0, listN[0]); //заменяем первый элемент списка видов нужным видом Nova
                            }



                            bool canOverwriteNwc = true;
                            string nwcPath1 = nwcPath;
                            if (viewModel.NWC == false) nwcPath1 = rvtPath2;
                            string path = nwcPath1 + @"\" + fileName + ".nwc"; if (viewModel.NWCNova) path = nwcPath1 + @"\" + fileName + " внутр.nwc";
                            bool nwcFileExists = File.Exists(path);
                            if (nwcFileExists)
                            {
                                Logger.Log("найден существующий NWC-файл", 2);
                                FileInfo fileInfo = new FileInfo(path);
                                bool fileInUse = IsFileInUse(fileInfo);
                                if (fileInUse)
                                {
                                    nwcPath1 = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                                    path = nwcPath1 + @"\" + fileName + ".nwc"; if (viewModel.NWCNova) path = nwcPath1 + @"\" + fileName + " внутр.nwc";
                                    bool nwcFileExists1 = File.Exists(path);
                                    if (nwcFileExists1)
                                    {
                                        FileInfo fileInfo1 = new FileInfo(path);
                                        bool fileInUse1 = IsFileInUse(fileInfo1);
                                        if (fileInUse1)
                                        {
                                            Logger.Log("ошибка: модель NWC используется другим приложением, сохранить на рабочий стол также не удалось", 4);
                                            log += "\nМодель " + fileName + " - ошибка: модель NWC используется, сохранить на рабочий стол также не удалось";
                                            canOverwriteNwc = false;
                                        }
                                        else
                                        {
                                            this.bimExportProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.bimExportProgressBar.info.Text = fileName +
                            ": экспорт NWC на рабочий стол"));

                                            Logger.Log("ошибка: модель NWC используется другим приложением, сохраняем на рабочий стол", 1);
                                            log += "\nМодель " + fileName + " - ошибка: модель NWC используется, NWC сохранен на рабочий стол";
                                            desktopNWCcount++;
                                        }
                                    }


                                }
                            }
                            //проверка на наличие элементов
                            if (!modelConsistsElements)
                            {
                                Logger.Log("в модели отсутствуют элементы", 1);
                                log += "\nМодель " + fileName + " - в модели отсутствуют элементы";
                                canOverwriteNwc = false;
                            }
                            //экспорт в NWC
                            if (canOverwriteNwc)
                            {
                                if (list != null)
                                {

                                    string nwcFileName = fileName; if (viewModel.NWCNova) nwcFileName += " внутр";

                                    try
                                    {
                                        using (Transaction t = new Transaction(document))
                                        {
                                            t.Start("Сброс подрезки вида");
                                            Element vElem = document.GetElement(list[0].Id);
                                            View3D view3D = (View3D)vElem;
                                            view3D.get_Parameter(BuiltInParameter.VIEWER_MODEL_CLIP_BOX_ACTIVE).Set(0);
                                            if (view3D.IsSectionBoxActive == false) Logger.Log("Подрезка вида сброшена", 1);
                                            t.Commit();
                                        }

                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Log("ошибка: " + ex.Message, 4);
                                        log += "\nМодель " + fileName + " - ошибка: " + ex.Message;
                                    }

                                    try
                                    {
                                        navisworksExportOptions.ViewId = list[0].Id;
                                        document.Export(nwcPath1, nwcFileName, navisworksExportOptions);
                                        if (viewModel.NWC && viewModel.NWC2)
                                        {
                                            //копирование nwc в папку выдачи
                                            File.Copy(nwcPath1 + @"\" + nwcFileName + ".nwc", rvtPath2 + @"\" + nwcFileName + ".nwc");
                                        }
                                        if (nwcPath1 == nwcPathD)
                                        {
                                            log += "\nМодель " + nwcFileName + " - NWC занят другим приложением, поэтому сохранен на рабочий стол";
                                            Logger.Log("Модель " + nwcFileName + " - NWC успешно (рабочий стол)", 1);
                                        }
                                        else
                                        {
                                            log += "\nМодель " + nwcFileName + " - успешно создан NWC";
                                            Logger.Log("Модель " + nwcFileName + " - NWC успешно", 1);
                                        }

                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Log("ошибка: " + ex.Message, 4);
                                        log += "\nМодель " + nwcFileName + " - ошибка: " + ex.Message;
                                    }




                                }
                                else
                                {
                                    Logger.Log("отсутствует вид для экспорта", 4);
                                    log += "\nМодель " + fileName + " - отсутствует вид для экспорта";

                                }
                            }

                            if (viewModel.RVT == false) document.Close(false);
                        }

                        if (viewModel.RVT)
                        {
                            this.bimExportProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.bimExportProgressBar.info.Text = fileName +
                            ": очистка RVT"));

                            bool rvtFileExists = File.Exists(rvtPath2 + @"\" + fileName + ".rvt");
                            if (rvtFileExists)
                            {
                                FileInfo fileInfo = new FileInfo(rvtPath2 + @"\" + fileName + ".rvt");
                                fileInfo.Delete();
                            }

                            //удаление связей
                            List<RevitLinkInstance> links = new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_RvtLinks)      //фильтр по категории Связи
                                                                                    .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                                    .Cast<RevitLinkInstance>()         //элементы категории Связи
                                                                                    .ToList();                         //формируем список
                            ICollection<ElementId> linksToDelete = new List<ElementId>();
                            if (links.Count > 0)
                            {
                                foreach (RevitLinkInstance link in links)
                                {
                                    linksToDelete.Add(link.Id);
                                }
                                using (Transaction transaction = new Transaction(document))
                                {
                                    transaction.Start("Удалить связи");
                                    document.Delete(linksToDelete);
                                    transaction.Commit();
                                }
                            }
                            //вид тангл
                            List<Element> listTangl = ((IEnumerable<Element>)new FilteredElementCollector(document)
                                .WhereElementIsNotElementType()
                                .OfCategory(BuiltInCategory.OST_Views))
                                .Where<Element>((System.Func<Element, bool>)(fi => fi.Name == "Tangl"))
                                .ToList<Element>();

                            //чистка файла
                            using (Transaction transaction1 = new Transaction(document))
                            {
                                transaction1.Start("Очистка RVT");
                                try { PurgeDoc.Purge(document); Logger.Log("Очистка прошла успешно", 1); }
                                catch (Exception e) { Logger.Log($"Ошибка очистки: {e.Message}", 4); }

                                if (listTangl.Count > 0)
                                {
                                    Element vElemT = document.GetElement(listTangl[0].Id);
                                    View3D view3DT = (View3D)vElemT;
                                    view3DT.get_Parameter(BuiltInParameter.VIEWER_MODEL_CLIP_BOX_ACTIVE).Set(0);
                                    if (view3DT.IsSectionBoxActive == false) Logger.Log("Подрезка вида Tangl сброшена", 1);
                                }
                                transaction1.Commit();
                            }

                            //сохранить файл
                            this.bimExportProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.bimExportProgressBar.info.Text = fileName +
                            ": сохранение RVT"));

                            document.SaveAs(rvtPath2 + @"\" + fileName + ".rvt");
                            document.Close(false);
                            log += "\nМодель " + fileName + " - RVT успешно очищен и сохранен"; Logger.Log("Модель " + fileName + " - RVT успешно", 1);
                        }

                        PBCount++;
                        this.bimExportProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.bimExportProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                        this.bimExportProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.bimExportProgressBar.value.Text = PBCount.ToString()));

                    }
                    catch (Autodesk.Revit.Exceptions.FileNotFoundException)
                    {
                        log += "\nМодель " + fileName + " - модель не существует, обновите дерево Revit Server";
                        Logger.Log("\nМодель " + fileName + " - модель не существует в дереве Revit Server", 1);
                        continue;
                    }


                }

                this.bimExportProgressBar.Dispatcher.Invoke((System.Action)(() => this.bimExportProgressBar.Close()));
            }
            catch (Exception ex)
            {
                Logger.Log("Ошибка: " + ex.Message, 4);
            }
            finally
            {
                CloseProgressBarSafely();
            }
            new InfoWindow400(log).ShowDialog();
            if (viewModel.NWC) Process.Start("explorer.exe", nwcPath);
            if (viewModel.NWC&& desktopNWCcount>0) Process.Start("explorer.exe", nwcPathD);
            if (viewModel.RVT||viewModel.NWC2) Process.Start("explorer.exe", rvtPath2);
            #endregion

            rvtApp.FailuresProcessing -= new EventHandler<FailuresProcessingEventArgs>(andWarningHandler.OnFailuresProcessing);

            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
        private static IEnumerable<Node> GetAllNodes(ObservableCollection<Node> nodes)
        {
            foreach (var node in nodes)
            {
                yield return node;

                foreach (var child in GetAllNodes(node.Children))
                {
                    yield return child;
                }
            }
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
        private bool IsFileInUse(FileInfo file)
        {
            try
            {
                using (FileStream stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                return true;
            }

            return false;
        }
    }
}
