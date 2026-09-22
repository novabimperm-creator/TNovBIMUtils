using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Interop;
using System.Windows.Threading;
using TNovCommon;
using Document = Autodesk.Revit.DB.Document;

namespace TNovBIMUtils
{
    [Transaction(TransactionMode.Manual)]
    public class Otkryvashka : IExternalCommand
    {
        private TNovProgressBar progressBar;

        private void ThreadStartingPoint()
        {
            this.progressBar = new TNovProgressBar();
            this.progressBar.Show();
            Dispatcher.Run();
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Открывашка";
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIApplication uiApp = RevitAPI.UiApplication;
            Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            string userName = rvtApp.Username;
            userName = userName.Replace(",", "");
            #endregion

            TNovConfig config = TNovConfigLoad.LoadConfig(DBCommandName, TNovVersion);
            if (config == null) return Result.Failed;

            #region Настройки логов
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

            #region Диалог
            Logger.Log("Диалоговое окно", 1);
            List<string> openModels = GetOpenModelNames(rvtApp, userName);
            if (openModels.First() != "-----")
            {
                Logger.Log("Уже открытые модели:", 2);
                foreach (var openModel in openModels)
                    Logger.Log("   " + openModel, 2);
            }

            RevitServerViewModel viewModel;
            try
            {
                viewModel = new RevitServerViewModel(openModels, " (открыто)");
                viewModel.Header = "ОТКРЫВАШКА";
            }
            catch (Exception ex)
            {
                new InfoWindow280("Не удалось построить дерево Revit Server:\n" + ex.Message).ShowDialog();
                Logger.Log("Ошибка построения дерева RS: " + ex.Message, 4);
                return Result.Failed;
            }

            if (viewModel.Nodes == null)
                viewModel.Nodes = new ObservableCollection<Node>();

            var wpfview = new RevitServer(viewModel);
            new WindowInteropHelper(wpfview) { Owner = uiApp.MainWindowHandle };
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != true)
            {
                Logger.Log("Запуск отменен пользователем. Завершение работы.", 3);
                return Result.Cancelled;
            }
            #endregion

            #region Модели в работу
            string rsPathFile = config.ServerPath + "RSpath.txt";
            if (!File.Exists(rsPathFile))
            {
                new InfoWindow280("Не найден файл RSpath.txt.").ShowDialog();
                Logger.Log("Не найден RSpath.txt. Завершение работы.", 3);
                return Result.Cancelled;
            }

            string RSfilePath = FolderPathHelper.Sanitize(File.ReadAllText(rsPathFile));
            List<string> rvtFiles = new List<string>();
            if (viewModel.Nodes != null && viewModel.Nodes.Count > 0)
            {
                foreach (var node in GetAllNodes(viewModel.Nodes))
                {
                    if (node.IsChecked && node.IsModel && node.IsLocked == false)
                        rvtFiles.Add(@"RSN:\\" + RSfilePath + @"\" + node.Path);
                }
            }

            if (rvtFiles.Count == 0)
            {
                new InfoWindow280("Модели в дереве Revit Server не были выбраны.").ShowDialog();
                Logger.Log("Не выбраны модели на RS. Завершение работы.", 3);
                return Result.Cancelled;
            }
            #endregion

            List<string> projectNames = LoadProjectNames(config);
            Logger.Log("Загружено проектов из CDE: " + projectNames.Count, 2);

            FailureAndWarningHandler andWarningHandler = new FailureAndWarningHandler();
            rvtApp.FailuresProcessing += andWarningHandler.OnFailuresProcessing;
            uiApp.DialogBoxShowing += OnUnresolvedReferencesDialog;

            string log = "Журнал запуска:";
            try
            {
                Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
                for (int i = 0; i < 50 && this.progressBar == null; i++)
                    Thread.Sleep(100);
                if (this.progressBar == null)
                {
                    Logger.Log("Не удалось создать окно прогресса", 4);
                    return Result.Failed;
                }

                int PBCount = 0;
                void BumpProgress()
                {
                    PBCount++;
                    this.progressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.progressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.progressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.progressBar.value.Text = PBCount.ToString()));
                }

