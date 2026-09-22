using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TNovCommon;

namespace TNovBIMUtils
{
    internal static class TDiameterResolver
    {
        static readonly Guid AdskNaimGuid = new Guid("e6e0f5cd-3e26-485b-9342-23882b20eb43");

        const int CatPipeAccessory = -2008055;
        const int CatPipeInsulation = -2008122;
        const int CatRebar = -2009000;
        const int CatGenericModel = -2000151;
        const int CatMechanicalEquipment = -2001140;
        const int CatPipeFitting = -2008049;
        const int CatPipe = -2008044;

        static readonly Regex RxNominal = new Regex(
            @"(?:^|[^A-Za-zА-Яа-яЁё])(?:Д[уУнН]?|D(?:[NnYy])?)\s*(\d+(?:[.,]\d+)?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        static readonly Regex RxDiameterSign = new Regex(
            @"[øØ⌀]\s*(?:[A-Za-zА-Яа-яЁё.]+\s+)?(\d+(?:[.,]\d+)?)",
            RegexOptions.Compiled);

        static readonly Regex RxExplicitD = new Regex(
            @"(?<![A-Za-zА-Яа-яЁё])[DdД]\s*=\s*(\d+(?:[.,]\d+)?)(?:\s*[-–—]\s*(\d+(?:[.,]\d+)?))?",
            RegexOptions.Compiled);

