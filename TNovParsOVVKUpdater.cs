using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TNovCommon;

namespace TNovBIMUtils
{
    public class TNovParsOVVKUpdater : IUpdater
    {
        private static AddInId m_appId;
        private static UpdaterId m_updaterId;

        //параметры
        Guid adskGparamGuid = new Guid("3de5f1a4-d560-4fa8-a74f-25d250fb3401");//ADSK_Группирование
        Guid TSystemNameParamGuid = new Guid("e4cd1559-649f-4a24-9782-b1840b41773f");//Т_Имя системы
        Guid adskNparamGuid = new Guid("e6e0f5cd-3e26-485b-9342-23882b20eb43");//ADSK_Наименование
        Guid TNaimParamGuid = new Guid("cc50c492-9220-45fa-97fa-a2611b3696e7");//Т_Наименование
        Guid adskMarkparamGuid = new Guid("2204049c-d557-4dfc-8d70-13f19715e46d");//ADSK_Марка
        Guid adskOboznparamGuid = new Guid("9c98831b-9450-412d-b072-7d69b39f4029");//ADSK_Обозначение
        Guid TOboznParamGuid = new Guid("992bd635-f80c-4380-a978-f8ac3bc5a111");//Т_Обозначение
        Guid adskCodeparamGuid = new Guid("2fd9e8cb-84f3-4297-b8b8-75f444e124ed");//ADSK_Код изделия
        Guid adskManufparamGuid = new Guid("a8cdbf7b-d60a-485e-a520-447d2055f351");//ADSK_Завод-изготовитель
        Guid TManufParamGuid = new Guid("2fcb084c-f1bc-473b-9e88-9f9b304254e1");//Т_Завод-изготовитель
        Guid adskEdParamGuid = new Guid("4289cb19-9517-45de-9c02-5a74ebf5c86d");//ADSK_Единица измерения
        Guid TEdParamGuid = new Guid("9486acdc-ed8e-482e-aa18-b518aaf08a94");//Т_Единица измерения
        Guid adskCparamGuid = new Guid("8d057bb3-6ccd-4655-9165-55526691fe3a");//ADSK_Количество
        Guid TCountParamGuid = new Guid("b3f5d47f-d1cf-4ac4-9a38-a27b5204e16c");//Т_Количество
        Guid adskTstParamGuid = new Guid("381b467b-3518-42bb-b183-35169c9bdfb3");//ADSK_Толщина стенки
        Guid TStParamGuid = new Guid("021340dc-4952-4429-b3a9-20ca2a308d92");//Т_Толщина стенки
        Guid TDimsParamGuid = new Guid("f45c49d7-c46f-418c-948e-d4cde7ea6772");//Т_Размер
        Guid TDiamParamGuid = new Guid("e955e814-e8de-404b-aba7-0cfe10120aff");//Т_Диаметр
        Guid NTParamsNotSetParamGuid = new Guid("70879f6b-b838-49de-8ff5-35e1c7d97e0c");

