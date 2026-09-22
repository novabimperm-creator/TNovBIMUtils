using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using System;
using System.Collections.Generic;
using System.IO;
using TNovCommon;

namespace TNovBIMUtils
{
    [Transaction(TransactionMode.Manual)]
    public class TNovInsulationUpdater : IUpdater
    {
        private const string UpdaterName = "TNovInsulationUpdater";

        // категории
        const long CatPipeInsulation = -2008122;
        const long CatDuctInsulation = -2008123;
        const long CatDuctLining = -2008124;
        const long CatPipeFitting = -2008049;
        const long CatPipeAccessory = -2008055;
        const long CatPipe = -2008044;
        const long CatDuctFitting = -2008010;
        const long CatDuctAccessory = -2008016;
        const long CatDuct = -2008000;

        /// <summary>Как часто перепроверять доступность серверной папки.</summary>
        static readonly TimeSpan ServerCheckInterval = TimeSpan.FromSeconds(30);
        static DateTime _serverCheckedUtc = DateTime.MinValue;
        static bool _serverAvailable;

        static AddInId _appId;
        static UpdaterId _updaterId;

        public TNovInsulationUpdater(AddInId id)
        {
            _appId = id;

            _updaterId = new UpdaterId(_appId, new Guid("3b34b8b6-bbb0-4be9-b7e3-269b3f22f9c0"));
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

            //проверка подключения к серверу
            if (!IsServerAvailable()) return;

            var ids = new HashSet<ElementId>();
            ICollection<ElementId> addedIds = data.GetAddedElementIds();
            if (addedIds != null) ids.UnionWith(addedIds);
            ICollection<ElementId> modifiedIds = data.GetModifiedElementIds();
            if (modifiedIds != null) ids.UnionWith(modifiedIds);

            if (ids.Count == 0) return;

            foreach (ElementId id in ids)
            {
                // Сбой на одном элементе не должен ронять обработку остальных
                try
                {
                    ProcessElement(doc, id);
                }
                catch (Exception ex)
                {
                    UpdaterDiagnostics.Report(UpdaterName, "элемент " + UpdaterUtils.IdText(id), ex);
                }
            }
        }

        /// <summary>
        /// Доступность серверной папки. Проверяется по времени, а не на каждый вызов:
        /// обращение к недоступной сетевой папке подвешивает Revit.
        /// </summary>
        private static bool IsServerAvailable()
        {
            DateTime nowUtc = DateTime.UtcNow;
            if (nowUtc - _serverCheckedUtc < ServerCheckInterval) return _serverAvailable;
            _serverCheckedUtc = nowUtc;

            try
            {
                TNovConfig config = TNovConfigLoad.LoadConfig();
                if (config == null || string.IsNullOrEmpty(config.ServerPath))
                {
                    _serverAvailable = false;
                    return false;
                }
                _serverAvailable = File.Exists(config.ServerPath + "usage.txt");
            }
            catch
            {
                _serverAvailable = false;
            }
            return _serverAvailable;
        }

        private void ProcessElement(Document doc, ElementId id)
        {
            Element elem = doc.GetElement(id);
            if (elem == null) return;

            long catId = UpdaterUtils.CategoryId(elem);
            if (catId == 0) return;

            string value = "Не определено";

            if (catId == CatPipeInsulation) //изоляция труб PipeInsulation
            {
                PipeInsulation pipeInsulation = elem as PipeInsulation;
                if (pipeInsulation == null) return;
                long hostCatId = HostCategoryId(doc, pipeInsulation.HostElementId);
                if (hostCatId == CatPipeFitting || hostCatId == CatPipeAccessory) value = "Фитинги и арматура труб";
                else if (hostCatId == CatPipe) value = "Трубы";
            }
            else if (catId == CatDuctInsulation) //изоляция возд DuctInsulation
            {
                DuctInsulation ductInsulation = elem as DuctInsulation;
                if (ductInsulation == null) return;
                long hostCatId = HostCategoryId(doc, ductInsulation.HostElementId);
                if (hostCatId == CatDuctFitting || hostCatId == CatDuctAccessory) value = "Фитинги и арматура воздуховодов";
                else if (hostCatId == CatDuct) value = "Воздуховоды";
            }
            else if (catId == CatDuctLining) //внутр изол возд DuctLining
            {
                DuctLining ductLining = elem as DuctLining;
                if (ductLining == null) return;
                long hostCatId = HostCategoryId(doc, ductLining.HostElementId);
                if (hostCatId == CatDuctFitting || hostCatId == CatDuctAccessory) value = "Фитинги и арматура воздуховодов";
                else if (hostCatId == CatDuct) value = "Воздуховоды";
            }

            UpdaterUtils.TrySetString(elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS), value);
        }

        /// <summary>Категория основы изоляции или 0, если основы нет.</summary>
        private static long HostCategoryId(Document doc, ElementId hostId)
        {
            if (hostId == null || UpdaterUtils.IdValue(hostId) == -1) return 0;
            Element host = doc.GetElement(hostId);
            return UpdaterUtils.CategoryId(host);
        }

        public string GetAdditionalInformation()
        {
            return "TNov, bim@pm-nova.ru";
        }

        public ChangePriority GetChangePriority()
        {
            return ChangePriority.FloorsRoofsStructuralWalls;
        }

        public UpdaterId GetUpdaterId()
        {
            return _updaterId;
        }

        public string GetUpdaterName()
        {
            return UpdaterName;
        }
    }
}
