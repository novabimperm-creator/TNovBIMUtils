using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TNovCommon;

namespace TNovBIMUtils
{
    public class TNovParsNaimOboznSTUpdater : IUpdater
    {
        private static AddInId m_appId;
        private static UpdaterId m_updaterId;

        //параметры
        Guid NProjectCodeParamGuid = new Guid("ae46eb7a-03bf-497e-ac96-1615c672324b");//N_Ш.ШифрПроекта
        Guid adskSheetSetParamGuid = new Guid("e1b06433-f527-403c-8986-af9a01e6be7f");//A_Комплект чертежей
        Guid adskElemSheetNumberParamGuid = new Guid("68e483e3-4c06-494b-979e-4958d44d6f71");//A_Номер листа элемента
        Guid adskCMarkParamGuid = new Guid("5d369dfb-17a2-4ae2-a1a1-bdfc33ba7405"); //A_Марка конструкции
        Guid adskIzdMarkParamGuid = new Guid("92ae0425-031b-40a9-8904-023f7389963b");//A_Марка изделия
        Guid NNaimParamGuid = new Guid("c0a5ddcb-1fc6-4151-9c6b-e12d2e293b9f");//N_Наименование
        Guid TNaimParamGuid = new Guid("cc50c492-9220-45fa-97fa-a2611b3696e7");//Т_Наименование
        Guid NOboznParamGuid = new Guid("5e21acce-0d16-4d14-81ca-a3adfab14142");//N_Обозначение
        Guid TOboznParamGuid = new Guid("992bd635-f80c-4380-a978-f8ac3bc5a111");//Т_Обозначение
        Guid NTParamsNotSetParamGuid = new Guid("70879f6b-b838-49de-8ff5-35e1c7d97e0c");

        public TNovParsNaimOboznSTUpdater(AddInId id)
        {
            m_appId = id;
            m_updaterId = new UpdaterId(m_appId, new Guid("8cf9db58-79db-4cb4-8c24-dd0762fb6ade"));
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

            // шифр проекта
            ProjectInfo projectInfo = doc.ProjectInformation;
            string projectCodeValue = "";
            if (projectInfo != null)
            {
                try { projectCodeValue = projectInfo.get_Parameter(NProjectCodeParamGuid)?.AsString(); } catch { }
            }

            string docName = doc.Title.ToString();
            if (docName.Contains("-КЖ") || docName.Contains("_КЖ") || docName.Contains("-КР-") || docName.Contains("_КР_"))
            {
                foreach (ElementId elementId in allElementIds)
                {
                    Element elem = doc.GetElement(elementId);
                    if (elem == null) continue;
                    if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, elem) && elem.get_Parameter(NTParamsNotSetParamGuid).AsDouble() == 1) continue;

                    string naimValue = ""; string oboznValue = "";

                    //сценарии: 1 - заводское изделие, 2 - индив изделие, 3 - конструкция
                    int scenario = 3;
                    //считываем исходные параметры либо с экз, либо с типа
                    string NOboznParamValue = Param.GetStringParamValue(doc, NOboznParamGuid, elem); 
                    string NNaimParamValue = Param.GetStringParamValue(doc, NNaimParamGuid, elem); 
                    string adskCMarkParamValue = Param.GetStringParamValue(doc, adskCMarkParamGuid, elem); 
                    string adskIzdMarkParamValue = Param.GetStringParamValue(doc, adskIzdMarkParamGuid, elem); 
                    string adskSheetSetParamValue = Param.GetStringParamValue(doc, adskSheetSetParamGuid, elem); 
                    if (adskSheetSetParamValue.Length > 0) adskSheetSetParamValue = "-" + adskSheetSetParamValue;
                    string adskElemSheetNumberParamValue = Param.GetStringParamValue(doc, adskElemSheetNumberParamGuid, elem); 
                    if (adskElemSheetNumberParamValue.Length > 0) adskElemSheetNumberParamValue = " л. " + adskElemSheetNumberParamValue;
                    //группа модели
                    string gmValue = ""; ElementId typeId = elem.GetTypeId();
                    if (typeId != null && typeId.IntegerValue != -1)
                    {
                        Element type = doc.GetElement(typeId);
                        if (type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).HasValue)
                            gmValue = type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString();
                    }
                    if (gmValue.Contains("Серия") || gmValue.Contains("ГОСТ")) scenario = 1;
                    else if (adskIzdMarkParamValue.Length > 1) scenario = 2;

                    switch (scenario)
                    {
                        case 1:
                            naimValue = NNaimParamValue + " " + adskIzdMarkParamValue;
                            oboznValue = NOboznParamValue;
                            break;
                        case 2:
                            naimValue = NNaimParamValue + " " + adskIzdMarkParamValue;
                            oboznValue = projectCodeValue + adskSheetSetParamValue + adskElemSheetNumberParamValue;
                            break;
                        case 3:
                            if (NNaimParamValue.Length == 0 && adskCMarkParamValue.Length > 0) NNaimParamValue = ConstructionType(adskCMarkParamValue);
                            naimValue = NNaimParamValue + " " + adskCMarkParamValue;
                            oboznValue = projectCodeValue + adskSheetSetParamValue + adskElemSheetNumberParamValue;
                            break;
                    }

                    try { 
                        if (naimValue != null && naimValue.Length > 0 && Param.ParamExistByGuid(TNaimParamGuid, elem))
                        {
                            Parameter param = elem.get_Parameter(TNaimParamGuid); //Т_Наименование
                            if (param.IsReadOnly == false) { param.Set(naimValue); }
                        }
                        if (oboznValue != null && oboznValue.Length > 0 && Param.ParamExistByGuid(TOboznParamGuid, elem))
                        {
                            Parameter param = elem.get_Parameter(TOboznParamGuid); //Т_Обозначение
                            if (param.IsReadOnly == false) { param.Set(oboznValue); }
                        }
                    }
                    catch { }
                }
            }

            
        }
        
        String ConstructionType(in string mark)
        {
            string type = "";
            if (mark.StartsWith("Фп") || mark.StartsWith("Фм")) type = "Фундаментная плита";
            if (mark.StartsWith("Рп") || mark.StartsWith("Рм")) type = "Ростверк";
            if (mark.StartsWith("Пл") || mark.StartsWith("Пр")) type = "Приямок";
            if (mark.StartsWith("Пп")) type = "Плита перекрытия";
            if (mark.StartsWith("Пб")) type = "Плита по грунту";
            if (mark.StartsWith("Кл")) type = "Колонна";
            if (mark.StartsWith("Пм")) type = "Пилон";
            if (mark.StartsWith("Дж")) type = "Диафрагма жесткости";
            if (mark.StartsWith("Мс")) type = "Стена монолитная";
            if (mark.StartsWith("Бм")) type = "Балка монолитная";
            if (mark.StartsWith("Лм")) type = "Лестница монолитная";
            if (mark.StartsWith("Лп")) type = "Площадка монолитная";
            if (mark.StartsWith("Пт")) type = "Парапет";
            if (mark.StartsWith("Км")) type = "Канал монолитный";
            return type;
        }
        public string GetAdditionalInformation() => "Обновляет параметры Т_Наименование и Т_Обозначение у элементов КЖ";
        public ChangePriority GetChangePriority() => ChangePriority.FloorsRoofsStructuralWalls;
        public UpdaterId GetUpdaterId() => m_updaterId;
        public string GetUpdaterName() => "TNovParsNaimOboznSTUpdater";
    }
}