        public TNovParsOVVKUpdater(AddInId id)
        {
            m_appId = id;
            m_updaterId = new UpdaterId(m_appId, new Guid("64242259-e3df-41fd-a922-e4be29a5c339"));
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
            if (docName.Contains("-ВК") || docName.Contains("_ВК") || docName.Contains("-ПТ") || docName.Contains("_ПТ")
                || docName.Contains("-ОВ") || docName.Contains("_ОВ") || docName.Contains("-ТС") || docName.Contains("_ТС"))
            {
                foreach (ElementId elementId in allElementIds)
                {
                    Element elem = doc.GetElement(elementId);
                    if (elem == null) continue;
                    if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, elem)&&elem.get_Parameter(NTParamsNotSetParamGuid).AsDouble() == 1) continue;

                    //Т_Количество
                    double TCountValue = Param.GetDoubleParamValue(doc, adskCparamGuid, elem);

                    //Т_Обозначение
                    string TOboznValue = Param.GetStringParamValue(doc, adskMarkparamGuid, elem);

                    //Т_Наименование
                    string TNaimValue = Param.GetStringParamValue(doc, adskNparamGuid, elem);

                    //Т_Размер
                    string TSizeValue = Param.GetStringParamValue(doc, BuiltInParameter.RBS_CALCULATED_SIZE, elem);

                    //Т_Завод-изготовитель
                    string TManufValue = Param.GetStringParamValue(doc, adskManufparamGuid, elem);
                    
                    //Т_Единица измерения
                    string TEdValue = Param.GetStringParamValue(doc, adskEdParamGuid, elem);

                    //Т_Имя системы
                    string TSystemValue = Param.GetStringParamValue(doc, adskGparamGuid, elem);

                    //Т_Толщина стенки
                    double TStValue = Param.GetDoubleParamValue(doc, adskTstParamGuid, elem)*304.8;

                    //Назначение параметров
                    if (Param.ParamExistByGuid(TCountParamGuid, elem)) //Т_Количество
                    {
                        Parameter TParam = elem.get_Parameter(TCountParamGuid);
                        if (TParam.IsReadOnly == false) {try{TParam.Set(TCountValue).ToString(); } catch { } }
                    }
                    if (Param.ParamExistByGuid(TOboznParamGuid, elem)) //Т_Обозначение
                    {
                        Parameter TParam = elem.get_Parameter(TOboznParamGuid);
                        if (TParam.IsReadOnly == false) {try{TParam.Set(TOboznValue); } catch { } }
                    }
                    if (Param.ParamExistByGuid(TNaimParamGuid, elem)) //Т_Наименование
                    {
                        Parameter TParam = elem.get_Parameter(TNaimParamGuid);
                        if (TParam.IsReadOnly == false) {try{TParam.Set(TNaimValue); } catch { } }
                    }
#if R2022
                    int categoryId = elem.Category.Id.IntegerValue;
#else
                    int categoryId = (int)elem.Category.Id.Value;
#endif
                    if (categoryId == -2008000 || categoryId == -2008010 || categoryId == -2008013 || categoryId == -2008016
                        && Param.ParamExistByGuid(TDimsParamGuid, elem)) //Т_Размер
                    {
                        Parameter TParam = elem.get_Parameter(TDimsParamGuid);
                        if (TParam.IsReadOnly == false) {try{TParam.Set(TSizeValue); } catch { } }
                    }
                    if (Param.ParamExistByGuid(TManufParamGuid, elem)) //Т_Завод-изготовитель
                    {
                        Parameter TParam = elem.get_Parameter(TManufParamGuid);
                        if (TParam.IsReadOnly == false) {try{TParam.Set(TManufValue); } catch { } }
                    }
                    if (Param.ParamExistByGuid(TEdParamGuid, elem)) //Т_Единица измерения
                    {
                        Parameter TParam = elem.get_Parameter(TEdParamGuid);
                        if (TParam.IsReadOnly == false) {try{TParam.Set(TEdValue); } catch { } }
                    }
                    if (Param.ParamExistByGuid(TSystemNameParamGuid, elem)) //Т_Имя системы
                    {
                        Parameter TParam = elem.get_Parameter(TSystemNameParamGuid);
                        if (TParam.IsReadOnly == false) {try{TParam.Set(TSystemValue); } catch { } }
                    }
                    if (Param.ParamExistByGuid(TStParamGuid, elem)) //Т_Толщина стенки
                    {
                        Parameter TParam = elem.get_Parameter(TStParamGuid);
                        if (TParam.IsReadOnly == false) {try{TParam.Set(TStValue.ToString().Replace(',','.')); } catch { } }
                    }
                }
            }

               
        }
        
        public string GetAdditionalInformation() => "Обновляет Т параметры у элементов ОВ ВК";
        public ChangePriority GetChangePriority() => ChangePriority.FloorsRoofsStructuralWalls;
        public UpdaterId GetUpdaterId() => m_updaterId;
        public string GetUpdaterName() => "TNovParsOVVKUpdater";
    }
}
