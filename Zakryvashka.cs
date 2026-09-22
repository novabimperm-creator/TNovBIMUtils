using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
    public class Zakryvashka : IExternalCommand
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
            string DBCommandName = "Закрывашка";
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIApplication uiApp = RevitAPI.UiApplication;
            Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            #endregion

            TNovConfig config = TNovConfigLoad.LoadConfig(DBCommandName, TNovVersion);
            if (config == null) return Result.Failed;

            #region Настройки логов
            Logger.Initialize(DBCommandName, dateTime, TNovVersion);

            var viewModel0 = new AppVersionViewModel();
            string jsonpath0 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovSettings.json");
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
            var viewModel = new ZakryvashkaViewModel();
            CollectOpenDocuments(rvtApp, viewModel);

            var wpfview = new ZakryvashkaWPF(viewModel);
            new WindowInteropHelper(wpfview) { Owner = uiApp.MainWindowHandle };
            bool? ok = wpfview.ShowDialog();
            if (ok != true)
            {
                Logger.Log("Запуск отменен пользователем. Завершение работы.", 3);
                return Result.Cancelled;
            }

            if (!viewModel.SyncAndSave && !viewModel.CloseDocuments)
            {
                new InfoWindow280("Не выбрано действие: включите синхронизацию/сохранение или закрытие.").ShowDialog();
                Logger.Log("Не выбрано действие. Завершение работы.", 3);
                return Result.Cancelled;
            }

            List<OpenDocItem> selected = viewModel.Models.Where(m => m.IsChecked)
                .Concat(viewModel.Families.Where(f => f.IsChecked))
                .ToList();
            if (selected.Count == 0)
            {
                new InfoWindow280("Не выбраны модели или семейства.").ShowDialog();
                Logger.Log("Не выбраны документы. Завершение работы.", 3);
                return Result.Cancelled;
            }
            #endregion

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

                this.progressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.progressBar.TNov_ProgressBar.Minimum = 0));
                this.progressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.progressBar.value.Text = "0"));
                this.progressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.progressBar.TNov_ProgressBar.Maximum = (double)selected.Count));
                this.progressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.progressBar.maxvalue.Text = selected.Count.ToString()));

                try
                {
                    for (int i = 0; i < selected.Count; i++)
                    {
                        OpenDocItem item = selected[i];
                        this.progressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.progressBar.info.Text = item.Name));
                        Logger.Log(item.Name, 1);
                        List<OpenDocItem> remaining = selected.Skip(i + 1).ToList();
                        ProcessDocument(uiApp, item, remaining, viewModel.SyncAndSave, viewModel.CloseDocuments, ref log);
                        BumpProgress();
                    }

                    this.progressBar.Dispatcher.Invoke((System.Action)(() => this.progressBar.Close()));
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка: " + ex.Message, 4);
                    log += "\nОшибка: " + ex.Message;
                }
                finally
                {
                    CloseProgressBarSafely();
                }
                new InfoWindow400(log).ShowDialog();
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

        private static void CollectOpenDocuments(Autodesk.Revit.ApplicationServices.Application rvtApp, ZakryvashkaViewModel viewModel)
        {
            foreach (Document document in rvtApp.Documents)
            {
                if (document == null || !document.IsValidObject || document.IsLinked)
                    continue;

                string name = GetDocumentDisplayName(document);
                var item = new OpenDocItem(document, name);
                if (document.IsFamilyDocument)
                    viewModel.AddFamily(item);
                else
                    viewModel.AddModel(item);
            }
        }

        private static string GetDocumentDisplayName(Document document)
        {
            string name = document.Title;
            if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrEmpty(document.PathName))
                name = Path.GetFileNameWithoutExtension(document.PathName);
            if (string.IsNullOrWhiteSpace(name))
                name = "Без имени";
            return name;
        }

        private static void ProcessDocument(
            UIApplication uiApp,
            OpenDocItem item,
            List<OpenDocItem> remaining,
            bool syncAndSave,
            bool closeDocument,
            ref string log)
        {
            Document document = item.Document;
            string name = item.Name;
            try
            {
                if (document == null || !document.IsValidObject)
                {
                    log += "\n" + name + " - ошибка: документ уже закрыт";
                    Logger.Log(name + " - документ уже закрыт", 4);
                    return;
                }

                if (!TryActivate(uiApp, document))
                    throw new InvalidOperationException("не удалось сделать документ активным");

                List<string> done = new List<string>();

                if (syncAndSave)
                {
                    if (document.IsReadOnlyFile)
                        throw new InvalidOperationException("файл на диске только для чтения");

                    if (CanSynchronize(document))
                    {
                        TransactWithCentralOptions twc = new TransactWithCentralOptions();
                        SynchronizeWithCentralOptions syncOpts = new SynchronizeWithCentralOptions();
                        RelinquishOptions relinq = new RelinquishOptions(true);
                        syncOpts.SetRelinquishOptions(relinq);
                        syncOpts.SaveLocalBefore = true;
                        syncOpts.SaveLocalAfter = true;
                        syncOpts.Comment = "TNov Закрывашка";
                        document.SynchronizeWithCentral(twc, syncOpts);
                        done.Add("синхронизирована");
                        Logger.Log(name + " - синхронизирована", 1);
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(document.PathName))
                            throw new InvalidOperationException("файл ещё не сохранён на диск");
                        document.Save();
                        done.Add("сохранена");
                        Logger.Log(name + " - сохранена", 1);
                    }
                }

                if (closeDocument)
                {
                    Document next = FindDocumentToActivate(uiApp, document, remaining);
                    if (next == null)
                    {
                        Logger.Log(name + " - нельзя закрыть: это последний открытый документ", 2);
                        if (done.Count > 0)
                            log += "\n" + name + " - " + string.Join(" и ", done) + ", не закрыта (последний открытый документ)";
                        else
                            log += "\n" + name + " - не закрыта (последний открытый документ)";
                        return;
                    }

                    if (!TryActivate(uiApp, next))
                        throw new InvalidOperationException("не удалось переключить активный документ перед закрытием");

                    document.Close(false);
                    done.Add("закрыта");
                    Logger.Log(name + " - закрыта", 1);
                }

                if (done.Count > 0)
                    log += "\n" + name + " - " + string.Join(" и ", done);
            }
            catch (Exception ex)
            {
                string error = FormatDocumentError(ex.Message);
                log += "\n" + name + " - ошибка: " + error;
                Logger.Log("Ошибка (" + name + "): " + error, 4);
            }
        }

        private static bool TryActivate(UIApplication uiApp, Document document)
        {
            if (document == null || !document.IsValidObject)
                return false;

            Document active = uiApp.ActiveUIDocument != null ? uiApp.ActiveUIDocument.Document : null;
            if (IsSameDocument(active, document))
                return true;

            if (string.IsNullOrEmpty(document.PathName))
                return false;

            uiApp.OpenAndActivateDocument(document.PathName);
            return true;
        }

        private static Document FindDocumentToActivate(UIApplication uiApp, Document current, List<OpenDocItem> remaining)
        {
            foreach (OpenDocItem item in remaining)
            {
                if (item.Document != null && item.Document.IsValidObject && !IsSameDocument(item.Document, current))
                    return item.Document;
            }

            foreach (Document document in uiApp.Application.Documents)
            {
                if (document == null || !document.IsValidObject || document.IsLinked)
                    continue;
                if (IsSameDocument(document, current))
                    continue;
                return document;
            }

            return null;
        }

        private static bool IsSameDocument(Document a, Document b)
        {
            if (a == null || b == null)
                return false;
            if (ReferenceEquals(a, b))
                return true;
            try
            {
                if (!a.IsValidObject || !b.IsValidObject)
                    return false;
                if (!string.IsNullOrEmpty(a.PathName) && !string.IsNullOrEmpty(b.PathName))
                    return string.Equals(a.PathName, b.PathName, StringComparison.OrdinalIgnoreCase);
                return string.Equals(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string FormatDocumentError(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "неизвестная ошибка";
            if (message.IndexOf("active document may not be closed", StringComparison.OrdinalIgnoreCase) >= 0)
                return "активный документ нельзя закрыть через API";
            if (message.IndexOf("read-only", StringComparison.OrdinalIgnoreCase) >= 0)
                return "документ доступен только для чтения";
            return message;
        }

        private static bool CanSynchronize(Document document)
        {
            try
            {
                return document.IsWorkshared && !document.IsDetached;
            }
            catch
            {
                return false;
            }
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
