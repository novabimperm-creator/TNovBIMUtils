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
    public class TParsNaimOboznST : IExternalCommand
    {
        private TNovProgressBar ProgressBar;
        private void ThreadStartingPoint()
        {
            this.ProgressBar = new TNovProgressBar();
            this.ProgressBar.Show();
            Dispatcher.Run();
        }
        #region Параметры
        Guid NProjectCodeParamGuid = new Guid("ae46eb7a-03bf-497e-ac96-1615c672324b");//N_Ш.ШифрПроекта
        Guid adskSheetSetParamGuid = new Guid("e1b06433-f527-403c-8986-af9a01e6be7f");//A_Комплект чертежей
        Guid adskElemSheetNumberParamGuid = new Guid("68e483e3-4c06-494b-979e-4958d44d6f71");//A_Номер листа элемента
        Guid adskCMarkParamGuid = new Guid("5d369dfb-17a2-4ae2-a1a1-bdfc33ba7405"); //A_Марка конструкции
        Guid adskIzdMarkParamGuid = new Guid("92ae0425-031b-40a9-8904-023f7389963b");//A_Марка изделия
        Guid NNaimParamGuid = new Guid("c0a5ddcb-1fc6-4151-9c6b-e12d2e293b9f");//N_Наименование
        Guid TNaimParamGuid = new Guid("cc50c492-9220-45fa-97fa-a2611b3696e7");//Т_Наименование
        Guid NOboznParamGuid = new Guid("5e21acce-0d16-4d14-81ca-a3adfab14142");//N_Обозначение
        Guid TOboznParamGuid = new Guid("992bd635-f80c-4380-a978-f8ac3bc5a111");//Т_Обозначение
        Guid NTParamsNotSetParamGuid = new Guid("70879f6b-b838-49de-8ff5-35e1c7d97e0c");
        #endregion
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Т Параметры Наименование Обозначение КЖ";
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

            //шифр проекта
            ProjectInfo projectInfo = doc.ProjectInformation;
            string projectCodeValue = ""; try { projectCodeValue = projectInfo.get_Parameter(NProjectCodeParamGuid)?.AsString(); } catch { Logger.Log("В проекте отсутствует параметр Шифр проекта", 4); }


            #region Основной код
            using (Transaction transaction = new Transaction(doc))
            {
                try
                {
                    transaction.Start("TNov - Т Параметры Наименование Обозначение КЖ");
                    Logger.Log("Открываем транзакцию", 1);

                    foreach (var id in ids)
                    {
                        
                        Element elem = doc.GetElement(id); Logger.Log("Элемент " + id.IntegerValue.ToString(), 2);
                        if (elem.get_Parameter(NTParamsNotSetParamGuid).AsDouble() == 1)
                        {
                            Logger.Log("   пропуск", 2); continue;
                        }
                        int catId = elem.Category.Id.IntegerValue;

                        string naimValue = ""; string oboznValue = "";

                        //сценарии: 1 - заводское изделие, 2 - индив изделие, 3 - конструкция
                        int scenario = 3;
                        //считываем исходные параметры либо с экз, либо с типа
                        string NOboznParamValue = GetTextParamValue(doc, elem, NOboznParamGuid); Logger.Log("   " + NOboznParamValue, 2);
                        string NNaimParamValue = GetTextParamValue(doc, elem, NNaimParamGuid); Logger.Log("   " + NNaimParamValue, 2);
                        string adskCMarkParamValue = GetTextParamValue(doc, elem, adskCMarkParamGuid); Logger.Log("   " + adskCMarkParamValue, 2);
                        string adskIzdMarkParamValue = GetTextParamValue(doc, elem, adskIzdMarkParamGuid); Logger.Log("   " + adskIzdMarkParamValue, 2);
                        string adskSheetSetParamValue = GetTextParamValue(doc, elem, adskSheetSetParamGuid); Logger.Log("   " + adskSheetSetParamValue, 2);
                        if (adskSheetSetParamValue.Length > 0) adskSheetSetParamValue = "-" + adskSheetSetParamValue;
                        string adskElemSheetNumberParamValue = GetTextParamValue(doc, elem, adskElemSheetNumberParamGuid); Logger.Log("   " + adskElemSheetNumberParamValue, 2);
                        if (adskElemSheetNumberParamValue.Length > 0) adskElemSheetNumberParamValue = " л. " + adskElemSheetNumberParamValue;
                        //группа модели
                        string gmValue = ""; ElementId typeId = elem.GetTypeId();
                        if (typeId != null && typeId.IntegerValue != -1)
                        {
                            Element type = doc.GetElement(typeId);
                            if (type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).HasValue)
                                gmValue = type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString();
                        }
                        Logger.Log("   " + gmValue, 2);
                        if (gmValue.Contains("Серия") || gmValue.Contains("ГОСТ")) scenario = 1;
                        else if (adskIzdMarkParamValue.Length > 1) scenario = 2;

                        switch (scenario)
                        {
                            case 1:
                                Logger.Log("   заводское", 2);
                                naimValue = NNaimParamValue + " " + adskIzdMarkParamValue;
                                oboznValue = NOboznParamValue;
                                break;
                            case 2:
                                Logger.Log("   индивидуальное", 2);
                                naimValue = NNaimParamValue + " " + adskIzdMarkParamValue;
                                oboznValue = projectCodeValue + adskSheetSetParamValue + adskElemSheetNumberParamValue;
                                break;
                            case 3:
                                Logger.Log("   конструкция", 2);
                                if (NNaimParamValue.Length == 0 && adskCMarkParamValue.Length > 0) NNaimParamValue = ConstructionType(adskCMarkParamValue);
                                naimValue = NNaimParamValue + " " + adskCMarkParamValue;
                                oboznValue = projectCodeValue + adskSheetSetParamValue + adskElemSheetNumberParamValue;
                                break;
                        }

                        if (naimValue != null && naimValue.Length > 0 && Param.ParamExistByGuid(TNaimParamGuid, elem))
                        {
                            Parameter param = elem.get_Parameter(TNaimParamGuid); //Т_Наименование
                            if (param.IsReadOnly == false) { param.Set(naimValue); Logger.Log("   назначено " + naimValue, 2); }
                        }
                        if (oboznValue != null && oboznValue.Length > 0 && Param.ParamExistByGuid(TOboznParamGuid, elem))
                        {
                            Parameter param = elem.get_Parameter(TOboznParamGuid); //Т_Обозначение
                            if (param.IsReadOnly == false) { param.Set(oboznValue); Logger.Log("   назначено " + oboznValue, 2); }
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

        String GetTextParamValue(in Document doc, in Element elem, in Guid paramGuid)
        {
            string paramValue = "";
            Element elem1 = doc.GetElement(elem.Id);
            if (Param.ParamExistByGuid(paramGuid, elem) == false)
            {
                ElementId typeId = elem.GetTypeId(); if (typeId != null && typeId.IntegerValue != -1)
                {
                    Element type = doc.GetElement(typeId);
                    if (Param.ParamExistByGuid(paramGuid, type)) elem1 = doc.GetElement(type.Id);
                }
            }
            if (Param.ParamExistByGuid(paramGuid, elem1) && elem1.get_Parameter(paramGuid).HasValue)
            {
                paramValue = elem1.get_Parameter(paramGuid).AsString();
            }
            return paramValue;
        }
        String ConstructionType(in string mark)
        {
            string type = "";
            if (mark.StartsWith("Фп") || mark.StartsWith("Фм")) type = "Фундаментная плита";
            if (mark.StartsWith("Рп") || mark.StartsWith("Рм")) type = "Ростверк";
            if (mark.StartsWith("Пл") || mark.StartsWith("Пр")) type = "Приямок";
            if (mark.StartsWith("Пп")) type = "Плита перекрытия";
            if (mark.StartsWith("Пб")) type = "Плита по грунту";
            if (mark.StartsWith("Кл")) type = "Колонна";
            if (mark.StartsWith("Пм")) type = "Пилон";
            if (mark.StartsWith("Дж")) type = "Диафрагма жесткости";
            if (mark.StartsWith("Мс")) type = "Стена монолитная";
            if (mark.StartsWith("Бм")) type = "Балка монолитная";
            if (mark.StartsWith("Лм")) type = "Лестница монолитная";
            if (mark.StartsWith("Лп")) type = "Площадка монолитная";
            if (mark.StartsWith("Пт")) type = "Парапет";
            if (mark.StartsWith("Км")) type = "Канал монолитный";
            return type;
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
