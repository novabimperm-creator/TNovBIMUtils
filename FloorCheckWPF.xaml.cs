using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TNovBIMUtils
{
    /// <summary>Немодальное окно Проверки этажей. Действия выполняются через FloorCheckRevitBridge.</summary>
    public partial class FloorCheckWPF : Window
    {
        private bool _resettingCombo;

        private string ParamName => textRadio.IsChecked == true
            ? FloorCheckActions.FloorTextParam
            : FloorCheckActions.FloorNumberParam;

        public FloorCheckWPF()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                valueBox.Focus();
                RefreshValues();
            };
        }

        private void paramRadio_Checked(object sender, RoutedEventArgs e)
        {
            // Срабатывает и в InitializeComponent, до создания остальных элементов.
            if (valueBox == null)
                return;
            valueBox.Text = "";
            statusText.Text = "";
            valuesCombo.ItemsSource = null;
            RefreshValues();
        }

        /// <summary>Перечитывает значения выбранного параметра из модели в выпадающий список.</summary>
        private void RefreshValues()
        {
            string paramName = ParamName;
            FloorCheckRevitBridge.Enqueue(app =>
            {
                Document doc = app.ActiveUIDocument?.Document;
                if (doc != null && paramName == ParamName)
                    SetValues(FloorCheckActions.CollectValues(doc, paramName));
            });
        }

        private void SetValues(List<string> values)
        {
            _resettingCombo = true;
            valuesCombo.ItemsSource = values;
            valuesCombo.SelectedIndex = -1;
            _resettingCombo = false;
        }

        private void valuesCombo_DropDownOpened(object sender, System.EventArgs e) => RefreshValues();

        private void valuesCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_resettingCombo || !(valuesCombo.SelectedItem is string value))
                return;
            _resettingCombo = true;
            valuesCombo.SelectedIndex = -1;
            _resettingCombo = false;

            valueBox.Text = value;
            string paramName = ParamName;
            FloorCheckRevitBridge.Enqueue(app => Apply(app, paramName, value));
        }

        private void applyButton_Click(object sender, RoutedEventArgs e)
        {
            string paramName = ParamName;
            string value = valueBox.Text;
            FloorCheckRevitBridge.Enqueue(app => Apply(app, paramName, value));
        }

        private void resetButton_Click(object sender, RoutedEventArgs e)
        {
            FloorCheckRevitBridge.Enqueue(app =>
            {
                FloorCheckActions.Reset(app);
                statusText.Text = "Временное скрытие/изоляция снято.";
            });
        }

        private void upButton_Click(object sender, RoutedEventArgs e) => Step(true);

        private void downButton_Click(object sender, RoutedEventArgs e) => Step(false);

        /// <summary>К соседнему значению параметра из имеющихся в модели, сразу с изоляцией.</summary>
        private void Step(bool up)
        {
            string paramName = ParamName;
            string text = (valueBox.Text ?? "").Trim();
            FloorCheckRevitBridge.Enqueue(app =>
            {
                Document doc = app.ActiveUIDocument?.Document;
                if (doc == null)
                    return;
                List<string> values = FloorCheckActions.CollectValues(doc, paramName);
                SetValues(values);
                if (values.Count == 0)
                {
                    statusText.Text = "В модели нет значений «" + paramName + "».";
                    return;
                }

                int index = NeighbourIndex(values, text, up, paramName == FloorCheckActions.FloorNumberParam);
                if (index < 0 || index >= values.Count)
                {
                    statusText.Text = up ? "Это последнее значение в модели." : "Это первое значение в модели.";
                    return;
                }

                string value = values[index];
                valueBox.Text = value;
                Apply(app, paramName, value);
            });
        }

        /// <summary>
        /// Индекс соседнего значения. Число, которого нет в списке, — ближайшее больше/меньше;
        /// пустое или неизвестное значение — первое (^) или последнее (v).
        /// </summary>
        private static int NeighbourIndex(List<string> values, string text, bool up, bool isNumber)
        {
            int index = values.IndexOf(text);
            if (index >= 0)
                return up ? index + 1 : index - 1;

            if (isNumber && FloorCheckActions.TryParseNumber(text, out double current))
            {
                var numbers = values.Select(v => FloorCheckActions.TryParseNumber(v, out double d) ? d : 0).ToList();
                return up
                    ? numbers.FindIndex(n => n > current)
                    : numbers.FindLastIndex(n => n < current);
            }
            return up ? 0 : values.Count - 1;
        }

        private void Apply(UIApplication app, string paramName, string value)
        {
            int? count = FloorCheckActions.Apply(app, paramName, value);
            if (!count.HasValue)
                return;
            string shown = string.IsNullOrWhiteSpace(value) ? "без значения" : "= " + value.Trim();
            statusText.Text = count.Value > 0
                ? paramName + " " + shown + ": изолировано элементов — " + count.Value + "."
                : paramName + " " + shown + ": элементов не найдено.";
        }

        private void closeButton_Click(object sender, RoutedEventArgs e) => Close();

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
