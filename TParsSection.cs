using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using TNovCommon;

namespace TNovBIMUtils
{
    /// <summary>
    /// Т Номер секции: при первом запуске добавляет Т_Номер секции в Сведения о проекте
    /// и запрашивает значение, затем заполняет его у всех элементов категорий, к которым добавлен параметр.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class TParsSection : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Т Номер секции";
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            Document doc = RevitAPI.Document;
            #endregion

            TNovConfig config = TNovConfigLoad.LoadConfig(DBCommandName, TNovVersion); if (config == null) return Result.Failed;
            Logger.Initialize(DBCommandName, dateTime, TNovVersion);

            #region Проверки
            if (doc.IsFamilyDocument || !SectionNumberUtils.IsTargetModel(doc.Title))
            {
                new InfoWindow280("Функция работает только в моделях АР, КР, КЖ, ОВ, ВК.").ShowDialog();
                Logger.Log("Неподходящая модель: " + doc.Title, 3);
                return Result.Cancelled;
            }

            InternalDefinition definition = SectionNumberUtils.GetDefinition(doc);
            ElementBinding binding = SectionNumberUtils.GetBinding(doc, definition);
            if (definition == null || binding == null)
            {
                new InfoWindow280("Параметр " + SectionNumberUtils.ParamName + " не добавлен в проект.").ShowDialog();
                Logger.Log("Параметр не добавлен в проект", 3);
                return Result.Cancelled;
            }
            if (!(binding is InstanceBinding))
            {
                new InfoWindow280("Параметр " + SectionNumberUtils.ParamName + " добавлен в проект как параметр типа. " +
                    "Для заполнения нужен параметр экземпляра.").ShowDialog();
                Logger.Log("Параметр добавлен как параметр типа", 3);
                return Result.Cancelled;
            }

            ProjectInfo projectInfo = doc.ProjectInformation;
            if (doc.IsWorkshared && projectInfo != null &&
                WorksharingUtils.GetCheckoutStatus(doc, projectInfo.Id) == CheckoutStatus.OwnedByOtherUser)
            {
                new InfoWindow280("Сведения о проекте заняты другим пользователем.").ShowDialog();
                Logger.Log("Сведения о проекте заняты другим пользователем", 3);
                return Result.Cancelled;
            }
            #endregion

            #region Значение
            bool bound = SectionNumberUtils.IsBoundToProjectInfo(doc, binding);
            string value = bound ? SectionNumberUtils.GetProjectValue(doc) : "";
            bool enteredByUser = false;
            if (value.Length == 0)
            {
                var dialog = new SectionNumberWPF("");
                if (dialog.ShowDialog() != true)
                {
                    Logger.Log("Ввод номера секции отменен", 1);
                    return Result.Cancelled;
                }
                value = dialog.Value;
                enteredByUser = true;
            }
            Logger.Log("Номер секции: " + value, 1);
            #endregion

            #region Заполнение
            int count = 0, busy = 0;
            using (Transaction transaction = new Transaction(doc, "TNov - Т Номер секции"))
            {
                try
                {
                    transaction.Start();

                    if (!bound)
                    {
                        if (!SectionNumberUtils.BindToProjectInfo(doc, definition, binding))
                            throw new InvalidOperationException("не удалось добавить параметр в Сведения о проекте");
                        Logger.Log("Параметр добавлен в Сведения о проекте", 1);
                    }

                    if (enteredByUser)
                    {
                        Parameter projectParam = SectionNumberUtils.GetProjectInfoParam(doc);
                        if (!SectionNumberUtils.CanConvert(projectParam, value))
                        {
                            transaction.RollBack();
                            new InfoWindow280("Значение \"" + value + "\" не подходит для типа данных параметра " +
                                SectionNumberUtils.ParamName + ".").ShowDialog();
                            Logger.Log("Значение не подходит для типа данных параметра", 3);
                            return Result.Cancelled;
                        }
                        SectionNumberUtils.TrySetValue(projectParam, value);
                    }

                    IList<Element> targets = SectionNumberUtils.CollectElements(doc, binding);
                    foreach (Element elem in targets)
                    {
                        if (doc.IsWorkshared &&
                            WorksharingUtils.GetCheckoutStatus(doc, elem.Id) == CheckoutStatus.OwnedByOtherUser)
                        {
                            busy++; continue;
                        }
                        if (SectionNumberUtils.ApplyToElement(elem, value)) count++;
                    }

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    if (transaction.HasStarted() && !transaction.HasEnded()) transaction.RollBack();
                    Logger.Log("Ошибка: " + ex.Message, 4);
                    new InfoWindow280("Ошибка: " + ex.Message).ShowDialog();
                    return Result.Failed;
                }
            }
            #endregion

            string report = "Номер секции: " + value + "\nЗаполнено элементов: " + count;
            if (busy > 0) report += "\nПропущено (заняты другими пользователями): " + busy;
            new InfoWindow280(report).ShowDialog();
            Logger.Log(report.Replace("\n", "; "), 1);
            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }
    }
}