        static readonly Regex RxNxG = new Regex(
            @"(\d+(?:[.,]\d+)?)\s*[xхX×]\s*G",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        static readonly Regex RxInchPrefixed = new Regex(
            @"(?:^|[^A-Za-zА-Яа-яЁё])(?:G|R|ВР|НР)\s*(?:(\d+)\s+(\d+)\s*/\s*(\d+)|(\d+)\s*/\s*(\d+)|(\d+))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        static readonly Regex RxInchQuoted = new Regex(
            @"(?:(\d+)\s+(\d+)\s*/\s*(\d+)|(\d+)\s*/\s*(\d+)|(\d+))\s*(?:''|""|″|'|”|“)",
            RegexOptions.Compiled);

        static readonly Regex RxTiporazmer = new Regex(
            @"типоразмер\s+(\d+(?:[.,]\d+)?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        static readonly Regex RxMk = new Regex(
            @"МК-(\d+(?:[.,]\d+)?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        static readonly Regex RxMetricThread = new Regex(
            @"(?:^|[^A-Za-zА-Яа-яЁё])М([1-9]\d?)(?=[xх\s]|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        static readonly Regex RxCommaSize = new Regex(
            @",\s*(\d{2,3})(?!\d)(?!\s*[xхX×/])",
            RegexOptions.Compiled);

        static readonly Regex RxRangeMm = new Regex(
            @"(?<![A-Za-zА-Яа-яЁё=])(\d{2,3})\s*[-–]\s*(\d{2,3})\s*мм",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        static readonly Regex RxArticleDot = new Regex(
            @"(?<!\d)(\d{1,3})\.(\d{2})(?!\d)",
            RegexOptions.Compiled);

        static readonly Regex RxSizeNumbers = new Regex(
            @"\d+(?:[.,]\d+)?",
            RegexOptions.Compiled);

        public static bool IsTargetCategory(int categoryId)
        {
            return categoryId == CatPipeAccessory
                || categoryId == CatPipeInsulation
                || categoryId == CatRebar
                || categoryId == CatGenericModel
                || categoryId == CatMechanicalEquipment
                || categoryId == CatPipeFitting
                || categoryId == CatPipe;
        }

        public static void Apply(Document doc, Element elem, Parameter tParam)
        {
            if (tParam == null || tParam.IsReadOnly) return;
            if (tParam.StorageType != StorageType.Double) return;

            double value;
            if (!TryResolve(doc, elem, out value))
            {
                if (!tParam.HasValue) return;
                value = 0.0;
            }

            // повторная запись того же значения помечает элемент изменённым
            // и заново запускает обновители
            if (tParam.HasValue && Math.Abs(tParam.AsDouble() - value) < 1e-9) return;
            tParam.Set(value);
        }

        public static bool TryResolve(Document doc, Element elem, out double value)
        {
            value = 0;
            if (elem == null || elem.Category == null) return false;

            int categoryId = GetCategoryId(elem);
            string name = Param.GetStringParamValue(doc, AdskNaimGuid, elem) ?? "";
            string size = GetSizeString(doc, elem);

            TDiamException rule = FindException(name);
            if (rule != null)
            {
                if (string.Equals(rule.Source, TDiamException.SourceArticle, StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseArticleDot(name, out value) && value > 0) return true;
                    value = ParseFromSize(size);
                    return value > 0;
                }

                value = ParseFromSize(size);
                return value > 0;
            }

            double fromName = ParseFromName(name);
            if (fromName > 0)
            {
                value = fromName;
                return true;
            }

            if (categoryId == CatPipe)
            {
                string marking = Param.GetStringParamValue(doc, "Маркировка типоразмера", elem) ?? "";
                if (marking.IndexOf("Днар", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    double outer = GetLengthMm(doc, elem, BuiltInParameter.RBS_PIPE_OUTER_DIAMETER);
                    if (outer > 0)
                    {
                        value = outer;
                        return true;
                    }
                }
            }

            double fromSize = ParseFromSize(size);
            if (fromSize > 0)
            {
                value = fromSize;
                return true;
            }

            if (categoryId == CatPipe)
            {
                double nom = GetLengthMm(doc, elem, BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                if (nom > 0)
                {
                    value = nom;
                    return true;
                }
            }

            return false;
        }

        public static double ParseFromName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;

            double parsed;
            if (TryMaxFromRegex(name, RxNominal, 1, out parsed)) return parsed;
            if (TryMaxFromRegex(name, RxDiameterSign, 1, out parsed)) return parsed;
            if (TryParseExplicitD(name, out parsed)) return parsed;
            if (TryParseInches(name, out parsed)) return parsed;
            if (TryParseMisc(name, out parsed)) return parsed;
            return 0;
        }

        public static double ParseFromSize(string size)
        {
            if (string.IsNullOrWhiteSpace(size)) return 0;
            double max = 0;
            foreach (Match m in RxSizeNumbers.Matches(size))
            {
                double n;
                if (TryParseNum(m.Value, out n) && n > max) max = n;
            }
            return RoundDiam(max);
        }

        static TDiamException FindException(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            IReadOnlyList<TDiamException> rules = TDiamExceptionStore.GetRules();
            if (rules == null) return null;
            foreach (TDiamException rule in rules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.Match)) continue;
                if (name.IndexOf(rule.Match, StringComparison.OrdinalIgnoreCase) >= 0)
                    return rule;
            }
            return null;
        }

        static bool TryParseArticleDot(string name, out double value)
        {
            value = 0;
            bool found = false;
            foreach (Match m in RxArticleDot.Matches(name))
            {
                double n;
                if (TryParseNum(m.Groups[2].Value, out n) && n > value)
                {
                    value = n;
                    found = true;
                }
            }
            if (found) value = RoundDiam(value);
            return found && value > 0;
        }

        static bool TryParseExplicitD(string name, out double value)
        {
            value = 0;
            bool found = false;
            foreach (Match m in RxExplicitD.Matches(name))
            {
                double a;
                if (!TryParseNum(m.Groups[1].Value, out a)) continue;
                double local = a;
                if (m.Groups[2].Success)
                {
                    double b;
                    if (TryParseNum(m.Groups[2].Value, out b) && b > local) local = b;
                }
                if (local > value) value = local;
                found = true;
            }
            if (found) value = RoundDiam(value);
            return found && value > 0;
        }

        static bool TryParseInches(string name, out double value)
        {
            value = 0;
            bool found = false;

            foreach (Match m in RxNxG.Matches(name))
            {
                double n;
                if (TryParseNum(m.Groups[1].Value, out n) && n > value)
                {
                    value = n;
                    found = true;
                }
            }

            foreach (Match m in RxInchPrefixed.Matches(name))
            {
                double dn;
                if (TryMatchInch(m, out dn) && dn > value)
                {
                    value = dn;
                    found = true;
                }
            }

            foreach (Match m in RxInchQuoted.Matches(name))
            {
                double dn;
                if (TryMatchInch(m, out dn) && dn > value)
                {
                    value = dn;
                    found = true;
                }
            }

            if (found) value = RoundDiam(value);
            return found && value > 0;
        }

        static bool TryMatchInch(Match m, out double dn)
        {
            dn = 0;
            if (m.Groups[1].Success && m.Groups[2].Success && m.Groups[3].Success)
            {
                int whole, num, den;
                if (!int.TryParse(m.Groups[1].Value, out whole)) return false;
                if (!int.TryParse(m.Groups[2].Value, out num)) return false;
                if (!int.TryParse(m.Groups[3].Value, out den)) return false;
                return TryInchToDn(whole + (den == 0 ? 0 : (double)num / den), out dn);
            }
            if (m.Groups[4].Success && m.Groups[5].Success)
            {
                int num, den;
                if (!int.TryParse(m.Groups[4].Value, out num)) return false;
                if (!int.TryParse(m.Groups[5].Value, out den)) return false;
                return TryInchToDn(den == 0 ? 0 : (double)num / den, out dn);
            }
            if (m.Groups[6].Success)
            {
                int whole;
                if (!int.TryParse(m.Groups[6].Value, out whole)) return false;
                return TryInchToDn(whole, out dn);
            }
            return false;
        }

        static bool TryInchToDn(double inches, out double dn)
        {
            dn = 0;
            const double eps = 0.02;
            if (Near(inches, 0.25, eps)) { dn = 8; return true; }
            if (Near(inches, 0.375, eps)) { dn = 10; return true; }
            if (Near(inches, 0.5, eps)) { dn = 15; return true; }
            if (Near(inches, 0.75, eps)) { dn = 20; return true; }
            if (Near(inches, 1, eps)) { dn = 25; return true; }
            if (Near(inches, 1.25, eps)) { dn = 32; return true; }
            if (Near(inches, 1.5, eps)) { dn = 40; return true; }
            if (Near(inches, 2, eps)) { dn = 50; return true; }
            if (Near(inches, 2.5, eps)) { dn = 65; return true; }
            if (Near(inches, 3, eps)) { dn = 80; return true; }
            if (Near(inches, 4, eps)) { dn = 100; return true; }
            return false;
        }

        static bool Near(double a, double b, double eps) => Math.Abs(a - b) <= eps;

        static bool TryParseMisc(string name, out double value)
        {
            value = 0;
            if (TryMaxFromRegex(name, RxTiporazmer, 1, out value)) return true;
            if (TryMaxFromRegex(name, RxMk, 1, out value)) return true;
            if (name.IndexOf("шпильк", StringComparison.OrdinalIgnoreCase) >= 0
                && TryMaxFromRegex(name, RxMetricThread, 1, out value))
                return true;
            if (TryParseRangeMm(name, out value)) return true;
            if (TryMaxFromRegex(name, RxCommaSize, 1, out value)) return true;
            return false;
        }

        static bool TryParseRangeMm(string name, out double value)
        {
            value = 0;
            bool found = false;
            foreach (Match m in RxRangeMm.Matches(name))
            {
                double a, b;
                if (!TryParseNum(m.Groups[1].Value, out a)) continue;
                if (!TryParseNum(m.Groups[2].Value, out b)) continue;
                double local = a > b ? a : b;
                if (local > value) value = local;
                found = true;
            }
            if (found) value = RoundDiam(value);
            return found && value > 0;
        }

        static bool TryMaxFromRegex(string text, Regex rx, int group, out double value)
        {
            value = 0;
            bool found = false;
            foreach (Match m in rx.Matches(text))
            {
                if (!m.Groups[group].Success) continue;
                double n;
                if (TryParseNum(m.Groups[group].Value, out n) && n > value)
                {
                    value = n;
                    found = true;
                }
            }
            if (found) value = RoundDiam(value);
            return found && value > 0;
        }

        static bool TryParseNum(string s, out double n)
        {
            return double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out n);
        }

