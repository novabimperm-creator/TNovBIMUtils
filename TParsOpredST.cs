using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using TNovCommon;

namespace TNovBIMUtils
{
    [Transaction(TransactionMode.Manual)]
    public class TParsOpredST : IExternalCommand
    {
        private TNovProgressBar ProgressBar;
        private void ThreadStartingPoint()
        {
            this.ProgressBar = new TNovProgressBar();
            this.ProgressBar.Show();
            Dispatcher.Run();
        }
        #region Параметры
        Guid adskCMarkParamGuid = new Guid("5d369dfb-17a2-4ae2-a1a1-bdfc33ba7405"); //A_Марка конструкции
        Guid TOprParamGuid = new Guid("7b538440-ae96-4e43-9dbb-4d35be82eb9c"); //Т_Определение
        Guid NTParamsNotSetParamGuid = new Guid("70879f6b-b838-49de-8ff5-35e1c7d97e0c");
        #endregion
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Т Параметры Определение КЖ";
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
            Logger.Initialize(DBCommandName,dateTime,TNovVersion);

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

            #region Сбор элементов
            List<ElementId> ids = new List<ElementId>();

            List<FamilyInstance> beams = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming)   //Каркас несущий
                                                                         .WhereElementIsNotElementType()
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            foreach (var e in beams) ids.Add(e.Id); 
            
