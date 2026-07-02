using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TNovCommon;

namespace TNovBIMUtils
{
    public class TNovParsOpredSTUpdater : IUpdater
    {
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
            if (docName.Contains("-КЖ") || docName.Contains("_КЖ") || docName.Contains("-КР-") || docName.Contains("_КР_"))
            {
                foreach (ElementId elementId in allElementIds)
                {
                    Element elem = doc.GetElement(elementId);
                    if (elem == null) continue;
                    if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, elem) && elem.get_Parameter(NTParamsNotSetParamGuid).AsDouble() == 1) continue;
#if R2022
                        long catId = elem.Category.Id.IntegerValue;
#else
                    long catId = elem.Category.Id.Value;
#endif
                    
                    if (catId == -2000919 || catId == -2000920 || catId == -2000123 || catId == -2000120) //лестницы и вложенные лестниц - ускоренное назначение параметра
                    {
                        if (Param.ParamExistByGuid(TOprParamGuid, elem))
                        {
                            Parameter param = elem.get_Parameter(TOprParamGuid); //Т_Определение
                            if (param.IsReadOnly == false) { try{param.Set("Лестница"); } catch { } }
                        }
                        continue;
                    }
                    if (catId == -2000126) //ограждения - ускоренное назначение параметра
                    {
                        if (Param.ParamExistByGuid(TOprParamGuid, elem))
                        {
                            Parameter param = elem.get_Parameter(TOprParamGuid); //Т_Определение
                            if (param.IsReadOnly == false) { try{param.Set("Ограждение"); } catch { } }
                        }
                        continue;
                    }
                    string group = "";
                    group = MarkGroup(elem, doc);
                    if (group != null && group.Length > 0 && Param.ParamExistByGuid(TOprParamGuid, elem))
                    {
                        Parameter param = elem.get_Parameter(TOprParamGuid); //Т_Определение
                        if (param.IsReadOnly == false) { try { param.Set(group); } catch { } }
                    }
                }
            }

            
        }
        String MarkGroup(in Element elem, in Document doc)
        {
            string mark = "-";
            if (Param.ParamExistByGuid(adskCMarkParamGuid, elem) && elem.get_Parameter(adskCMarkParamGuid).HasValue)
            {
                mark = elem.get_Parameter(adskCMarkParamGuid).AsString(); 
            }

            string group = "";
            if (mark.StartsWith("Фп") || mark.StartsWith("Рп") || mark.StartsWith("Фм") || mark.StartsWith("Рм") || mark.StartsWith("Рл"))
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != null && GetIdValue(typeId) != -1)
                    group = ParseTypeST(typeId, doc, "Фундамент");
            }
            else if (mark.StartsWith("Пл") || mark.StartsWith("Пп"))
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != null && GetIdValue(typeId) != -1)
                    group = ParseTypeST(typeId, doc, "Плита перекрытия");
            }
            else if (mark.StartsWith("Пб"))
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != null && GetIdValue(typeId) != -1)
                    group = ParseTypeST(typeId, doc, "Плита по грунту");
            }
            else if (mark.StartsWith("Пр"))
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != null && GetIdValue(typeId) != -1)
                    group = ParseTypeST(typeId, doc, "Приямок");
            }
            else if (mark.StartsWith("Кл"))
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != null && GetIdValue(typeId) != -1)
                    group = ParseTypeST(typeId, doc, "Колонна");
            }
            else if (mark.StartsWith("Пм"))
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != null && GetIdValue(typeId) != -1)
                    group = ParseTypeST(typeId, doc, "Пилон");
            }
            else if (mark.StartsWith("Дж") || mark.StartsWith("Мс"))
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != null && GetIdValue(typeId) != -1)
                    group = ParseTypeST(typeId, doc, "Стена");
            }
            else if (mark.StartsWith("Бм"))
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != null && GetIdValue(typeId) != -1)
                    group = ParseTypeST(typeId, doc, "Балка");
            }
            else if (mark.StartsWith("Лм") || mark.StartsWith("Лп") || mark.StartsWith("Лк"))
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != null && GetIdValue(typeId) != -1)
                    group = ParseTypeST(typeId, doc, "Лестница");
            }
            else if (mark.StartsWith("Пт"))
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != null && GetIdValue(typeId) != -1)
                    group = ParseTypeST(typeId, doc, "Парапет");
            }
            else // прочие марки (Км и т.д.) либо пустые марки
            {
                if (GetIdValue(elem.Category.Id) == -2001300)
                {
                    ElementId typeId = elem.GetTypeId();
                    if (typeId != null && GetIdValue(typeId) != -1)
                        group = ParseTypeST(typeId, doc, "Фундамент");
                }
                if (GetIdValue(elem.Category.Id) == -2000032)
                {
                    ElementId typeId = elem.GetTypeId();
                    if (typeId != null && GetIdValue(typeId) != -1)
                        group = ParseTypeST(typeId, doc, "Плита перекрытия");
                }
                if (GetIdValue(elem.Category.Id) == -2000011)
                {
                    ElementId typeId = elem.GetTypeId();
                    if (typeId != null && GetIdValue(typeId) != -1)
                        group = ParseTypeST(typeId, doc, "Стена");
                }
                if (GetIdValue(elem.Category.Id) == -2000120)
                {
                    ElementId typeId = elem.GetTypeId();
                    if (typeId != null && GetIdValue(typeId) != -1)
                        group = ParseTypeST(typeId, doc, "Лестница");
                }
            }

            return group;
        }

        String ParseTypeST(in ElementId typeId, in Document doc, in string OpredValue)
        {
            string group = "";
            Element type = doc.GetElement(typeId);
            //подготовка, термо, гидро, сваи, лестницы, галтели
            if (type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).HasValue) //условие исходя из группы модели
            {
                string gm = type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString(); 
                if (gm.Contains("Подготовка") || gm.Contains("Подбетонка")) group = "Подготовка";
                if (gm.Contains("Термо")) group = "Термовкладыш";
                if (gm.Contains("Свая")) group = "Свая";
                if (gm.Contains("Лестн")) group = "Лестница";
                if (gm.Contains("Галтель")) group = "Фундамент";
            }
            else //альтернативное исходя из имени типа
            {
                if (type.Name.Contains("Подготовка") || type.Name.Contains("Подбетонка")) group = "Подготовка";
                if (type.Name.Contains("Термо")) group = "Термовкладыш";
                if (type.Name.Contains("ГИ") || type.Name.Contains("Гидроиз")) group = "Гидроизоляция";
                if (type.Name.Contains("Фунд")) group = "Фундамент";
            }
            //основная конструкция (бетон)
            if (type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).HasValue) //условие исходя из группы модели
            {
                string gm = type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString();
                if (gm.Contains("Бетон") || gm.Contains("Бетон")) group = OpredValue;
            }
            else //альтернативное исходя из имени типа
            {
                if (type.Name.Contains("Бетон") || type.Name.Contains("Бетон")) group = OpredValue;
            }
            //рампа
            if (type.Name.Contains("Рампа") || type.Name.Contains("рампа")) group = "Рампа"; 


            return group;
        }
        private static int GetIdValue(ElementId id)
        {
#if R2022
    return id.IntegerValue;
#else
            return (int)id.Value;
#endif
        }
        public string GetAdditionalInformation() => "Обновляет параметр Т_Определение у элементов КЖ";
        public ChangePriority GetChangePriority() => ChangePriority.FloorsRoofsStructuralWalls;
        public UpdaterId GetUpdaterId() => m_updaterId;
        public string GetUpdaterName() => "TNovParsOpredSTUpdater";
    }
}
