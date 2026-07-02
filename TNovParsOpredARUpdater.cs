using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TNovCommon;

namespace TNovBIMUtils
{
    public class TNovParsOpredARUpdater : IUpdater
    {
        private static AddInId m_appId;
        private static UpdaterId m_updaterId;

        //параметры
        Guid TOprParamGuid = new Guid("7b538440-ae96-4e43-9dbb-4d35be82eb9c"); //Т_Определение
        Guid NTParamsNotSetParamGuid = new Guid("70879f6b-b838-49de-8ff5-35e1c7d97e0c");

        public TNovParsOpredARUpdater(AddInId id)
        {
            m_appId = id;
            m_updaterId = new UpdaterId(m_appId, new Guid("b22b0b8f-1b90-4f28-845b-1c24a9fcdcaf"));
        }
        public void Execute(UpdaterData data)
        {
            Document doc = data.GetDocument();
            ICollection<ElementId> addedIds = data.GetAddedElementIds();
            ICollection<ElementId> modifiedIds = data.GetModifiedElementIds();

            // Объединяем все измененные элементы
            var allElementIds = new HashSet<ElementId>(addedIds);
            allElementIds.UnionWith(modifiedIds);

            if (!allElementIds.Any()) return;

            string docName = doc.Title.ToString();
            if (docName.Contains("-АР") || docName.Contains("_АР") || docName.Contains("-АР-") || docName.Contains("_ПОФ") || docName.Contains("-ПОФ-"))
            {
                foreach (ElementId elementId in allElementIds)
                {
                    Element elem = doc.GetElement(elementId);
                    if (elem == null) continue;
                    if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, elem) && elem.get_Parameter(NTParamsNotSetParamGuid).AsDouble() == 1) continue;

                    string value = "";

                    if (Param.ParamExistByGuid(TOprParamGuid, elem) && elem.get_Parameter(TOprParamGuid).IsReadOnly == false)
                    {
                        Parameter param = elem.get_Parameter(TOprParamGuid); //Т_Определение

                        string gmValue = GetGMValue(doc, elem); string type = GetTypeName(doc, elem);
#if R2022
                        long catId = elem.Category.Id.IntegerValue;
#else
                        long catId = elem.Category.Id.Value;
#endif
                        

                        //лестницы и вложенные лестниц
                        if (catId == -2000919 || catId == -2000920 || catId == -2000123 || catId == -2000120)
                        {
                            try { param.Set("Лестница"); } catch { }
                            continue;
                        }
                        //ограждения
                        if (catId == -2000126)
                        {
                            value = "Ограждение";
                            if (gmValue.Contains("алкон") || gmValue.Contains("Балк") || gmValue.Contains("Лодж") || gmValue.Contains("лодж") ||
                                type.Contains("алкон") || type.Contains("Балк") || type.Contains("Лодж") || type.Contains("лодж")) value = "Фасад";
                            if (elem is FamilyInstance familyInstance1)
                            {
                                FamilySymbol symbol = familyInstance1.Symbol;
                                if (symbol.Family.Name.Contains("Окно")) value = "Фасад";
                            }
                            if (value.Length > 0)
                            {
                                try{param.Set(value); } catch { }
                                continue;
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
                                try{param.Set(value); } catch { }
                                continue;
                            }
                        }
                        //перекрытия
                        if (catId == -2000032)
                        {
                            if (gmValue.Contains("Пол") || type.StartsWith("Пол") || elem.Name.StartsWith("Пол")) value = "Отделка";
                            else if (gmValue.Contains("Кровл") || type.StartsWith("Кровл")) value = "Кровля";
                            if (value.Length > 0)
                            {
                                try{param.Set(value); } catch { }
                                continue;
                            }
                        }
                        //ребра плит
                        if (catId == -2001392)
                        {
                            try{param.Set("Элемент фасонный"); } catch { }
                            continue;
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
                                try{param.Set(value); } catch { }
                                continue;
                            }
                        }
                        //устройства вызова
                        if (catId == -2008077)
                        {
                            try{param.Set("Фасад"); } catch { }
                            continue;
                        }
                        //общие правила для оставшихся элементов - по имени семейства и далее
                        if (elem is FamilyInstance familyInstance)
                        {
                            FamilySymbol symbol = familyInstance.Symbol;
                            string family = symbol.Family.Name;
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
                                    string naimKvalue = GetTextParamValue(doc, elem, new Guid("f194bf60-b880-4217-b793-1e0c30dda5e9"));//Наим краткое
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


                                if (type.Contains("Вход") || type.Contains("вход"))
                                {
                                    elem.get_Parameter(new Guid("7d68b956-732c-4da9-99a8-13be56ccaf94"))?.Set("Входные группы"); //Т_Положение
                                }
                                else elem.get_Parameter(new Guid("7d68b956-732c-4da9-99a8-13be56ccaf94"))?.Set("");

                            }
                            if (family.Contains("Ворота")) value = "Ворота";
                            if (value.Length > 0)
                            {
                                try{param.Set(value); } catch { }
                                continue;
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

                        try{param.Set(value); } catch { }

                    }
                }
            }

            
        }
        String GetGMValue(in Document doc, in Element elem)
        {
            string gmValue = "";
            ElementId typeId = elem.GetTypeId(); 
            if (typeId != null)
            {
#if R2022
                long typeint = typeId.IntegerValue;
#else
                long typeint = typeId.Value;
#endif
                if (typeint != -1)
                {
                    Element type = doc.GetElement(typeId);
                    if (type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).HasValue) gmValue = type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString();
                }
            }
            return gmValue;
        }
        String GetTypeName(in Document doc, in Element elem)
        {
            string typeName = "";
            ElementId typeId = elem.GetTypeId(); if (typeId != null)
            {
#if R2022
                long typeint = typeId.IntegerValue;
#else
                long typeint = typeId.Value;
#endif
                if (typeint != -1)
                {
                    Element type = doc.GetElement(typeId);
                    typeName = type.Name;
                }
            }
            return typeName;
        }
        String GetTextParamValue(in Document doc, in Element elem, in Guid paramGuid)
        {
            string paramValue = "";
            Element elem1 = doc.GetElement(elem.Id);
            if (Param.ParamExistByGuid(paramGuid, elem) == false)
            {
                ElementId typeId = elem.GetTypeId(); if (typeId != null)
                {
#if R2022
                long typeint = typeId.IntegerValue;
#else
                    long typeint = typeId.Value;
#endif
                    if (typeint != -1)
                    {
                        Element type = doc.GetElement(typeId);
                        if (Param.ParamExistByGuid(paramGuid, type)) elem1 = doc.GetElement(type.Id);
                    }
                }
            }
            if (Param.ParamExistByGuid(paramGuid, elem1) && elem1.get_Parameter(paramGuid).HasValue)
            {
                paramValue = elem1.get_Parameter(paramGuid).AsString();
            }
            return paramValue;
        }
        public string GetAdditionalInformation() => "Обновляет параметр Т_Определение у элементов АР";
        public ChangePriority GetChangePriority() => ChangePriority.FloorsRoofsStructuralWalls;
        public UpdaterId GetUpdaterId() => m_updaterId;
        public string GetUpdaterName() => "TNovParsOpredARUpdater";
    }
}