            List<FamilyInstance> columns = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns)   //Несущие колонны
                                                                         .WhereElementIsNotElementType()
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            foreach (var e in columns) ids.Add(e.Id); 

            List<Wall> walls = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls)   //Стены
                                                                         .WhereElementIsNotElementType()    
                                                                         .OfClass(typeof(Wall))         //отсеиваем модели в контексте
                                                                         .Cast<Wall>()                     
                                                                         .ToList();
            foreach (var e in walls) ids.Add(e.Id); 

            List<Autodesk.Revit.DB.Floor> floors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Floors)   //Перекрытия
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.Floor))  //отсеиваем модели в контексте
                                                                         .Cast<Autodesk.Revit.DB.Floor>()
                                                                         .ToList();
            foreach (var e in floors) ids.Add(e.Id); 

            List<Autodesk.Revit.DB.Architecture.Stairs> stairs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Stairs)   //Лестницы
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.Architecture.Stairs))  //отсеиваем модели в контексте
                                                                         .Cast<Autodesk.Revit.DB.Architecture.Stairs>()
                                                                         .ToList();
            foreach (var e in stairs) ids.Add(e.Id);

            List<FamilyInstance> stairs2 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Stairs)   //Лестницы семействами
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            foreach (var e in stairs2) ids.Add(e.Id);

            List<StairsRun> stairRuns = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StairsRuns)
                .WhereElementIsNotElementType().OfClass(typeof(StairsRun)).Cast<StairsRun>().ToList(); //марши
            foreach (var e in stairRuns) ids.Add(e.Id); 

            List<StairsLanding> stairLandings = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StairsLandings)
                .WhereElementIsNotElementType().OfClass(typeof(StairsLanding)).Cast<StairsLanding>().ToList(); //площадки
            foreach (var e in stairLandings) ids.Add(e.Id);

            ElementId stairSupportsСategoryId = new ElementId(-2000123);
            ElementCategoryFilter stairSupportsСategoryFilter = new ElementCategoryFilter(stairSupportsСategoryId);
            List<Element> stairSupports = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WherePasses(stairSupportsСategoryFilter)
                .Cast<Element>()
                .ToList(); //опоры
            foreach (var e in stairSupports) ids.Add(e.Id); 

            List<Autodesk.Revit.DB.Architecture.Railing> railings = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StairsRailing)   //Ограждения
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.Architecture.Railing)) //отсеиваем модели в контексте
                                                                         .Cast<Autodesk.Revit.DB.Architecture.Railing>()
                                                                         .ToList();
            foreach (var e in railings) ids.Add(e.Id); 

            List<FamilyInstance> railings2 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StairsRailing)   //Ограждения семействами
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            foreach (var e in railings2) ids.Add(e.Id); 

            List<Autodesk.Revit.DB.Floor> foundations = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFoundation)   //Фундаменты
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.Floor))  //отсеиваем модели в контексте
                                                                         .Cast<Autodesk.Revit.DB.Floor>()
                                                                         .ToList();
            foreach (var e in foundations) ids.Add(e.Id); 

            List<FamilyInstance> foundations2 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFoundation)   //Фундаменты семействами
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            foreach (var e in foundations2) ids.Add(e.Id); 

            List<Autodesk.Revit.DB.WallFoundation> foundations3 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFoundation)   //Фундаменты ленточные
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.WallFoundation))  //отсеиваем модели в контексте
                                                                         .Cast<Autodesk.Revit.DB.WallFoundation>()
                                                                         .ToList();
            foreach (var e in foundations3) ids.Add(e.Id); 

            ElementId slabEdgesСategoryId = new ElementId(-2001392);
            ElementCategoryFilter slabEdgesСategoryFilter = new ElementCategoryFilter(slabEdgesСategoryId);
            List<Element> slabEdges = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WherePasses(slabEdgesСategoryFilter)
                .Cast<Element>()
                .ToList(); //ребра плит
            foreach (var e in slabEdges) ids.Add(e.Id); 

            List<FamilyInstance> GMs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)   //Об мод
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            foreach (var e in GMs) ids.Add(e.Id); 

            List<FamilyInstance> structconnections = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructConnections)   //Болты фунд
                                                                         .WhereElementIsNotElementType()
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            foreach (var e in structconnections) ids.Add(e.Id); 
                

            int allcount = ids.Count;
            
            #endregion
            
            Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Thread.Sleep(100);

            int PBCount = 0;
            this.ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.ProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
            this.ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.ProgressBar.value.Text = PBCount.ToString()));
            this.ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.ProgressBar.TNov_ProgressBar.Maximum = allcount));
            this.ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.ProgressBar.maxvalue.Text = allcount.ToString()));

            #region Основной код
            using (Transaction transaction = new Transaction(doc))
            {
                try
                {
                    transaction.Start("TNov - Т Параметры Определение КЖ");
                    Logger.Log("Открываем транзакцию", 1);

                    foreach (var id in ids)
                    {
                        
                        Element elem = doc.GetElement(id); Logger.Log("Элемент " + id.IntegerValue.ToString(), 2);
                        if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, elem) && elem.get_Parameter(NTParamsNotSetParamGuid).AsDouble() == 1)
                        {
                            Logger.Log("   пропуск", 2); continue;
                        }

                        int catId = elem.Category.Id.IntegerValue;
                        if (catId == -2000919 || catId == -2000920 || catId == -2000123 || catId == -2000120) //лестницы и вложенные лестниц - ускоренное назначение параметра
                        {
                            if (Param.ParamExistByGuid(TOprParamGuid, elem))
                            {
                                Parameter param = elem.get_Parameter(TOprParamGuid); //Т_Определение
                                if (param.IsReadOnly == false) { param.Set("Лестница"); Logger.Log("   назначено Лестница", 2); }
                            }
                            continue;
                        }
                        if (catId == -2000126) //ограждения - ускоренное назначение параметра
                        {
                            if (Param.ParamExistByGuid(TOprParamGuid, elem))
                            {
                                Parameter param = elem.get_Parameter(TOprParamGuid); //Т_Определение
                                if (param.IsReadOnly == false) { param.Set("Ограждение"); Logger.Log("   назначено Ограждение", 2); }
                            }
                            continue;
                        }
                        string group = "";
                        group = MarkGroup(elem, doc);
                        if (group != null && group.Length > 0 && Param.ParamExistByGuid(TOprParamGuid, elem))
                        {
                            Parameter param = elem.get_Parameter(TOprParamGuid); //Т_Определение
                            if (param.IsReadOnly == false) { param.Set(group); Logger.Log("   назначено " + group, 2); }
                        }
                        PBCount++;
                        this.ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.ProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                        this.ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.ProgressBar.value.Text = PBCount.ToString()));

                    }

                    transaction.Commit(); Logger.Log("Закрываем транзакцию", 1);
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка: " + ex.Message, 4);

                    new InfoWindow280("Ошибка: " + ex.Message).ShowDialog();
                }
                finally
                {
                    CloseProgressBarSafely();
                }
            }
            #endregion
            

            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }

        String MarkGroup(in Element elem, in Document doc)
        {
            string mark = "-"; 
            if(Param.ParamExistByGuid(adskCMarkParamGuid,elem)& elem.get_Parameter(adskCMarkParamGuid).HasValue)
            {
                mark= elem.get_Parameter(adskCMarkParamGuid).AsString(); Logger.Log("      Марка: " + mark, 2);
            }

            string group = "";
            if (mark.StartsWith("Фп")||mark.StartsWith("Рп") || mark.StartsWith("Фм") || mark.StartsWith("Рм") || mark.StartsWith("Рл"))
            {
                ElementId typeId = elem.GetTypeId();  if (typeId != null&&typeId.IntegerValue!=-1) group = ParseTypeST(typeId, doc, "Фундамент");
            }
            else if (mark.StartsWith("Пл") || mark.StartsWith("Пп"))
            {
                ElementId typeId = elem.GetTypeId(); if (typeId != null && typeId.IntegerValue != -1) group = ParseTypeST(typeId, doc, "Плита перекрытия");
            }
            else if (mark.StartsWith("Пб"))
            {
                ElementId typeId = elem.GetTypeId(); if (typeId != null && typeId.IntegerValue != -1) group = ParseTypeST(typeId, doc, "Плита по грунту");
            }
            else if(mark.StartsWith("Пр"))
            {
                ElementId typeId = elem.GetTypeId(); if (typeId != null && typeId.IntegerValue != -1) group = ParseTypeST(typeId, doc, "Приямок");
            }
            else if(mark.StartsWith("Кл"))
            {
                ElementId typeId = elem.GetTypeId(); if (typeId != null && typeId.IntegerValue != -1) group = ParseTypeST(typeId, doc, "Колонна");
            }
            else if(mark.StartsWith("Пм"))
            {
                ElementId typeId = elem.GetTypeId(); if (typeId != null && typeId.IntegerValue != -1) group = ParseTypeST(typeId, doc, "Пилон");
            }
            else if(mark.StartsWith("Дж") || mark.StartsWith("Мс"))
            {
                ElementId typeId = elem.GetTypeId(); if (typeId != null && typeId.IntegerValue != -1) group = ParseTypeST(typeId, doc, "Стена");
            }
            else if(mark.StartsWith("Бм"))
            {
                ElementId typeId = elem.GetTypeId(); if (typeId != null && typeId.IntegerValue != -1) group = ParseTypeST(typeId, doc, "Балка");
            }
            else if(mark.StartsWith("Лм") || mark.StartsWith("Лп") || mark.StartsWith("Лк"))
            {
                ElementId typeId = elem.GetTypeId(); if (typeId != null && typeId.IntegerValue != -1) group = ParseTypeST(typeId, doc, "Лестница");
            }
            else if (mark.StartsWith("Пт"))
            {
                ElementId typeId = elem.GetTypeId(); if (typeId != null && typeId.IntegerValue != -1) group = ParseTypeST(typeId, doc, "Парапет");
            }
            else //прочие марки (Км и т.д.) либо пустые марки
            {
                if (elem.Category.Id.IntegerValue.Equals(-2001300))
                {
                    ElementId typeId = elem.GetTypeId(); if (typeId != null && typeId.IntegerValue != -1) group = ParseTypeST(typeId, doc, "Фундамент");
                }
                if (elem.Category.Id.IntegerValue.Equals(-2000032))
                {
                    ElementId typeId = elem.GetTypeId(); if (typeId != null && typeId.IntegerValue != -1) group = ParseTypeST(typeId, doc, "Плита перекрытия");
                }
                if (elem.Category.Id.IntegerValue.Equals(-2000011))
                {
                    ElementId typeId = elem.GetTypeId(); if (typeId != null && typeId.IntegerValue != -1) group = ParseTypeST(typeId, doc, "Стена");
                }
                if (elem.Category.Id.IntegerValue.Equals(-2000120))
                {
                    ElementId typeId = elem.GetTypeId(); if (typeId != null && typeId.IntegerValue != -1) group = ParseTypeST(typeId, doc, "Лестница");
                }
            }

            return group;
        }

        String ParseTypeST(in ElementId typeId, in Document doc, in string OpredValue)
        {
            string group = "";
            Element type = doc.GetElement(typeId);
            //подготовка, термо, гидро, сваи, лестницы, галтели
            if (type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).HasValue) //условие исходя из группы модели
            {
                string gm = type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString(); Logger.Log("      Группа модели: " + gm, 2);
                if (gm.Contains("Подготовка") || gm.Contains("Подбетонка")) group = "Подготовка";
                if (gm.Contains("Термо")) group = "Термовкладыш";
                if (gm.Contains("Свая")) group = "Свая";
                if (gm.Contains("Лестн")) group = "Лестница";
                if (gm.Contains("Галтель")) group = "Фундамент";
            }
            else //альтернативное исходя из имени типа
            {
                Logger.Log("      Имя типа: " + type.Name, 2);
                if (type.Name.Contains("Подготовка") || type.Name.Contains("Подбетонка")) group = "Подготовка";
                if (type.Name.Contains("Термо") ) group = "Термовкладыш";
                if (type.Name.Contains("ГИ") || type.Name.Contains("Гидроиз")) group = "Гидроизоляция";
                if (type.Name.Contains("Фунд")) group = "Фундамент";
            }
            //основная конструкция (бетон)
            if (type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).HasValue) //условие исходя из группы модели
            {
                string gm = type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString();
                if (gm.Contains("Бетон") || gm.Contains("Бетон")) group = OpredValue;
            }
            else //альтернативное исходя из имени типа
            {
                Logger.Log("      Имя типа: " + type.Name, 2);
                if (type.Name.Contains("Бетон") || type.Name.Contains("Бетон")) group = OpredValue;
            }
            //рампа
            if (type.Name.Contains("Рампа") || type.Name.Contains("рампа")) { Logger.Log("      Имя типа: " + type.Name, 2); group = "Рампа"; }
            

            return group;
        }
        private void CloseProgressBarSafely()
        {
            if (ProgressBar != null &&
                ProgressBar.Dispatcher != null &&
                !ProgressBar.Dispatcher.HasShutdownStarted)
            {
                ProgressBar.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    if (ProgressBar.IsLoaded)
                        ProgressBar.Close();
                    // Завершаем цикл сообщений диспетчера, чтобы поток завершился
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }));
            }
        }
    }
}
