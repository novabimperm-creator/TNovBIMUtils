using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using TNovCommon;

namespace TNovBIMUtils
{
    /// <summary>
    /// Заполняет Т_Номер секции у добавленных/измененных элементов значением из Сведений о проекте.
    /// Не работает, пока параметр не добавлен в Сведения о проекте или его значение пустое
    /// (первичная настройка — кнопкой "Т Номер секции").
    /// </summary>
    public class TNovSectionNumberUpdater : IUpdater
    {
        private const string UpdaterName = "TNovSectionNumberUpdater";

        private static AddInId m_appId;
        private static UpdaterId m_updaterId;

        public TNovSectionNumberUpdater(AddInId id)
        {
            m_appId = id;
            m_updaterId = new UpdaterId(m_appId, new Guid("3f6c2a8e-9d41-4b7a-a5e2-7c1d8b0e4f93"));
        }

        /// <summary>
        /// Точка входа Revit. Наружу не должно вылетать ни одного исключения:
        /// любое исключение из IUpdater.Execute Revit показывает пользователю
        /// с предложением отключить обновитель.
        /// </summary>
        public void Execute(UpdaterData data)
        {
            try
            {
                ExecuteCore(data);
            }
            catch (Exception ex)
            {
                UpdaterDiagnostics.Report(UpdaterName, "Execute", ex);
            }
        }

        private void ExecuteCore(UpdaterData data)
        {
            if (data == null) return;

            Document doc = data.GetDocument();
            if (doc == null || doc.IsFamilyDocument) return;
            if (!SectionNumberUtils.IsTargetModel(doc.Title)) return;

            string value = SectionNumberUtils.GetProjectValue(doc);
            if (value.Length == 0) return;

            var allElementIds = new HashSet<ElementId>();
            ICollection<ElementId> addedIds = data.GetAddedElementIds();
            if (addedIds != null) allElementIds.UnionWith(addedIds);
            ICollection<ElementId> modifiedIds = data.GetModifiedElementIds();
            if (modifiedIds != null) allElementIds.UnionWith(modifiedIds);

            foreach (ElementId elementId in allElementIds)
            {
                // Сбой на одном элементе не должен ронять обработку остальных
                try
                {
                    SectionNumberUtils.ApplyToElement(doc.GetElement(elementId), value);
                }
                catch (Exception ex)
                {
                    UpdaterDiagnostics.Report(UpdaterName, "элемент " + UpdaterUtils.IdText(elementId), ex);
                }
            }
        }

        public string GetAdditionalInformation() => "Заполняет Т_Номер секции у элементов значением из Сведений о проекте";
        public ChangePriority GetChangePriority() => ChangePriority.FloorsRoofsStructuralWalls;
        public UpdaterId GetUpdaterId() => m_updaterId;
        public string GetUpdaterName() => UpdaterName;
    }
}
