using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TNovBIMUtils.Panel13
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TestOV : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // Получаем выбранные соединительные детали воздуховодов
                var selectedIds = uidoc.Selection.GetElementIds();
                if (selectedIds.Count == 0)
                {
                    TaskDialog.Show("Информация", "Выберите соединительные детали воздуховодов.");
                    return Result.Cancelled;
                }

                // Фильтруем только соединительные детали воздуховодов
                var ductConnectors = new List<Element>();
                foreach (ElementId id in selectedIds)
                {
                    Element elem = doc.GetElement(id);
                    if (IsDuctConnector(elem))
                    {
                        ductConnectors.Add(elem);
                    }
                }

                if (ductConnectors.Count == 0)
                {
                    TaskDialog.Show("Информация", "Выберите соединительные детали воздуховодов.");
                    return Result.Cancelled;
                }

                using (Transaction trans = new Transaction(doc, "Запись ID воздуховодов в комментарии"))
                {
                    trans.Start();

                    foreach (Element ductConnector in ductConnectors)
                    {
                        AnalyzeDuctConnector(doc, ductConnector);
                    }

                    trans.Commit();
                }

                TaskDialog.Show("Успех", $"Обработано {ductConnectors.Count} соединительных деталей.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
        private bool IsDuctConnector(Element element)
        {
            // Проверяем, является ли элемент соединительной деталью воздуховода
            if (element is FamilyInstance familyInstance)
            {
                // Проверяем категорию и наличие коннекторов
                if (familyInstance.Category != null &&
                    (familyInstance.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctFitting ||
                     familyInstance.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctAccessory ||
                     familyInstance.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctTerminal))
                {
                    return true;
                }
            }
            return false;
        }

        private void AnalyzeDuctConnector(Document doc, Element ductConnector)
        {
            var connectedDuctIds = new HashSet<ElementId>();

            // Получаем коннекторы элемента
            var connectorSet = GetConnectors(ductConnector);
            if (connectorSet == null) return;

            // Шаг 1: Проверяем прямое подключение к воздуховодам
            bool hasDirectDuctConnection = false;

            foreach (Connector connector in connectorSet)
            {
                foreach (Connector refConnector in connector.AllRefs)
                {
                    if (refConnector.Owner is Duct)
                    {
                        connectedDuctIds.Add(refConnector.Owner.Id);
                        hasDirectDuctConnection = true;
                    }
                }
            }

            // Если есть прямое подключение к воздуховодам
            if (hasDirectDuctConnection)
            {
                string comment = string.Join(",", connectedDuctIds.Select(id => id.IntegerValue));
                SetCommentParameter(doc, ductConnector, comment);
                return;
            }

            // Шаг 2: Если нет прямых подключений к воздуховодам,
            // ищем воздуховоды через подключенные соединительные детали/арматуру
            var analyzedElements = new HashSet<ElementId> { ductConnector.Id };
            var elementsToProcess = new Queue<Element>();
            var affectedElements = new List<Element> { ductConnector };

            // Добавляем все подключенные соединительные детали/арматуру
            foreach (Connector connector in connectorSet)
            {
                foreach (Connector refConnector in connector.AllRefs)
                {
                    Element connectedElement = refConnector.Owner;

                    if (connectedElement.Id != ductConnector.Id &&
                        IsDuctConnector(connectedElement) &&
                        !analyzedElements.Contains(connectedElement.Id))
                    {
                        elementsToProcess.Enqueue(connectedElement);
                        analyzedElements.Add(connectedElement.Id);
                        affectedElements.Add(connectedElement);
                    }
                }
            }

            // Проверяем каждый подключенный элемент на наличие воздуховодов
            while (elementsToProcess.Count > 0)
            {
                Element currentElement = elementsToProcess.Dequeue();
                var currentConnectors = GetConnectors(currentElement);

                if (currentConnectors != null)
                {
                    foreach (Connector connector in currentConnectors)
                    {
                        foreach (Connector refConnector in connector.AllRefs)
                        {
                            if (refConnector.Owner is Duct)
                            {
                                connectedDuctIds.Add(refConnector.Owner.Id);
                            }
                        }
                    }
                }
            }

            // Если нашли воздуховоды через подключенные элементы
            if (connectedDuctIds.Count > 0)
            {
                string comment = string.Join(",", connectedDuctIds.Select(id => id.IntegerValue));

                // Записываем комментарий для всех затронутых элементов
                foreach (Element element in affectedElements)
                {
                    SetCommentParameter(doc, element, comment);
                }
            }
            else
            {
                // Если воздуховодов не найдено, очищаем параметр
                SetCommentParameter(doc, ductConnector, "");
            }
        }

        private ConnectorSet GetConnectors(Element element)
        {
            if (element is FamilyInstance familyInstance)
            {
                if (familyInstance.MEPModel != null)
                {
                    return familyInstance.MEPModel.ConnectorManager.Connectors;
                }
            }
            else if (element is MEPCurve mepCurve)
            {
                return mepCurve.ConnectorManager.Connectors;
            }
            return null;
        }

        private void SetCommentParameter(Document doc, Element element, string value)
        {
            Parameter commentParam = element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (commentParam != null && !commentParam.IsReadOnly)
            {
                commentParam.Set(value);
            }
        }
    }
}
