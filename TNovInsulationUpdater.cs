using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TNovCommon;

namespace TNovBIMUtils
{
    [Transaction(TransactionMode.Manual)]
    public class TNovInsulationUpdater : IUpdater
    {
        static AddInId _appId;
        static UpdaterId _updaterId;

        public TNovInsulationUpdater(AddInId id)
        {
            _appId = id;

            _updaterId = new UpdaterId(_appId, new Guid("3b34b8b6-bbb0-4be9-b7e3-269b3f22f9c0"));
        }

        public void Execute(UpdaterData data)
        {
            Document doc = data.GetDocument();

            //проверка подключения к серверу
            TNovConfig config = TNovConfigLoad.LoadConfig();
            string usagefilePath = config.ServerPath + "usage.txt";
            bool servercheck = File.Exists(usagefilePath);

            if (servercheck)
            {
                List<ElementId> idsA = data.GetAddedElementIds().ToList();
                List<ElementId> idsM = data.GetModifiedElementIds().ToList();
                List<ElementId> ids = new List<ElementId>();
                foreach (var id in idsA) ids.Add(id); foreach (var id in idsM) ids.Add(id);

                foreach (ElementId id in ids)
                {
                    Element elem = doc.GetElement(id);
                    if (elem != null & elem.Name != null)
                    {
                        string value = "Не определено";

                        long catId = IdLongValue(elem.Category.Id);

                        if (catId == -2008122) //изоляция труб PipeInsulation
                        {
                            PipeInsulation pipeInsulation = (PipeInsulation)elem;
                            if (pipeInsulation.HostElementId != null && IdLongValue(pipeInsulation.HostElementId) != -1)
                            {
                                Element host = doc.GetElement(pipeInsulation.HostElementId);
                                int hostCatId = (int)IdLongValue(host.Category.Id);
                                if (hostCatId == -2008049 || hostCatId == -2008055) value = "Фитинги и арматура труб";
                                else if (hostCatId == -2008044) value = "Трубы";
                            }
                        }
                        else if (IdLongValue(elem.Category.Id) == -2008123) //изоляция возд DuctInsulation
                        {
                            DuctInsulation ductInsulation = (DuctInsulation)elem;
                            if (ductInsulation.HostElementId != null && IdLongValue(ductInsulation.HostElementId) != -1)
                            {
                                Element host = doc.GetElement(ductInsulation.HostElementId);
                                int hostCatId = (int)IdLongValue(host.Category.Id);
                                if (hostCatId == -2008010 || hostCatId == -2008016) value = "Фитинги и арматура воздуховодов";
                                else if (hostCatId == -2008000) value = "Воздуховоды";
                            }
                        }
                        else if (IdLongValue(elem.Category.Id) == -2008124) //внутр изол возд DuctLining
                        {
                            DuctLining ductLining = (DuctLining)elem;
                            if (ductLining.HostElementId != null && IdLongValue(ductLining.HostElementId) != -1)
                            {
                                Element host = doc.GetElement(ductLining.HostElementId);
                                int hostCatId = (int)IdLongValue(host.Category.Id);
                                if (hostCatId == -2008010 || hostCatId == -2008016) value = "Фитинги и арматура воздуховодов";
                                else if (hostCatId == -2008000) value = "Воздуховоды";
                            }
                        }
                        try
                        {
                            elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(value);
                        }
                        catch (Exception e) {  }
                    }
                }

            }



        }
        private long IdLongValue(ElementId id)
        {
#if R2022
            return id.IntegerValue;
#else
            return id.Value;
#endif
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
            return "TNovInsulationUpdater";
        }
    }
}
