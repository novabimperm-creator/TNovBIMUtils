using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace TNovBIMUtils
{
    [Transaction(TransactionMode.Manual)]
    public class AssignMaterialCodesCommand : IExternalCommand
    {
        // Встроенный словарь с данными из Excel
        private static readonly Dictionary<string, string> MaterialCodes = new Dictionary<string, string>
        {
            // Основные классы бетона
            { "В7.5", "1007000000" },
            { "В15", "1015000000" },
            { "В25", "1025000000" },
            { "В30", "1030000000" },
            { "В35", "1035000000" },
            { "В40", "1040000000" },
            
            // В25 с различными характеристиками
            { "В25 W4 F75", "1025004075" },
            { "В25 W4 F100", "1025004100" },
            { "В25 W4 F150", "1025004150" },
            { "В25 W4 F200", "1025004200" },
            { "В25 W6 F75", "1025006075" },
            { "В25 W6 F100", "1025006100" },
            { "В25 W6 F150", "1025006150" },
            { "В25 W6 F200", "1025006200" },
            
            // В30 с различными характеристиками
            { "В30 W4 F75", "1030004075" },
            { "В30 W4 F100", "1030004100" },
            { "В30 W4 F150", "1030004150" },
            { "В30 W4 F200", "1030004200" },
            { "В30 W6 F100", "1030006100" },
            { "В30 W6 F150", "1030006150" },
            { "В30 W6 F200", "1030006200" },
            
            // В35 с различными характеристиками
            { "В35 W4 F75", "1035004075" },
            { "В35 W4 F100", "1035004100" },
            { "В35 W4 F150", "1035004150" },
            { "В35 W4 F200", "1035004200" },
            { "В35 W6 F100", "1035006100" },
            { "В35 W6 F150", "1035006150" },
            { "В35 W6 F200", "1035006200" },
            
            // В40 с различными характеристиками
            { "В40 W4 F75", "1040004075" },
            { "В40 W4 F100", "1040004100" },
            { "В40 W4 F150", "1040004150" },
            { "В40 W4 F200", "1040004200" },
            { "В40 W6 F75", "1040006075" },
            { "В40 W6 F100", "1040006100" },
            { "В40 W6 F150", "1040006150" },
            { "В40 W6 F200", "1040006200" }
        };

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Получение всех материалов в проекте
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            ICollection<Element> materials = collector.OfClass(typeof(Material)).ToElements();

            int totalMaterials = materials.Count;
            int concreteMaterials = 0;
            int updatedCount = 0;
            int notFoundCount = 0;

            // Собираем информацию о материалах, для которых не найден код
            List<string> notFoundMaterials = new List<string>();

            using (Transaction trans = new Transaction(doc, "Назначение кодов материалов"))
            {
                trans.Start();

                foreach (Element element in materials)
                {
                    Material material = element as Material;
                    if (material == null) continue;

                    string materialName = material.Name;

                    // Пропускаем материалы, не содержащие слово "Бетон" (регистронезависимо)
                    if (!ContainsConcrete(materialName))
                    {
                        continue;
                    }

                    concreteMaterials++;

                    // Извлекаем характеристики бетона из имени материала
                    string concreteInfo = ExtractConcreteInfo(materialName);

                    if (!string.IsNullOrWhiteSpace(concreteInfo))
                    {
                        // Поиск кода по характеристикам бетона
                        string code = FindMaterialCode(concreteInfo);

                        if (!string.IsNullOrEmpty(code))
                        {
                            // Запись кода в параметр
                            Parameter codeParam = material.LookupParameter("N_Код материала");
                            if (codeParam != null && codeParam.StorageType == StorageType.Integer&&int.TryParse(code,out int intcode))
                            {
                                codeParam.Set(intcode);
                                updatedCount++;
                            }
                            else
                            {
                                // Если параметр не существует
                                notFoundMaterials.Add($"{materialName} (отсутствует параметр 'N_Код материала')");
                            }
                        }
                        else
                        {
                            // Код не найден
                            notFoundMaterials.Add($"{materialName} -> характеристики: {concreteInfo}");
                            notFoundCount++;
                        }
                    }
                    else
                    {
                        // Не удалось извлечь характеристики бетона
                        notFoundMaterials.Add($"{materialName} (не удалось извлечь характеристики)");
                        notFoundCount++;
                    }
                }

                trans.Commit();
            }

            // Формирование отчета
            string report = $"Всего материалов: {totalMaterials}\n" +
                          $"Материалов с бетоном: {concreteMaterials}\n" +
                          $"Назначено кодов: {updatedCount}\n" +
                          $"Не найдено соответствий: {notFoundCount}";

            // Если есть материалы без кода, показываем детали
            if (notFoundCount > 0)
            {
                string details = string.Join("\n", notFoundMaterials);
                TaskDialog dialog = new TaskDialog("Результат назначения кодов материалов");
                dialog.MainInstruction = report;
                dialog.MainContent = $"Следующие материалы не были обработаны:\n\n{details}";
                dialog.CommonButtons = TaskDialogCommonButtons.Ok;
                dialog.Show();
            }
            else if (concreteMaterials > 0)
            {
                TaskDialog.Show("Завершено", report);
            }
            else
            {
                TaskDialog.Show("Информация", "В проекте не найдено материалов, содержащих слово 'Бетон'");
            }

            return Result.Succeeded;
        }

        /// <summary>
        /// Проверяет, содержит ли имя материала слово "Бетон" (в любом регистре)
        /// </summary>
        private bool ContainsConcrete(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
                return false;

            return materialName.IndexOf("бетон", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   materialName.IndexOf("beton", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Извлекает характеристики бетона из имени материала
        /// </summary>
        private string ExtractConcreteInfo(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
                return string.Empty;

            // Приведение к верхнему регистру
            string upperName = materialName.ToUpper();

            // Замена латинской B на кириллическую В
            upperName = upperName.Replace("B", "В");

            // Замена русской "Ф" на латинскую "F"
            upperName = upperName.Replace("Ф", "F");

            // Извлекаем компоненты
            List<string> components = new List<string>();

            // Ищем класс бетона (В7.5, В15, В25 и т.д.)
            string concreteClass = ExtractConcreteClass(upperName);
            if (!string.IsNullOrEmpty(concreteClass))
            {
                components.Add(concreteClass);
            }

            // Ищем марку по водонепроницаемости (W4, W6 и т.д.)
            string waterproof = ExtractWaterproof(upperName);
            if (!string.IsNullOrEmpty(waterproof))
            {
                components.Add(waterproof);
            }

            // Ищем марку по морозостойкости (F75, F100 и т.д.)
            string frostResistance = ExtractFrostResistance(upperName);
            if (!string.IsNullOrEmpty(frostResistance))
            {
                components.Add(frostResistance);
            }

            // Если не нашли класс бетона, возвращаем пустую строку
            if (components.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(" ", components);
        }

        /// <summary>
        /// Извлекает класс бетона из строки
        /// </summary>
        private string ExtractConcreteClass(string upperName)
        {
            // Разбиваем строку на слова и ищем класс бетона
            string[] words = upperName.Split(new[] { ' ', ',', ';', '(', ')', '[', ']', '{', '}', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string word in words)
            {
                // Проверяем, начинается ли слово с В (кириллица) и содержит цифры
                if (word.StartsWith("В") && word.Length > 1)
                {
                    // Проверяем, есть ли цифры после В
                    bool hasDigits = false;
                    for (int i = 1; i < word.Length; i++)
                    {
                        if (char.IsDigit(word[i]) || word[i] == '.' || word[i] == ',')
                        {
                            hasDigits = true;
                        }
                        else if (word[i] == ' ' || word[i] == '-' || word[i] == '_')
                        {
                            // Прерываем, если встретили разделитель
                            break;
                        }
                    }

                    if (hasDigits)
                    {
                        // Извлекаем только класс бетона (В7.5, В15 и т.д.)
                        string concreteClass = "В";
                        for (int i = 1; i < word.Length; i++)
                        {
                            if (char.IsDigit(word[i]) || word[i] == '.' || word[i] == ',')
                            {
                                concreteClass += word[i];
                            }
                            else
                            {
                                break;
                            }
                        }

                        // Заменяем запятую на точку
                        concreteClass = concreteClass.Replace(',', '.');

                        return concreteClass;
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Извлекает марку по водонепроницаемости из строки
        /// </summary>
        private string ExtractWaterproof(string upperName)
        {
            // Разбиваем строку на слова и ищем W
            string[] words = upperName.Split(new[] { ' ', ',', ';', '(', ')', '[', ']', '{', '}', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string word in words)
            {
                // Проверяем, начинается ли слово с W и содержит цифры
                if (word.StartsWith("W") && word.Length > 1 && char.IsDigit(word[1]))
                {
                    // Извлекаем только W и цифры
                    string waterproof = "W";
                    for (int i = 1; i < word.Length; i++)
                    {
                        if (char.IsDigit(word[i]))
                        {
                            waterproof += word[i];
                        }
                        else
                        {
                            break;
                        }
                    }

                    return waterproof;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Извлекает марку по морозостойкости из строки
        /// </summary>
        private string ExtractFrostResistance(string upperName)
        {
            // Разбиваем строку на слова и ищем F
            string[] words = upperName.Split(new[] { ' ', ',', ';', '(', ')', '[', ']', '{', '}', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string word in words)
            {
                // Проверяем, начинается ли слово с F и содержит цифры
                if (word.StartsWith("F") && word.Length > 1 && char.IsDigit(word[1]))
                {
                    // Извлекаем только F и цифры
                    string frostResistance = "F";
                    for (int i = 1; i < word.Length; i++)
                    {
                        if (char.IsDigit(word[i]))
                        {
                            frostResistance += word[i];
                        }
                        else
                        {
                            break;
                        }
                    }

                    return frostResistance;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Ищет код материала по характеристикам бетона
        /// </summary>
        private string FindMaterialCode(string concreteInfo)
        {
            if (string.IsNullOrEmpty(concreteInfo))
                return null;

            // Прямое совпадение
            if (MaterialCodes.ContainsKey(concreteInfo))
            {
                return MaterialCodes[concreteInfo];
            }

            // Если не найдено полное совпадение, пробуем найти только по классу бетона
            string[] parts = concreteInfo.Split(' ');
            if (parts.Length > 0)
            {
                string concreteClass = parts[0];
                if (MaterialCodes.ContainsKey(concreteClass))
                {
                    return MaterialCodes[concreteClass];
                }
            }

            return null;
        }
    }
}