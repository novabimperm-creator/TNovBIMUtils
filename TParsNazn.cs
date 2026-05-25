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
    public class TParsNazn : IExternalCommand
    {
        private TNovProgressBar ProgressBar;
        private void ThreadStartingPoint()
        {
            this.ProgressBar = new TNovProgressBar();
            this.ProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Т Параметры Назначение";
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

            List<Room> rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms)   //фильтр по категории Помещения
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .Cast<Room>()                     //элементы категории Помещения
                                                                         .ToList();                         //формируем список

            var walls = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .Where(w => w.WallType != null && w.WallType.Kind == WallKind.Basic)
                .ToList();

            var floors = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType()
                .OfClass(typeof(Floor))
                .Cast<Floor>()
                .ToList();

            var ceilings = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Ceilings)
                .WhereElementIsNotElementType()
                .OfClass(typeof(Ceiling))
                .Cast<Ceiling>()
                .ToList();

            List<Element> elems = new List<Element>();
            foreach (var wall in walls)
            {
                Element type = doc.GetElement(wall.GetTypeId());
                if (type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString().Contains("Отделка")) elems.Add(doc.GetElement(wall.Id));
            }
            foreach (var floor in floors)
            {
                Element type = doc.GetElement(floor.GetTypeId());
                if (type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString().Contains("Пол")) elems.Add(doc.GetElement(floor.Id));
            }
            foreach (var ceiling in ceilings)
            {
                Element type = doc.GetElement(ceiling.GetTypeId());
                if (type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString().Contains("Потолок")) elems.Add(doc.GetElement(ceiling.Id));
            }
            #endregion

            #region Параметры и проверка их наличия
            Guid NTParamsNotSetParamGuid = new Guid("70879f6b-b838-49de-8ff5-35e1c7d97e0c");
            Guid TPolozhParamGuid = new Guid("7d68b956-732c-4da9-99a8-13be56ccaf94"); //Т_Положение
            Guid TNaznParamGuid = new Guid("2a73f7b8-05e7-410a-b22a-66498e315df4"); //Т_Назначение
            Guid NOtdRoomParamGuid = new Guid("8b9d4aff-a6c8-4ad5-b0f5-442f2b87c765"); //N_Отделка.Помещение


            // Безопасно берём первый элемент — если коллекция пуста, получим null
            Element testRoom = rooms?.FirstOrDefault();
            Element testWall = walls?.FirstOrDefault();
            Element testFloor = floors?.FirstOrDefault();
            Element testCeiling = ceilings?.FirstOrDefault();

            // проверка наличия Т параметров
            bool tPar1Exist = true;
            bool tPar2Exist = true;

            // Для параметра Т_Положение: если элемент существует, проверяем наличие параметра.
            if ((testRoom != null && !Param.ParamExistByGuid(TPolozhParamGuid, testRoom)) ||
                (testWall != null && !Param.ParamExistByGuid(TPolozhParamGuid, testWall)) ||
                (testFloor != null && !Param.ParamExistByGuid(TPolozhParamGuid, testFloor)) ||
                (testCeiling != null && !Param.ParamExistByGuid(TPolozhParamGuid, testCeiling)))
            {
                tPar1Exist = false;
            }

            // Аналогично для параметра Т_Назначение
            if ((testRoom != null && !Param.ParamExistByGuid(TNaznParamGuid, testRoom)) ||
                (testWall != null && !Param.ParamExistByGuid(TNaznParamGuid, testWall)) ||
                (testFloor != null && !Param.ParamExistByGuid(TNaznParamGuid, testFloor)) ||
                (testCeiling != null && !Param.ParamExistByGuid(TNaznParamGuid, testCeiling)))
            {
                tPar2Exist = false;
            }

            if (tPar1Exist == false && tPar2Exist == false)
            {
                Logger.Log("Отсутствует целевой параметр Т_Назначение или Т_Положение в проекте. Завершение работы.", 4);
                new InfoWindow280("Отсутствует целевой параметр Т_Назначение или Т_Положение в проекте!").ShowDialog();
                return Result.Cancelled;
            }
            #endregion

            int allcount = rooms.Count + elems.Count;

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
                    transaction.Start("TNov - Т Параметры Назначение");
                    Logger.Log("Открываем транзакцию", 1);

                    Logger.Log("Обработка помещений", 1);
                    //помещения
                    foreach (var room in rooms)
                    {
                        Logger.Log("Помещение " + room.Id.IntegerValue.ToString(), 2);
                        if (room.get_Parameter(NTParamsNotSetParamGuid).AsDouble() == 1)
                        {
                            Logger.Log("   пропуск", 2); continue;
                        }
                        string roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME).AsValueString();
                        string roomNazn = "";
                        if (room.get_Parameter(BuiltInParameter.ROOM_DEPARTMENT).HasValue) roomNazn = room.get_Parameter(BuiltInParameter.ROOM_DEPARTMENT).AsValueString();
                        Logger.Log("   " + roomName + " " + roomNazn, 2);

                        string value = GetTNazn(roomNazn, roomName);
                        if (Param.ParamExistByGuid(TPolozhParamGuid, room) && value != null)
                        {
                            room.get_Parameter(TPolozhParamGuid).Set(value);
                            Logger.Log("   назначено " + value, 2);
                        }
                        if (Param.ParamExistByGuid(TNaznParamGuid, room) && value != null)
                        {
                            room.get_Parameter(TNaznParamGuid).Set(value);
                            Logger.Log("   назначено " + value, 2);
                        }

                        PBCount++;
                        this.ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.ProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                        this.ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.ProgressBar.value.Text = PBCount.ToString()));

                    }
                    Logger.Log("Обработка элементов", 1);
                    //элементы
                    foreach (var elem in elems)
                    {
                        Logger.Log("Элемент " + elem.Id.IntegerValue.ToString(), 2);
                        if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, elem) && elem.get_Parameter(NTParamsNotSetParamGuid).AsDouble() == 1)
                        {
                            Logger.Log("   пропуск", 2); continue;
                        }

                        string roomName = "";
                        if (elem.get_Parameter(NOtdRoomParamGuid).HasValue) roomName = elem.get_Parameter(NOtdRoomParamGuid).AsString();
                        string roomNazn = "";
                        if (elem.LookupParameter("Отделка.Помещение.Назначение").HasValue) roomNazn = elem.LookupParameter("Отделка.Помещение.Назначение").AsString();
                        Logger.Log("   " + roomName + " " + roomNazn, 2);

                        string value = GetTNazn(roomNazn, roomName);
                        if (Param.ParamExistByGuid(TPolozhParamGuid, elem) && value != null && elem.get_Parameter(TPolozhParamGuid).IsReadOnly == false)
                        {
                            elem.get_Parameter(TPolozhParamGuid).Set(value);
                            Logger.Log("   назначено " + value, 2);
                        }
                        if (Param.ParamExistByGuid(TNaznParamGuid, elem) && value != null && elem.get_Parameter(TNaznParamGuid).IsReadOnly == false)
                        {
                            elem.get_Parameter(TNaznParamGuid).Set(value);
                            Logger.Log("   назначено " + value, 2);
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
        string GetTNazn(string Nazn, string Name)
        {
            string TNazn = "";
            if (Nazn.Contains("Жил")) TNazn = Nazn;
            else if (Nazn.Contains("Технич"))
            {
                if (Name.Contains("Лестн") || Name.Contains("лестн")) TNazn = "Лестница";
                else TNazn = "Техническое";
            }
            else if (Nazn.Contains("Лестн")) TNazn = "Лестница";
            else if (Nazn.Contains("Кладов")) TNazn = "Кладовые";
            else if (Nazn.Contains("Встроен")) TNazn = "МОП";
            else if (Nazn.Contains("Парк")) TNazn = "МОП";
            else if (Nazn.Contains("МОП"))
            {
                if (Name.Contains("Лестн") || Name.Contains("лестн")) TNazn = "Лестница";
                else if (Name.Contains("Кладов")) TNazn = "Кладовые";
                else if (Name.Contains("Электр")) TNazn = "Техническое";
                else if (Name.Contains("связи")) TNazn = "Техническое";
                else if (Name.Contains("Технич")) TNazn = "Техническое";
                else if (Name.Contains("ИТП")) TNazn = "Техническое";
                else if (Name.Contains("Котельная")) TNazn = "Техническое";
                else if (Name.Contains("Пульт")) TNazn = "Техническое";
                else if (Name.Contains("Венткамера")) TNazn = "Техническое";
                else TNazn = "МОП";
            }
            else TNazn = "Коммерция";
            return TNazn;
        }

    }
}
