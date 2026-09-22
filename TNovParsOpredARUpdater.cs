using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using TNovCommon;

namespace TNovBIMUtils
{
    public class TNovParsOpredARUpdater : IUpdater
    {
        private const string UpdaterName = "TNovParsOpredARUpdater";

        private static AddInId m_appId;
        private static UpdaterId m_updaterId;

        //параметры
        Guid TOprParamGuid = new Guid("7b538440-ae96-4e43-9dbb-4d35be82eb9c"); //Т_Определение
        Guid TPolozhParamGuid = new Guid("7d68b956-732c-4da9-99a8-13be56ccaf94"); //Т_Положение
        Guid NaimKParamGuid = new Guid("f194bf60-b880-4217-b793-1e0c30dda5e9"); //Наим краткое
        Guid NTParamsNotSetParamGuid = new Guid("70879f6b-b838-49de-8ff5-35e1c7d97e0c");

        public TNovParsOpredARUpdater(AddInId id)
        {
            m_appId = id;
            m_updaterId = new UpdaterId(m_appId, new Guid("b22b0b8f-1b90-4f28-845b-1c24a9fcdcaf"));
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
            if (!(docName.Contains("-АР") || docName.Contains("_АР") || docName.Contains("-АР-")
                || docName.Contains("_ПОФ") || docName.Contains("-ПОФ-"))) return;

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

            Parameter param = UpdaterUtils.GetWritableParam(elem, TOprParamGuid); //Т_Определение
            if (param == null) return;

            Category category = elem.Category;
            if (category == null) return;
            long catId = UpdaterUtils.IdValue(category.Id);

            string value = "";
            string gmValue = GetGMValue(doc, elem);
            string type = GetTypeName(doc, elem);

            //лестницы и вложенные лестниц
            if (catId == -2000919 || catId == -2000920 || catId == -2000123 || catId == -2000120)
            {
                UpdaterUtils.TrySetString(param, "Лестница");
                return;
            }
            //ограждения
            if (catId == -2000126)
            {
                value = "Ограждение";
                if (gmValue.Contains("алкон") || gmValue.Contains("Балк") || gmValue.Contains("Лодж") || gmValue.Contains("лодж") ||
                    type.Contains("алкон") || type.Contains("Балк") || type.Contains("Лодж") || type.Contains("лодж")) value = "Фасад";
                if (elem is FamilyInstance familyInstance1)
                {
                    if (FamilyName(familyInstance1).Contains("Окно")) value = "Фасад";
                }
                if (value.Length > 0)
                {
                    UpdaterUtils.TrySetString(param, value);
                    return;
                }
            }
            //потолки
            if (catId == -2000038)
            {
                if (gmValue.Contains("Фасад") || type.StartsWith("Фасад") || gmValue.Contains("одшивка")
                    || type.Contains("Композ") || type.Contains("озырек") || type.Contains("озырьк"))
                    value = "Фасад навесной";
                else value = "Отделка";
                if (value.Length > 0)
                {
                    UpdaterUtils.TrySetString(param, value);
                    return;
                }
            }
            //перекрытия
            if (catId == -2000032)
            {
                if (gmValue.Contains("Пол") || type.StartsWith("Пол") || ElementName(elem).StartsWith("Пол")) value = "Отделка";
                else if (gmValue.Contains("Кровл") || type.StartsWith("Кровл")) value = "Кровля";
                if (value.Length > 0)
                {
                    UpdaterUtils.TrySetString(param, value);
                    return;
                }
            }
            //ребра плит
            if (catId == -2001392)
            {
                UpdaterUtils.TrySetString(param, "Элемент фасонный");
                return;
            }
            //стены
            if (catId == -2000011 && elem is Wall wall)
            {
                if (wall.CurtainGrid != null)//витражи
                {
                    if (type.Contains("алкон") || gmValue.Contains("алкон")) value = "Витраж холодный";
                    if (type.Contains("олодн") || gmValue.Contains("олодн")) value = "Витраж холодный";
                    if (type.Contains("ермоиз") || gmValue.Contains("ермоиз") || type.Contains("еплый") || gmValue.Contains("еплый"))
                        value = "Витраж теплый";
                }
                else
                {
                    if (gmValue.Contains("Фасад"))
                    {
                        if (type.Contains("Кирп") || type.Contains("кирп") || type.Contains("Пенопл") || type.Contains("пенопл") || type.Contains("Мембр") || type.Contains("блок"))
                            value = "Стена наружная";
                        else if (type.Contains("Шт") || type.Contains("шт") || type.Contains("раска")
                            || type.Contains("ГИ") || type.Contains("идроиз"))
                            value = "Фасад мокрый";
                        else value = "Фасад навесной";
                        if (type.Contains("Хриз") || type.Contains("хриз")) value = "Кровля";
                    }
                    else if (gmValue.Contains("Вент"))
                    {
                        if (type.Contains("Кирп") || type.Contains("кирп")) value = "Стена наружная";
                        if (type.Contains("Хриз") || type.Contains("хриз")) value = "Кровля";
                    }
                    else if (gmValue.Contains("Перег")) value = "Стена внутренняя";
                    else if (gmValue.Contains("аруж")) value = "Стена наружная";
                    else if (gmValue.Contains("Отделка")) value = "Отделка";
                    if (type.Contains("ГКЛ") || type.Contains("ГВЛ") || type.Contains("борд")) value = "Отделка";
                }
                if (value.Length > 0)
                {
                    UpdaterUtils.TrySetString(param, value);
                    return;
                }
            }
            //устройства вызова
            if (catId == -2008077)
            {
                UpdaterUtils.TrySetString(param, "Фасад");
                return;
            }
            //общие правила для оставшихся элементов - по имени семейства и далее
            if (elem is FamilyInstance familyInstance)
            {
                string family = FamilyName(familyInstance);
                if (family.Contains("pmN.Откос кирпичный")) value = "Стена наружная";
                if (family.Contains("pmN.Пол")) value = "Отделка";
                if (family.Contains("Лифт") || family.Contains("Эскалатор") || family.Contains("одъемник")) value = "Лифт, подъемник, эскалатор";
                if (family.Contains("Вент")) value = "Блок вентиляционный";
                if (family.Contains("Люк")) value = "Люк кровельный";
                if (family.Contains("Аэратор")) value = "Аэратор";
                if (family.Contains("Лестн")) value = "Лестница";
                if (family.Contains("Водосток")) value = "Кровля";
                if (family.Contains("Козырек") || family.Contains("Корзина")) value = "Фасад";
                if (family.Contains("Перем")) value = "Перемычка";
                if (family.Contains("Окно") && family.Contains("Проем") == false)
                {
                    if (family.Contains("Балк") || family.Contains("балк") || type.Contains("Балк") || type.Contains("балк")) value = "Блок балконный";
                    else if (gmValue.Contains("Отлив")) value = "Отлив";
                    else if (gmValue.Contains("Откос")) value = "Откос";
                    else if (gmValue.Contains("Наличник")) value = "Откос";
                    else
                    {
                        string naimKvalue = GetTextParamValue(doc, elem, NaimKParamGuid);//Наим краткое
                        if (naimKvalue.Contains("Б-П")) value = "Блок балконный";
                        else value = "Блок оконный";
                    }
                }
                if (family.Contains("Проем.Решетка")) value = "Фасад";
                if (family.Contains("Дверь") && family.Contains("Проем") == false)
                {
                    if (catId == -2000014) { }
                    else if (type.Contains("Полотно")) { }
                    else if (type.Contains("Ручка")) { }
                    else if (type.Contains("Откос")) { }
                    else value = "Блок дверной";

                    //Т_Положение
                    Parameter polozh = UpdaterUtils.GetWritableParam(elem, TPolozhParamGuid);
                    if (type.Contains("Вход") || type.Contains("вход"))
                        UpdaterUtils.TrySetString(polozh, "Входные группы");
                    else
                        UpdaterUtils.TrySetString(polozh, "");
                }
                if (family.Contains("Ворота")) value = "Ворота";
                if (value.Length > 0)
                {
                    UpdaterUtils.TrySetString(param, value);
                    return;
                }
            }
            //общие правила для оставшихся элементов - по группе модели и типу
            if (gmValue.Contains("Фасад") || type.Contains("Фасад")) value = "Фасад";
            if (gmValue.Contains("аруж") || type.Contains("аруж")) value = "Стена наружная";
            if (gmValue.Contains("Перег") || type.Contains("Перег")) value = "Стена внутренняя";
            if (gmValue.Contains("Отделка") || type.Contains("Отделка") ||
                gmValue.StartsWith("Пол") || type.StartsWith("Пол")
                || gmValue.StartsWith("Потолок") || type.StartsWith("Потолок")) value = "Отделка";
            if (gmValue.Contains("Кровля") || type.Contains("Кровля")) value = "Кровля";
            if (gmValue.Contains("Огражд") || type.Contains("Огражд")) value = "Ограждение";
            if (gmValue.Contains("Лестн") || type.Contains("Лестн")) value = "Лестница";
            if (gmValue.Contains("Откос") || type.Contains("Откос")) value = "Откос";

            UpdaterUtils.TrySetString(param, value);
        }

