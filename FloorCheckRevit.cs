using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TNovCommon;

namespace TNovBIMUtils
{
    /// <summary>
    /// Единственное немодальное окно Проверки этажей.
    /// </summary>
    public static class FloorCheckHost
    {
        private static FloorCheckWPF _window;

        public static void ShowOrActivate(UIApplication uiapp)
        {
            if (_window != null)
            {
                if (_window.WindowState == System.Windows.WindowState.Minimized)
                    _window.WindowState = System.Windows.WindowState.Normal;
                _window.Activate();
                return;
            }

            _window = new FloorCheckWPF();
            _window.Closed += (s, e) => { _window = null; };
            RevitWindow.Show(_window, uiapp);
        }
    }

    /// <summary>
    /// Мост «немодальное окно → Revit API». Действия из WPF ставятся в очередь ExternalEvent.
    /// </summary>
    public sealed class FloorCheckActionHandler : IExternalEventHandler
    {
        private readonly ConcurrentQueue<Action<UIApplication>> _queue = new ConcurrentQueue<Action<UIApplication>>();
        public ExternalEvent Event { get; set; }

        public void Enqueue(Action<UIApplication> action)
        {
            _queue.Enqueue(action);
            Event?.Raise();
        }

        public void Execute(UIApplication app)
        {
            while (_queue.TryDequeue(out var action))
            {
                try { action(app); }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка Revit-операции: " + ex, 4);
                    new InfoWindow280("Ошибка Revit-операции: " + ex.Message).ShowDialog();
                }
            }
        }