        static double RoundDiam(double value)
        {
            if (value <= 0) return 0;
            return Math.Round(value, MidpointRounding.AwayFromZero);
        }

        static string GetSizeString(Document doc, Element elem)
        {
            string s = GetNamedParamText(doc, elem, "Размер трубы");
            if (!string.IsNullOrWhiteSpace(s)) return s;

            s = ParameterToText(elem.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE));
            if (!string.IsNullOrWhiteSpace(s)) return s;

            s = GetNamedParamText(doc, elem, "Размер");
            if (!string.IsNullOrWhiteSpace(s)) return s;

            PipeInsulation insulation = elem as PipeInsulation;
            if (insulation != null)
            {
                ElementId hostId = insulation.HostElementId;
                if (hostId != null)
                {
#if R2022
                    long hostVal = hostId.IntegerValue;
#else
                    long hostVal = hostId.Value;
#endif
                    if (hostVal != -1)
                    {
                        Element host = doc.GetElement(hostId);
                        if (host != null && host.Id != elem.Id)
                            return GetSizeString(doc, host);
                    }
                }
            }
            return "";
        }

        static string GetNamedParamText(Document doc, Element elem, string name)
        {
            string s = ParameterToText(elem.LookupParameter(name));
            if (!string.IsNullOrWhiteSpace(s)) return s;

            s = Param.GetStringParamValue(doc, name, elem);
            if (!string.IsNullOrWhiteSpace(s)) return s;

            ElementId typeId = elem.GetTypeId();
            if (typeId == null) return "";
#if R2022
            long idv = typeId.IntegerValue;
#else
            long idv = typeId.Value;
#endif
            if (idv == -1) return "";
            Element type = doc.GetElement(typeId);
            if (type == null) return "";
            return ParameterToText(type.LookupParameter(name));
        }

