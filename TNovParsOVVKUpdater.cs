using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TNovCommon;

namespace TNovBIMUtils
{
    public class TNovParsOVVKUpdater : IUpdater
    {
        private const string UpdaterName = "TNovParsOVVKUpdater";

        private static AddInId m_appId;
        private static UpdaterId m_updaterId;

        //параметры
        static readonly Guid adskGparamGuid = new Guid("3de5f1a4-d560-4fa8-a74f-25d250fb3401");//ADSK_Группирование
        static readonly Guid TSystemNameParamGuid = new Guid("e4cd1559-649f-4a24-9782-b1840b41773f");//Т_Имя системы
        static readonly Guid adskNparamGuid = new Guid("e6e0f5cd-3e26-485b-9342-23882b20eb43");//ADSK_Наименование
        static readonly Guid TNaimParamGuid = new Guid("cc50c492-9220-45fa-97fa-a2611b3696e7");//Т_Наименование
        static readonly Guid adskMarkparamGuid = new Guid("2204049c-d557-4dfc-8d70-13f19715e46d");//ADSK_Марка
        static readonly Guid TOboznParamGuid = new Guid("992bd635-f80c-4380-a978-f8ac3bc5a111");//Т_Обозначение
        static readonly Guid adskManufparamGuid = new Guid("a8cdbf7b-d60a-485e-a520-447d2055f351");//ADSK_Завод-изготовитель
        static readonly Guid TManufParamGuid = new Guid("2fcb084c-f1bc-473b-9e88-9f9b304254e1");//Т_Завод-изготовитель
        static readonly Guid adskEdParamGuid = new Guid("4289cb19-9517-45de-9c02-5a74ebf5c86d");//ADSK_Единица измерения
        static readonly Guid TEdParamGuid = new Guid("9486acdc-ed8e-482e-aa18-b518aaf08a94");//Т_Единица измерения
        static readonly Guid adskCparamGuid = new Guid("8d057bb3-6ccd-4655-9165-55526691fe3a");//ADSK_Количество
        static readonly Guid TCountParamGuid = new Guid("b3f5d47f-d1cf-4ac4-9a38-a27b5204e16c");//Т_Количество
        static readonly Guid adskTstParamGuid = new Guid("381b467b-3518-42bb-b183-35169c9bdfb3");//ADSK_Толщина стенки
        static readonly Guid TStParamGuid = new Guid("021340dc-4952-4429-b3a9-20ca2a308d92");//Т_Толщина стенки
        static readonly Guid TDimsParamGuid = new Guid("f45c49d7-c46f-418c-948e-d4cde7ea6772");//Т_Размер
        static readonly Guid TDiamParamGuid = new Guid("e955e814-e8de-404b-aba7-0cfe10120aff");//Т_Диаметр
        static readonly Guid NTParamsNotSetParamGuid = new Guid("70879f6b-b838-49de-8ff5-35e1c7d97e0c");

        //категории, у которых заполняется Т_Размер
        const int CatDuctCurves = -2008000;
        const int CatDuctFitting = -2008010;
        const int CatFlexDuctCurves = -2008013;
        const int CatDuctAccessory = -2008016;

        public TNovParsOVVKUpdater(AddInId id)
        {
            m_appId = id;
            m_updaterId = new UpdaterId(m_appId, new Guid("64242259-e3df-41fd-a922-e4be29a5c339"));
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
            if (!IsTargetDocument(doc.Title)) return;

            var allElementIds = new HashSet<ElementId>();
            ICollection<ElementId> addedIds = data.GetAddedElementIds();
            if (addedIds != null) allElementIds.UnionWith(addedIds);
            ICollection<ElementId> modifiedIds = data.GetModifiedElementIds();
            if (modifiedIds != null) allElementIds.UnionWith(modifiedIds);

            if (allElementIds.Count == 0) return;

            foreach (ElementId elementId in allElementIds)
            {
                // Сбой на одном элементе не должен ронять обработку остальных
                try
                {
                    ProcessElement(doc, elementId);
                }
                catch (Exception ex)
                {
                    UpdaterDiagnostics.Report(UpdaterName, "элемент " + IdText(elementId), ex);
                }
            }
        }

        private static bool IsTargetDocument(string docName)
        {
            if (string.IsNullOrEmpty(docName)) return false;
            return docName.Contains("-ВК") || docName.Contains("_ВК")
                || docName.Contains("-ПТ") || docName.Contains("_ПТ")
                || docName.Contains("-ОВ") || docName.Contains("_ОВ")
                || docName.Contains("-ТС") || docName.Contains("_ТС");
        }