        public string GetName() => "TNov Проверка этажей — Revit Action";
    }

    public static class FloorCheckRevitBridge
    {
        private static FloorCheckActionHandler _handler;

        /// <summary>Вызывать из IExternalCommand.Execute (ExternalEvent создается только в контексте API).</summary>
        public static void Initialize()
        {
            if (_handler != null) return;
            _handler = new FloorCheckActionHandler();
            _handler.Event = ExternalEvent.Create(_handler);
        }

        public static void Enqueue(Action<UIApplication> action)
        {
            if (_handler == null)
                throw new InvalidOperationException("FloorCheckRevitBridge не инициализирован");
            _handler.Enqueue(action);
        }
    }

    /// <summary>
    /// Операции Проверки этажей. Все методы вызываются в контексте Revit API.
    /// </summary>
    public static class FloorCheckActions
    {
        public const string FloorNumberParam = "N_Эт.Номер";
        public const string FloorTextParam = "Т_Этаж";
        public const string Not3DMessage = "Активный вид — не 3D. Проверка этажей работает на 3D-виде: откройте 3D-вид.";

        /// <summary>Допуск сравнения чисел — значения в единицах параметра после перевода из внутренних.</summary>
        private const double NumberEpsilon = 1e-4;
        private const string TransactionName = "Проверка этажей";

        public static bool Is3DView(View view) => view is View3D && !view.IsTemplate;

        /// <summary>Категории, к которым параметр привязан в модели (параметр проекта / общий).</summary>
        public static List<ElementId> GetBoundCategories(Document doc, string paramName)
        {
            var result = new HashSet<ElementId>();
            DefinitionBindingMapIterator iterator = doc.ParameterBindings.ForwardIterator();
            while (iterator.MoveNext())
            {
                if (iterator.Key?.Name != paramName || !(iterator.Current is ElementBinding binding))
                    continue;
                foreach (Category category in binding.Categories)
                    result.Add(category.Id);
            }
            return result.ToList();
        }

        /// <summary>
        /// Различные непустые значения параметра в модели (как отображаются в Revit): числа — по возрастанию,
        /// текст — в естественном порядке. Для выпадающего списка и кнопок ^/v.
        /// </summary>
        public static List<string> CollectValues(Document doc, string paramName)
        {
            var values = new HashSet<string>();
            List<ElementId> categoryIds = GetBoundCategories(doc, paramName);
            if (categoryIds.Count == 0)
                return new List<string>();

            bool isNumber = paramName == FloorNumberParam;
            var collector = new FilteredElementCollector(doc)
                .WherePasses(new ElementMulticategoryFilter(categoryIds))
                .WhereElementIsNotElementType();
            foreach (Element element in collector)
            {
                Parameter parameter = FindParameter(doc, element, paramName);
                if (parameter == null || !parameter.HasValue)
                    continue;
                string text;
                if (isNumber)
                {
                    double? number = AsNumber(parameter);
                    if (!number.HasValue)
                        continue;
                    text = FormatNumber(number.Value);
                }
                else
                    text = AsText(parameter).Trim();
                if (text.Length > 0)
                    values.Add(text);
            }

            return isNumber
                ? values.OrderBy(v => TryParseNumber(v, out double d) ? d : 0).ToList()
                : values.OrderBy(v => v, new AlphanumComparatorFastString()).ToList();
        }

        /// <summary>
        /// Изолирует на активном 3D-виде элементы привязанных категорий с заданным значением параметра и все уровни.
        /// Возвращает число изолированных элементов (без уровней) или null, если изоляция не выполнена.
        /// </summary>
        public static int? Apply(UIApplication app, string paramName, string value)
        {
            Document doc = app.ActiveUIDocument?.Document;
            if (doc == null)
                return null;
            View view = doc.ActiveView;
            if (!Is3DView(view))
            {
                new InfoWindow280(Not3DMessage).ShowDialog();
                return null;
            }

            List<ElementId> categoryIds = GetBoundCategories(doc, paramName);
            if (categoryIds.Count == 0)
            {
                new InfoWindow280("Параметр «" + paramName + "» не добавлен ни к одной категории в этой модели.").ShowDialog();
                return null;
            }

            bool isNumber = paramName == FloorNumberParam;
            value = (value ?? "").Trim();
            double number = 0;
            if (isNumber && value.Length > 0 && !TryParseNumber(value, out number))
            {
                new InfoWindow280("«" + value + "» — не число. Параметр «" + paramName + "» числовой.").ShowDialog();
                return null;
            }

            Logger.Log("Проверка этажей: " + paramName + " = «" + value + "»", 1);
            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start(TransactionName);
                // Снимаем прошлую изоляцию, чтобы собрать все элементы вида.
                if (view.IsInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate))
                {
                    view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                    doc.Regenerate();
                }

                var ids = new List<ElementId>();
                var collector = new FilteredElementCollector(doc, view.Id)
                    .WherePasses(new ElementMulticategoryFilter(categoryIds))
                    .WhereElementIsNotElementType();
                foreach (Element element in collector)
                {
                    Parameter parameter = FindParameter(doc, element, paramName);
                    if (parameter == null)
                        continue;
                    bool match = isNumber
                        ? MatchesNumber(parameter, value.Length == 0 ? (double?)null : number)
                        : MatchesText(parameter, value);
                    if (match)
                        ids.Add(element.Id);
                }
                int count = ids.Count;

                // Уровни видны всегда.
                ids.AddRange(new FilteredElementCollector(doc).OfClass(typeof(Level))
                    .Where(level => level.CanBeHidden(view))
                    .Select(level => level.Id));

                if (ids.Count > 0)
                    view.IsolateElementsTemporary(ids);
                doc.Regenerate();
                transaction.Commit();
                return count;
            }
        }

        /// <summary>Снимает временное скрытие/изоляцию на активном виде.</summary>
        public static void Reset(UIApplication app)
        {
            Document doc = app.ActiveUIDocument?.Document;
            View view = doc?.ActiveView;
            if (view == null || !view.IsInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate))
                return;
            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start(TransactionName);
                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                transaction.Commit();
            }
        }

        public static bool TryParseNumber(string text, out double number) =>
            double.TryParse((text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number);

        public static string FormatNumber(double number) =>
            Math.Round(number, 4).ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>Параметр экземпляра, иначе — типа.</summary>
        private static Parameter FindParameter(Document doc, Element element, string paramName)
        {
            Parameter parameter = element.LookupParameter(paramName);
            if (parameter != null)
                return parameter;
            Element type = doc.GetElement(element.GetTypeId());
            return type?.LookupParameter(paramName);
        }

        private static double? AsNumber(Parameter parameter)
        {
            switch (parameter.StorageType)
            {
                case StorageType.Integer:
                    return parameter.AsInteger();
                case StorageType.Double:
                    return FromInternalUnits(parameter);
                case StorageType.String:
                    return TryParseNumber(parameter.AsString(), out double d) ? d : (double?)null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Значение Double — в единицах параметра (Revit хранит во внутренних: м² → кв. футы, поэтому 1 читается как 10.76391).
        /// </summary>
        private static double FromInternalUnits(Parameter parameter)
        {
            double value = parameter.AsDouble();
            try
            {
                return UnitUtils.ConvertFromInternalUnits(value, parameter.GetUnitTypeId());
            }
            catch (Exception)
            {
                // У параметра без единиц измерения — значение как есть.
                return value;
            }
        }

        private static string AsText(Parameter parameter)
        {
            if (!parameter.HasValue)
                return "";
            return (parameter.StorageType == StorageType.String ? parameter.AsString() : parameter.AsValueString()) ?? "";
        }

        /// <summary>null — ищем элементы без значения.</summary>
        private static bool MatchesNumber(Parameter parameter, double? number)
        {
            if (!number.HasValue)
                return !parameter.HasValue
                    || (parameter.StorageType == StorageType.String && string.IsNullOrWhiteSpace(parameter.AsString()));
            if (!parameter.HasValue)
                return false;
            double? value = AsNumber(parameter);
            return value.HasValue && Math.Abs(value.Value - number.Value) < NumberEpsilon;
        }

        /// <summary>Пустая строка — ищем элементы без значения.</summary>
        private static bool MatchesText(Parameter parameter, string text) =>
            string.Equals(AsText(parameter).Trim(), text, StringComparison.Ordinal);
    }
}
