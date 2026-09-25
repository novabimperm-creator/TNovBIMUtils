using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TNovCommon;

namespace TNovBIMUtils
{
    /// <summary>
    /// Общая логика Т_Номер секции для кнопки TParsSection и TNovSectionNumberUpdater.
    /// Источник значения — параметр Т_Номер секции в Сведениях о проекте.
    /// </summary>
    public static class SectionNumberUtils
    {
        public static readonly Guid TSectionParamGuid = new Guid("888d43dc-f115-4880-aac2-3e26d35c492d"); //Т_Номер секции
        public static readonly Guid NTParamsNotSetParamGuid = new Guid("70879f6b-b838-49de-8ff5-35e1c7d97e0c");
        public const string ParamName = "Т_Номер секции";

        /// <summary>Модели АР, КР, КЖ, ОВ, ВК.</summary>
        public static bool IsTargetModel(string docName)
        {
            if (string.IsNullOrEmpty(docName)) return false;
            string[] codes = { "АР", "КР", "КЖ", "ОВ", "ВК" };
            return codes.Any(c => docName.Contains("-" + c) || docName.Contains("_" + c));
        }

        /// <summary>Определение параметра в проекте или null, если параметр в проект не добавлен.</summary>
        public static InternalDefinition GetDefinition(Document doc)
        {
            try
            {
                SharedParameterElement spe = SharedParameterElement.Lookup(doc, TSectionParamGuid);
                return spe == null ? null : spe.GetDefinition();
            }
            catch { return null; }
        }

        public static ElementBinding GetBinding(Document doc, Definition definition)
        {
            if (definition == null) return null;
            try { return doc.ParameterBindings.get_Item(definition) as ElementBinding; }
            catch { return null; }
        }

        public static bool IsBoundToProjectInfo(Document doc, ElementBinding binding)
        {
            if (binding == null || binding.Categories == null) return false;
            Category category = Category.GetCategory(doc, BuiltInCategory.OST_ProjectInformation);
            return category != null && binding.Categories.Contains(category);
        }

        /// <summary>Добавляет параметр к категории Сведения о проекте. Вызывать внутри транзакции.</summary>
        public static bool BindToProjectInfo(Document doc, InternalDefinition definition, ElementBinding binding)
        {
            Category category = Category.GetCategory(doc, BuiltInCategory.OST_ProjectInformation);
            if (category == null || binding == null) return false;
            binding.Categories.Insert(category);
#if R2022
            return doc.ParameterBindings.ReInsert(definition, binding, definition.ParameterGroup);
#else
            return doc.ParameterBindings.ReInsert(definition, binding, definition.GetGroupTypeId());
#endif
        }

        /// <summary>Параметр Т_Номер секции в Сведениях о проекте или null.</summary>
        public static Parameter GetProjectInfoParam(Document doc)
        {
            ProjectInfo projectInfo = doc.ProjectInformation;
            return projectInfo == null ? null : UpdaterUtils.GetParam(projectInfo, TSectionParamGuid);
        }

        /// <summary>Значение из Сведений о проекте; "" — если параметра нет или он пустой.</summary>
        public static string GetProjectValue(Document doc)
        {
            return UpdaterUtils.GetStringSafe(GetProjectInfoParam(doc)).Trim();
        }

        /// <summary>Проверка, что строку можно записать в параметр с его типом данных.</summary>
        public static bool CanConvert(Parameter p, string value)
        {
            if (p == null) return false;
            switch (p.StorageType)
            {
                case StorageType.String: return true;
                case StorageType.Integer: return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case StorageType.Double: return TryParseDouble(value, out _);
                default: return false;
            }
        }

        /// <summary>Запись значения с учетом типа данных параметра (пишет только при отличии).</summary>
        public static bool TrySetValue(Parameter p, string value)
        {
            if (p == null || p.IsReadOnly) return false;
            switch (p.StorageType)
            {
                case StorageType.String:
                    return UpdaterUtils.TrySetString(p, value);
                case StorageType.Integer:
                    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)
                        && UpdaterUtils.TrySetInteger(p, i);
                case StorageType.Double:
                    return TryParseDouble(value, out double d) && UpdaterUtils.TrySetDouble(p, d);
                default:
                    return false;
            }
        }

        /// <summary>Заполняет Т_Номер секции у элемента. Сведения о проекте и элементы с признаком "не назначать" пропускаются.</summary>
        public static bool ApplyToElement(Element elem, string value)
        {
            if (elem == null || elem is ProjectInfo) return false;
            if (UpdaterUtils.IsSkipFlagSet(elem, NTParamsNotSetParamGuid)) return false;
            return TrySetValue(UpdaterUtils.GetWritableParam(elem, TSectionParamGuid), value);
        }

        /// <summary>Экземпляры всех категорий, к которым добавлен параметр (кроме Сведений о проекте).</summary>
        public static IList<Element> CollectElements(Document doc, ElementBinding binding)
        {
            var categoryIds = new List<ElementId>();
            if (binding != null && binding.Categories != null)
            {
                long projectInfoId = (long)BuiltInCategory.OST_ProjectInformation;
                foreach (Category c in binding.Categories)
                {
                    if (c != null && UpdaterUtils.IdValue(c.Id) != projectInfoId) categoryIds.Add(c.Id);
                }
            }
            if (categoryIds.Count == 0) return new List<Element>();

            return new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WherePasses(new ElementMulticategoryFilter(categoryIds))
                .ToElements();
        }

        private static bool TryParseDouble(string value, out double result)
        {
            string s = (value ?? "").Replace(',', '.');
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }
    }
}