        private void ProcessElement(Document doc, ElementId elementId)
        {
            Element elem = doc.GetElement(elementId);
            if (elem == null) return;

            Category category = elem.Category;
            if (category == null) return;

            // Все общие параметры элемента забираем один раз: и быстрее,
            // и исключает рассинхрон "параметр есть" / get_Parameter() == null
            Dictionary<Guid, Parameter> shared = GetSharedParameters(elem);
            if (shared.Count == 0) return;

            if (IsSkipFlagSet(FindShared(shared, NTParamsNotSetParamGuid))) return;

            int categoryId = CategoryId(category);

            //Т_Количество
            Parameter pCount = GetWritable(shared, TCountParamGuid);
            if (pCount != null)
                SetDouble(pCount, Param.GetDoubleParamValue(doc, adskCparamGuid, elem));

            //Т_Обозначение
            Parameter pObozn = GetWritable(shared, TOboznParamGuid);
            if (pObozn != null)
                SetString(pObozn, Param.GetStringParamValue(doc, adskMarkparamGuid, elem));

            //Т_Наименование
            Parameter pNaim = GetWritable(shared, TNaimParamGuid);
            if (pNaim != null)
                SetString(pNaim, Param.GetStringParamValue(doc, adskNparamGuid, elem));

            //Т_Размер (воздуховоды, фитинги, гибкие воздуховоды, арматура воздуховодов)
            if (IsDuctCategory(categoryId))
            {
                Parameter pDims = GetWritable(shared, TDimsParamGuid);
                if (pDims != null)
                    SetString(pDims, Param.GetStringParamValue(doc, BuiltInParameter.RBS_CALCULATED_SIZE, elem));
            }

            //Т_Завод-изготовитель
            Parameter pManuf = GetWritable(shared, TManufParamGuid);
            if (pManuf != null)
                SetString(pManuf, Param.GetStringParamValue(doc, adskManufparamGuid, elem));

            //Т_Единица измерения
            Parameter pEd = GetWritable(shared, TEdParamGuid);
            if (pEd != null)
                SetString(pEd, Param.GetStringParamValue(doc, adskEdParamGuid, elem));

            //Т_Имя системы
            Parameter pSystem = GetWritable(shared, TSystemNameParamGuid);
            if (pSystem != null)
                SetString(pSystem, Param.GetStringParamValue(doc, adskGparamGuid, elem));

            //Т_Толщина стенки
            Parameter pSt = GetWritable(shared, TStParamGuid);
            if (pSt != null)
            {
                double stMm = Param.GetDoubleParamValue(doc, adskTstParamGuid, elem) * 304.8;
                SetString(pSt, stMm.ToString(CultureInfo.InvariantCulture));
            }

            //Т_Диаметр
            if (TDiameterResolver.IsTargetCategory(categoryId))
            {
                Parameter pDiam = GetWritable(shared, TDiamParamGuid);
                if (pDiam != null)
                {
                    try { TDiameterResolver.Apply(doc, elem, pDiam); }
                    catch (Exception ex) { UpdaterDiagnostics.Report(UpdaterName, "Т_Диаметр " + IdText(elem.Id), ex); }
                }
            }
        }

        private static bool IsDuctCategory(int categoryId)
        {
            return categoryId == CatDuctCurves
                || categoryId == CatDuctFitting
                || categoryId == CatFlexDuctCurves
                || categoryId == CatDuctAccessory;
        }

        /// <summary>Общие параметры экземпляра, сопоставленные с GUID.</summary>
        private static Dictionary<Guid, Parameter> GetSharedParameters(Element elem)
        {
            var map = new Dictionary<Guid, Parameter>();
            try
            {
                foreach (Parameter p in elem.ParametersMap)
                {
                    if (p == null || !p.IsShared) continue;
                    Guid guid = p.GUID;
                    if (!map.ContainsKey(guid)) map.Add(guid, p);
                }
            }
            catch (Exception ex)
            {
                UpdaterDiagnostics.Report(UpdaterName, "ParametersMap " + IdText(elem.Id), ex);
            }
            return map;
        }

        private static Parameter FindShared(Dictionary<Guid, Parameter> map, Guid guid)
        {
            Parameter p;
            return map.TryGetValue(guid, out p) ? p : null;
        }

        private static Parameter GetWritable(Dictionary<Guid, Parameter> map, Guid guid)
        {
            Parameter p = FindShared(map, guid);
            return p != null && !p.IsReadOnly ? p : null;
        }

        /// <summary>Признак "Т параметры не заполнять".</summary>
        private static bool IsSkipFlagSet(Parameter p)
        {
            if (p == null || !p.HasValue) return false;
            try
            {
                if (p.StorageType == StorageType.Integer) return p.AsInteger() == 1;
                if (p.StorageType == StorageType.Double) return Math.Abs(p.AsDouble() - 1.0) < 1e-9;
            }
            catch { }
            return false;
        }

        private static void SetString(Parameter p, string value)
        {
            if (p == null || value == null) return;
            if (p.StorageType != StorageType.String) return;
            try
            {
                // Запись того же значения всё равно помечает элемент изменённым
                // и повторно запускает обновители — пишем только при отличии
                if (string.Equals(p.AsString(), value, StringComparison.Ordinal)) return;
                p.Set(value);
            }
            catch (Exception ex)
            {
                UpdaterDiagnostics.Report(UpdaterName, "Set(string)", ex);
            }
        }

        private static void SetDouble(Parameter p, double value)
        {
            if (p == null) return;
            if (p.StorageType != StorageType.Double) return;
            try
            {
                if (p.HasValue && Math.Abs(p.AsDouble() - value) < 1e-9) return;
                p.Set(value);
            }
            catch (Exception ex)
            {
                UpdaterDiagnostics.Report(UpdaterName, "Set(double)", ex);
            }
        }

        private static int CategoryId(Category category)
        {
#if R2022
            return category.Id.IntegerValue;
#else
            return (int)category.Id.Value;
#endif
        }

        private static string IdText(ElementId id)
        {
            if (id == null) return "<null>";
#if R2022
            return id.IntegerValue.ToString(CultureInfo.InvariantCulture);
#else
            return id.Value.ToString(CultureInfo.InvariantCulture);
#endif
        }

        public string GetAdditionalInformation() => "Обновляет Т параметры у элементов ОВ ВК";
        public ChangePriority GetChangePriority() => ChangePriority.FloorsRoofsStructuralWalls;
        public UpdaterId GetUpdaterId() => m_updaterId;
        public string GetUpdaterName() => UpdaterName;
    }
}