        /// <summary>Группа модели типоразмера, всегда не null (у системных типов параметра нет).</summary>
        String GetGMValue(in Document doc, in Element elem)
        {
            Element type = UpdaterUtils.GetElementType(doc, elem);
            if (type == null) return "";
            return UpdaterUtils.GetStringSafe(type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL));
        }

        String GetTypeName(in Document doc, in Element elem)
        {
            Element type = UpdaterUtils.GetElementType(doc, elem);
            if (type == null) return "";
            try { return type.Name ?? ""; }
            catch { return ""; }
        }

        String GetTextParamValue(in Document doc, in Element elem, in Guid paramGuid)
        {
            // значение с экземпляра, при отсутствии параметра - с типоразмера
            return Param.GetStringParamValue(doc, paramGuid, elem) ?? "";
        }

        private static string FamilyName(FamilyInstance instance)
        {
            try
            {
                FamilySymbol symbol = instance == null ? null : instance.Symbol;
                Family family = symbol == null ? null : symbol.Family;
                return family == null ? "" : (family.Name ?? "");
            }
            catch { return ""; }
        }

        private static string ElementName(Element elem)
        {
            try { return elem.Name ?? ""; }
            catch { return ""; }
        }

        public string GetAdditionalInformation() => "Обновляет параметр Т_Определение у элементов АР";
        public ChangePriority GetChangePriority() => ChangePriority.FloorsRoofsStructuralWalls;
        public UpdaterId GetUpdaterId() => m_updaterId;
        public string GetUpdaterName() => UpdaterName;
    }
}
