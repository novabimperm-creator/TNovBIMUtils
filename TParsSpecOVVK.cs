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
    public class TParsSpecOVVK : IExternalCommand
    {
        private TNovProgressBar ProgressBar;
        private void ThreadStartingPoint()
        {
            this.ProgressBar = new TNovProgressBar();
            this.ProgressBar.Show();
            Dispatcher.Run();
        }
        #region Параметры
        Guid adskGparamGuid = new Guid("3de5f1a4-d560-4fa8-a74f-25d250fb3401");//ADSK_Группирование
        Guid TSystemNameParamGuid = new Guid("e4cd1559-649f-4a24-9782-b1840b41773f");//Т_Имя системы
        Guid adskNparamGuid = new Guid("e6e0f5cd-3e26-485b-9342-23882b20eb43");//ADSK_Наименование
        Guid TNaimParamGuid = new Guid("cc50c492-9220-45fa-97fa-a2611b3696e7");//Т_Наименование
        Guid adskMarkparamGuid = new Guid("2204049c-d557-4dfc-8d70-13f19715e46d");//ADSK_Марка
        Guid adskOboznparamGuid = new Guid("9c98831b-9450-412d-b072-7d69b39f4029");//ADSK_Обозначение
        Guid TOboznParamGuid = new Guid("992bd635-f80c-4380-a978-f8ac3bc5a111");//Т_Обозначение
        Guid adskCodeparamGuid = new Guid("2fd9e8cb-84f3-4297-b8b8-75f444e124ed");//ADSK_Код изделия
        Guid adskManufparamGuid = new Guid("a8cdbf7b-d60a-485e-a520-447d2055f351");//ADSK_Завод-изготовитель
        Guid TManufParamGuid = new Guid("2fcb084c-f1bc-473b-9e88-9f9b304254e1");//Т_Завод-изготовитель
        Guid adskEdParamGuid = new Guid("4289cb19-9517-45de-9c02-5a74ebf5c86d");//ADSK_Единица измерения
        Guid TEdParamGuid = new Guid("9486acdc-ed8e-482e-aa18-b518aaf08a94");//Т_Единица измерения
        Guid adskCparamGuid = new Guid("8d057bb3-6ccd-4655-9165-55526691fe3a");//ADSK_Количество
        Guid TCountParamGuid = new Guid("b3f5d47f-d1cf-4ac4-9a38-a27b5204e16c");//Т_Количество
        Guid adskTstParamGuid = new Guid("381b467b-3518-42bb-b183-35169c9bdfb3");//ADSK_Толщина стенки
        Guid TStParamGuid = new Guid("021340dc-4952-4429-b3a9-20ca2a308d92");//Т_Толщина стенки
        Guid TDimsParamGuid = new Guid("f45c49d7-c46f-418c-948e-d4cde7ea6772");//Т_Размер
        Guid TDiamParamGuid = new Guid("e955e814-e8de-404b-aba7-0cfe10120aff");//Т_Диаметр
        Guid NTParamsNotSetParamGuid = new Guid("70879f6b-b838-49de-8ff5-35e1c7d97e0c");
        #endregion
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Т Параметры ОВ ВК";
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

            List<Element> elems = CombinedElementFilter.GetAllElementsOVVK(doc);
            foreach(var elem in elems) { if(elem.Id!=null) ids.Add(elem.Id); }

            int allcount = ids.Count;
            if(allcount == 0)
            {
                string mes = "Элементы отсутствуют.";
                new InfoWindow280(mes).ShowDialog();
                Logger.Log(mes + " Завершение работы.", 3);
                return Result.Failed;
            }
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

            bool unhandledError = false;
            #region Основной код
            using (Transaction transaction = new Transaction(doc))
            {
                try
                {
                    transaction.Start("TNov - Т Параметры ОВ ВК");
                    Logger.Log("Открываем транзакцию", 1);

                    foreach (var id in ids)
                    {
                        
                        Element elem = doc.GetElement(id); Logger.Log("Элемент " + id.IntegerValue.ToString(), 2);
                        if(elem==null) continue;
                        if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, elem) && elem.get_Parameter(NTParamsNotSetParamGuid).AsDouble() == 1)
                        {
                            Logger.Log("   пропуск", 2); continue;
                        }

                        //Т_Количество
                        double TCountValue = Param.GetDoubleParamValue(doc, adskCparamGuid, elem);

                        //Т_Обозначение
                        string TOboznValue = Param.GetStringParamValue(doc, adskMarkparamGuid, elem);

                        //Т_Наименование
                        string TNaimValue = Param.GetStringParamValue(doc, adskNparamGuid, elem);

                        //Т_Размер
                        string TSizeValue = Param.GetStringParamValue(doc, BuiltInParameter.RBS_CALCULATED_SIZE, elem);

                        //Т_Завод-изготовитель
                        string TManufValue = Param.GetStringParamValue(doc, adskManufparamGuid, elem);

                        //Т_Единица измерения
                        string TEdValue = Param.GetStringParamValue(doc, adskEdParamGuid, elem);

                        //Т_Имя системы
                        string TSystemValue = Param.GetStringParamValue(doc, adskGparamGuid, elem);

                        //Т_Толщина стенки
                        double TStValue = Param.GetDoubleParamValue(doc, adskTstParamGuid, elem)*304.8;

                        //Назначение параметров
                        if (Param.ParamExistByGuid(TCountParamGuid, elem)) //Т_Количество
                        {
                            Parameter TParam = elem.get_Parameter(TCountParamGuid);
                            if (TParam.IsReadOnly == false)
                            {
                                TParam.Set(TCountValue.ToString()); Logger.Log("   кол-во: " + TCountValue.ToString(), 2);
                            }
                        }
                        if (Param.ParamExistByGuid(TOboznParamGuid, elem)) //Т_Обозначение
                        {
                            Parameter TParam = elem.get_Parameter(TOboznParamGuid);
                            if (TParam.IsReadOnly == false)
                            {
                                TParam.Set(TOboznValue); Logger.Log("   обозн: " + TOboznValue, 2);
                            }
                        }
                        if (Param.ParamExistByGuid(TNaimParamGuid, elem)) //Т_Наименование
                        {
                            Parameter TParam = elem.get_Parameter(TNaimParamGuid);
                            if (TParam.IsReadOnly == false)
                            {
                                TParam.Set(TNaimValue); Logger.Log("   наим: " + TNaimValue, 2);
                            }
                        }
                        int categoryId = elem.Category.Id.IntegerValue;
                        if (categoryId == -2008000 || categoryId == -2008010 || categoryId == -2008013 || categoryId == -2008016
                            && Param.ParamExistByGuid(TDimsParamGuid, elem)) //Т_Размер
                        {
                            Parameter TParam = elem.get_Parameter(TDimsParamGuid);
                            if (TParam.IsReadOnly == false)
                            {
                                TParam.Set(TSizeValue); Logger.Log("   размер: " + TSizeValue, 2);
                            }
                        }
                        if (Param.ParamExistByGuid(TManufParamGuid, elem)) //Т_Завод-изготовитель
                        {
                            Parameter TParam = elem.get_Parameter(TManufParamGuid);
                            if (TParam.IsReadOnly == false)
                            {
                                TParam.Set(TManufValue); Logger.Log("   завод: " + TManufValue, 2);
                            }
                        }
                        if (Param.ParamExistByGuid(TEdParamGuid, elem)) //Т_Единица измерения
                        {
                            Parameter TParam = elem.get_Parameter(TEdParamGuid);
                            if (TParam.IsReadOnly == false)
                            {
                                TParam.Set(TEdValue); Logger.Log("   ед изм: " + TEdValue, 2);
                            }
                        }
                        if (Param.ParamExistByGuid(TSystemNameParamGuid, elem)) //Т_Имя системы
                        {
                            Parameter TParam = elem.get_Parameter(TSystemNameParamGuid);
                            if (TParam.IsReadOnly == false)
                            {
                                TParam.Set(TSystemValue); Logger.Log("   имя системы: " + TSystemValue, 2);
                            }
                        }
                        if (Param.ParamExistByGuid(TStParamGuid, elem)) //Т_Толщина стенки
                        {
                            Parameter TParam = elem.get_Parameter(TStParamGuid);
                            if (TParam.IsReadOnly == false)
                            {
                                TParam.Set(TStValue.ToString().Replace(',', '.')); Logger.Log("   толщ ст: " + TStValue.ToString().Replace(',', '.'), 2);
                            }
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
                    unhandledError = true;
                }
                finally
                {
                    CloseProgressBarSafely();
                }
            }
            #endregion
            if (unhandledError)
            {
                Logger.Log("Завершение работы с ошибками.", 4);
                return Result.Succeeded;
            }

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

    }
}
