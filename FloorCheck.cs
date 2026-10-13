using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using TNovCommon;

namespace TNovBIMUtils
{
    /// <summary>
    /// Проверка этажей: немодальное окно, изолирующее на 3D-виде элементы с заданным значением N_Эт.Номер / Т_Этаж.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class FloorCheck : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Проверка этажей";
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIApplication uiApp = RevitAPI.UiApplication;

            TNovConfig config = TNovConfigLoad.LoadConfig(DBCommandName, TNovVersion);
            if (config == null) return Result.Failed;

            try
            {
                FloorCheckRevitBridge.Initialize();

                Document doc = uiApp.ActiveUIDocument?.Document;
                if (doc != null && !FloorCheckActions.Is3DView(doc.ActiveView))
                    new InfoWindow280(FloorCheckActions.Not3DMessage).ShowDialog();

                FloorCheckHost.ShowOrActivate(uiApp);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                new InfoWindow280("Ошибка: " + ex.Message).ShowDialog();
                return Result.Failed;
            }
        }
    }
}