        static string ParameterToText(Parameter p)
        {
            if (p == null || !p.HasValue) return "";
            if (p.StorageType == StorageType.String)
            {
                string s = p.AsString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
            string vs = p.AsValueString();
            if (!string.IsNullOrWhiteSpace(vs)) return vs;
            if (p.StorageType == StorageType.Double)
                return (p.AsDouble() * 304.8).ToString("0.###", CultureInfo.InvariantCulture);
            return "";
        }

        static double GetLengthMm(Document doc, Element elem, BuiltInParameter bip)
        {
            Parameter p = elem.get_Parameter(bip);
            if (p != null && p.HasValue)
            {
                double mm = Math.Round(p.AsDouble() * 304.8, MidpointRounding.AwayFromZero);
                if (mm > 0) return mm;
            }

            ElementId typeId = elem.GetTypeId();
            if (typeId == null) return 0;
#if R2022
            long idv = typeId.IntegerValue;
#else
            long idv = typeId.Value;
#endif
            if (idv == -1) return 0;
            Element type = doc.GetElement(typeId);
            if (type == null) return 0;
            p = type.get_Parameter(bip);
            if (p != null && p.HasValue)
            {
                double mm = Math.Round(p.AsDouble() * 304.8, MidpointRounding.AwayFromZero);
                if (mm > 0) return mm;
            }
            return 0;
        }

        static int GetCategoryId(Element elem)
        {
#if R2022
            return elem.Category.Id.IntegerValue;
#else
            return (int)elem.Category.Id.Value;
#endif
        }
    }
}
