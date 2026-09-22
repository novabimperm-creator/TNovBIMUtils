using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using TNovCommon;

namespace TNovBIMUtils
{
    [Transaction(TransactionMode.Manual)]
    public class TDiamExceptionsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Исключения Т_Диаметр";
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIApplication uiApp = RevitAPI.UiApplication;

            TNovConfig config = TNovConfigLoad.LoadConfig(DBCommandName, TNovVersion);
            if (config == null) return Result.Failed;

            string filePath = TDiamExceptionStore.GetFilePath();
            if (string.IsNullOrEmpty(filePath))
            {
                new InfoWindow280("Не задан путь к серверной папке _TNov.").ShowDialog();
                return Result.Failed;
            }

            try
            {
                var viewModel = new TDiamExceptionsViewModel();
                var window = new TDiamExceptionsWPF(viewModel);
                RevitWindow.ShowDialog(window, uiApp);
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