                this.progressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.progressBar.TNov_ProgressBar.Minimum = (double)PBCount));
                this.progressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.progressBar.value.Text = PBCount.ToString()));
                this.progressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.progressBar.TNov_ProgressBar.Maximum = (double)rvtFiles.Count));
                this.progressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.progressBar.maxvalue.Text = rvtFiles.Count.ToString()));

                #region Основной код
                try
                {
                    foreach (string rvtFile in rvtFiles)
                    {
                        string fileName = System.IO.Path.GetFileNameWithoutExtension(rvtFile);
                        this.progressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.progressBar.info.Text = fileName + ": создание локальной копии"));
                        Logger.Log(fileName, 1);

                        try
                        {
                            ModelPath centralPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(rvtFile);
                            string localFullPath = GetDefaultLocalPath(rvtFile, userName);
                            if (File.Exists(localFullPath))
                            {
                                FileInfo localInfo = new FileInfo(localFullPath);
                                if (localInfo.IsReadOnly)
                                    localInfo.IsReadOnly = false;
                                localInfo.Delete();
                                Logger.Log("Существующий локальный файл перезаписан: " + localFullPath, 2);
                            }

                            ModelPath localPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(localFullPath);
                            WorksharingUtils.CreateNewLocal(centralPath, localPath);

                            string projectPrefix = FindProjectInPath(fileName, projectNames);
                            if (string.IsNullOrEmpty(projectPrefix))
                                Logger.Log("Префикс проекта в CDE не найден для " + fileName, 2);
                            else
                                Logger.Log("Префикс проекта: " + projectPrefix, 2);

                            this.progressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.progressBar.info.Text = fileName + ": открытие модели"));

                            OpenOptions openOptions = new OpenOptions();
                            openOptions.DetachFromCentralOption = DetachFromCentralOption.DoNotDetach;
                            openOptions.SetOpenWorksetsConfiguration(CreateOpenWorksetConfiguration(localPath, projectPrefix));
                            uiApp.OpenAndActivateDocument(localPath, openOptions, false);

                            log += "\nМодель " + fileName + " - открыта как новый локальный файл";
                            if (!string.IsNullOrEmpty(projectPrefix))
                                log += " (префикс " + projectPrefix + ")";
                            Logger.Log("Модель " + fileName + " - открыта как новый локальный", 1);
                        }
                        catch (Autodesk.Revit.Exceptions.FileNotFoundException)
                        {
                            log += "\nМодель " + fileName + " - модель не существует, обновите дерево Revit Server";
                            Logger.Log("Модель " + fileName + " - модель не существует в дереве Revit Server", 1);
                        }
                        catch (Exception ex)
                        {
                            log += "\nМодель " + fileName + " - ошибка: " + ex.Message;
                            Logger.Log("Ошибка (" + fileName + "): " + ex.Message, 4);
                        }
                        finally
                        {
                            BumpProgress();
                        }
                    }

                    this.progressBar.Dispatcher.Invoke((System.Action)(() => this.progressBar.Close()));
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
                #endregion
            }
            finally
            {
                CloseProgressBarSafely();
                rvtApp.FailuresProcessing -= andWarningHandler.OnFailuresProcessing;
                uiApp.DialogBoxShowing -= OnUnresolvedReferencesDialog;
            }

            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }

        private void OnUnresolvedReferencesDialog(object sender, DialogBoxShowingEventArgs e)
        {
            try
            {
                string dialogId = e.DialogId ?? string.Empty;
                TaskDialogShowingEventArgs taskDlg = e as TaskDialogShowingEventArgs;
                string msg = taskDlg != null ? (taskDlg.Message ?? string.Empty) : string.Empty;

                bool unresolved =
                    dialogId.Equals("TaskDialog_Unresolved_References", StringComparison.OrdinalIgnoreCase)
                    || dialogId.IndexOf("Unresolved", StringComparison.OrdinalIgnoreCase) >= 0
                    || msg.IndexOf("не удалось найти или считать", StringComparison.OrdinalIgnoreCase) >= 0
                    || msg.IndexOf("could not find or read", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!unresolved)
                    return;

                e.OverrideResult((int)TaskDialogResult.CommandLink2);
                Logger.Log("Пропущен диалог необработанных связей: " + dialogId, 2);
            }
            catch (Exception ex)
            {
                Logger.Log("Ошибка закрытия диалога необработанных связей: " + ex.Message, 4);
            }
        }

        private static WorksetConfiguration CreateOpenWorksetConfiguration(ModelPath modelPath, string projectPrefix)
        {
            WorksetConfiguration worksetConfiguration = new WorksetConfiguration(WorksetConfigurationOption.OpenAllWorksets);
            if (string.IsNullOrEmpty(projectPrefix))
                return worksetConfiguration;

            IList<WorksetPreview> worksets;
            try
            {
                worksets = WorksharingUtils.GetUserWorksetInfo(modelPath);
            }
            catch (Exception ex)
            {
                Logger.Log("Не удалось получить рабочие наборы: " + ex.Message, 4);
                return worksetConfiguration;
            }

            if (worksets == null || worksets.Count == 0)
                return worksetConfiguration;

            List<WorksetId> worksetsToClose = new List<WorksetId>();
            foreach (WorksetPreview workset in worksets)
            {
                if (string.IsNullOrEmpty(workset.Name))
                    continue;
                if (workset.Name.IndexOf(projectPrefix, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                worksetsToClose.Add(workset.Id);
                Logger.Log("Закрываем рабочий набор: " + workset.Name, 2);
            }

            if (worksetsToClose.Count > 0)
                worksetConfiguration.Close(worksetsToClose);

            return worksetConfiguration;
        }

        private static List<string> LoadProjectNames(TNovConfig config)
        {
            string CdeFilePath = config.ServerPath + "CDE.txt";
            var names = new List<string>();
            try
            {
                if (!File.Exists(CdeFilePath))
                    return names;

                foreach (string line in File.ReadLines(CdeFilePath))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    int commaIndex = line.IndexOf(',');
                    string name = (commaIndex >= 0)
                        ? line.Substring(0, commaIndex).Trim()
                        : line.Trim();

                    if (!string.IsNullOrEmpty(name))
                        names.Add(name);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Ошибка чтения CDE.txt: " + ex.Message, 4);
            }

            return names.Distinct().OrderBy(n => n).ToList();
        }

        private static string FindProjectInPath(string path, List<string> projectNames)
        {
            if (string.IsNullOrEmpty(path) || projectNames == null || projectNames.Count == 0)
                return null;

            return projectNames.FirstOrDefault(name =>
                path.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string GetDefaultLocalPath(string rsnPath, string userName)
        {
            string modelName = System.IO.Path.GetFileNameWithoutExtension(rsnPath);
            string fileName = modelName + "_" + userName + ".rvt";
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);
        }

        private static List<string> GetOpenModelNames(Autodesk.Revit.ApplicationServices.Application rvtApp, string userName)
        {
            List<string> names = new List<string>();
            foreach (Document document in rvtApp.Documents)
            {
                if (document == null || !document.IsValidObject || document.IsLinked || document.IsFamilyDocument)
                    continue;

                string title = document.Title ?? "";
                title = title.Replace(",", " ");
                if (!string.IsNullOrEmpty(userName))
                    title = title.Replace("_" + userName, "");
                if (string.IsNullOrWhiteSpace(title))
                    continue;
                if (!title.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase))
                    title += ".rvt";
                names.Add(title);
            }

            if (names.Count == 0)
                names.Add("-----");
            return names;
        }

        private static IEnumerable<Node> GetAllNodes(ObservableCollection<Node> nodes)
        {
            if (nodes == null)
                yield break;

            foreach (var node in nodes)
            {
                yield return node;
                if (node.Children == null)
                    continue;
                foreach (var child in GetAllNodes(node.Children))
                    yield return child;
            }
        }

        private void CloseProgressBarSafely()
        {
            if (progressBar != null &&
                progressBar.Dispatcher != null &&
                !progressBar.Dispatcher.HasShutdownStarted)
            {
                progressBar.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (progressBar.IsLoaded)
                        progressBar.Close();
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }));
            }
        }
    }
}
