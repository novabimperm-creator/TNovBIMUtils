using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using TNovCommon;

namespace TNovBIMUtils
{
    public class TNovParsOpredSTUpdater : IUpdater
    {
        private const string UpdaterName = "TNovParsOpredSTUpdater";

        private static AddInId m_appId;
        private static UpdaterId m_updaterId;

        //параметры
        Guid adskCMarkParamGuid = new Guid("5d369dfb-17a2-4ae2-a1a1-bdfc33ba7405"); //A_Марка конструкции
        Guid TOprParamGuid = new Guid("7b538440-ae96-4e43-9dbb-4d35be82eb9c"); //Т_Определение
        Guid NTParamsNotSetParamGuid = new Guid("70879f6b-b838-49de-8ff5-35e1c7d97e0c");

        public TNovParsOpredSTUpdater(AddInId id)
        {
            m_appId = id;
            m_updaterId = new UpdaterId(m_appId, new Guid("c6acb694-23ab-4df2-8aad-ac2f727b54a7"));
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

            string docName = doc.Title ?? "";
            if (!(docName.Contains("-КЖ") || docName.Contains("_КЖ")
                || docName.Contains("-КР-") || docName.Contains("_КР_"))) return;

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
                    UpdaterDiagnostics.Report(UpdaterName, "элемент " + UpdaterUtils.IdText(elementId), ex);
                }
            }
        }

        private void ProcessElement(Document doc, ElementId elementId)
        {
            Element elem = doc.GetElement(elementId);
            if (elem == null) return;
            if (UpdaterUtils.IsSkipFlagSet(elem, NTParamsNotSetParamGuid)) return;

            Category category = elem.Category;
            if (category == null) return;
            long catId = UpdaterUtils.IdValue(category.Id);

            Parameter opredParam = UpdaterUtils.GetWritableParam(elem, TOprParamGuid); //Т_Определение

            if (catId == -2000919 || catId == -2000920 || catId == -2000123 || catId == -2000120) //лестницы и вложенные лестниц - ускоренное назначение параметра
            {
                UpdaterUtils.TrySetString(opredParam, "Лестница");
                return;
            }
            if (catId == -2000126) //ограждения - ускоренное назначение параметра
            {
                UpdaterUtils.TrySetString(opredParam, "Ограждение");
                return;
            }

            string group = MarkGroup(elem, doc);
            if (!string.IsNullOrEmpty(group))
                UpdaterUtils.TrySetString(opredParam, group);
        }

        String MarkGroup(in Element elem, in Document doc)
        {
            string mark = "-";
            Parameter markParam = UpdaterUtils.GetParam(elem, adskCMarkParamGuid);
            if (markParam != null && markParam.HasValue)
            {
                string markValue = UpdaterUtils.GetStringSafe(markParam);
                if (markValue.Length > 0) mark = markValue;
            }

            string group = "";
            if (mark.StartsWith("Фп") || mark.StartsWith("Рп") || mark.StartsWith("Фм") || mark.StartsWith("Рм") || mark.StartsWith("Рл"))
            {
                group = ParseTypeST(elem, doc, "Фундамент");
            }
            else if (mark.StartsWith("Пл") || mark.StartsWith("Пп"))
            {
                group = ParseTypeST(elem, doc, "Плита перекрытия");
            }
            else if (mark.StartsWith("Пб"))
            {
                group = ParseTypeST(elem, doc, "Плита по грунту");
            }
            else if (mark.StartsWith("Пр"))
            {
                group = ParseTypeST(elem, doc, "Приямок");
            }
            else if (mark.StartsWith("Кл"))
            {
                group = ParseTypeST(elem, doc, "Колонна");
            }
            else if (mark.StartsWith("Пм"))
            {
                group = ParseTypeST(elem, doc, "Пилон");
            }
            else if (mark.StartsWith("Дж") || mark.StartsWith("Мс"))
            {
                group = ParseTypeST(elem, doc, "Стена");
            }
            else if (mark.StartsWith("Бм"))
            {
                group = ParseTypeST(elem, doc, "Балка");
            }
            else if (mark.StartsWith("Лм") || mark.StartsWith("Лп") || mark.StartsWith("Лк"))
            {
                group = ParseTypeST(elem, doc, "Лестница");
            }
            else if (mark.StartsWith("Пт"))
            {
                group = ParseTypeST(elem, doc, "Парапет");
            }
            else // прочие марки (Км и т.д.) либо пустые марки
            {
                long catId = UpdaterUtils.CategoryId(elem);
                if (catId == -2001300) group = ParseTypeST(elem, doc, "Фундамент");
                if (catId == -2000032) group = ParseTypeST(elem, doc, "Плита перекрытия");
                if (catId == -2000011) group = ParseTypeST(elem, doc, "Стена");
                if (catId == -2000120) group = ParseTypeST(elem, doc, "Лестница");
            }

            return group;
        }

        String ParseTypeST(in Element elem, in Document doc, in string OpredValue)
        {
            Element type = UpdaterUtils.GetElementType(doc, elem);
            if (type == null) return "";

            // У системных типоразмеров (стены, перекрытия, фундаменты) параметра
            // "Модель" нет вовсе — тогда разбираем имя типа
            string gm = ModelGroup(type);
            string typeName = type.Name ?? "";

            string group = "";
            //подготовка, термо, гидро, сваи, лестницы, галтели
            if (gm != null) //условие исходя из группы модели
            {
                if (gm.Contains("Подготовка") || gm.Contains("Подбетонка")) group = "Подготовка";
                if (gm.Contains("Термо")) group = "Термовкладыш";
                if (gm.Contains("Свая")) group = "Свая";
                if (gm.Contains("Лестн")) group = "Лестница";
                if (gm.Contains("Галтель")) group = "Фундамент";
            }
            else //альтернативное исходя из имени типа
            {
                if (typeName.Contains("Подготовка") || typeName.Contains("Подбетонка")) group = "Подготовка";
                if (typeName.Contains("Термо")) group = "Термовкладыш";
                if (typeName.Contains("ГИ") || typeName.Contains("Гидроиз")) group = "Гидроизоляция";
                if (typeName.Contains("Фунд")) group = "Фундамент";
            }
            //основная конструкция (бетон)
            if (gm != null) //условие исходя из группы модели
            {
                if (gm.Contains("Бетон")) group = OpredValue;
            }
            else //альтернативное исходя из имени типа
            {
                if (typeName.Contains("Бетон")) group = OpredValue;
            }
            //рампа
            if (typeName.Contains("Рампа") || typeName.Contains("рампа")) group = "Рампа";

            return group;
        }

        /// <summary>Группа модели типоразмера или null, если параметра нет / он пуст.</summary>
        private static string ModelGroup(Element type)
        {
            Parameter p = type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL);
            if (p == null || !p.HasValue) return null;
            return p.AsString();
        }

        public string GetAdditionalInformation() => "Обновляет параметр Т_Определение у элементов КЖ";
        public ChangePriority GetChangePriority() => ChangePriority.FloorsRoofsStructuralWalls;
        public UpdaterId GetUpdaterId() => m_updaterId;
        public string GetUpdaterName() => UpdaterName;
    }
}
