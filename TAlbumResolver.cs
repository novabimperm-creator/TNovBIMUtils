using Autodesk.Revit.DB;
using System;
using TNovCommon;

namespace TNovBIMUtils
{
    /// <summary>
    /// Значение Т_Альбом для моделей ОВ ВК.
    /// ВК1 / ВК / ПТ — по имени модели; ОВ — по ADSK_Группирование:
    /// начинается с «Т» — «ОВ1», иначе — «ОВ2».
    /// </summary>
    public static class TAlbumResolver
    {
        public static readonly Guid TAlbumParamGuid = new Guid("7a5b2f12-13c8-4157-92c3-24ff5ccfafd7");//Т_Альбом
        static readonly Guid adskGparamGuid = new Guid("3de5f1a4-d560-4fa8-a74f-25d250fb3401");//ADSK_Группирование

        //значение Т_Альбом в моделях ОВ вычисляется по ADSK_Группирование
        const string AlbumByGrouping = "*ОВ*";

        /// <summary>
        /// Альбом по имени модели: фиксированное значение, AlbumByGrouping для ОВ,
        /// null — модель не участвует. ВК1 проверяется раньше ВК, т.к. "-ВК1" содержит "-ВК".
        /// </summary>
        public static string GetAlbumByDocument(string docName)
        {
            if (string.IsNullOrEmpty(docName)) return null;
            if (docName.Contains("-ВК1") || docName.Contains("_ВК1")) return "ВК1";
            if (docName.Contains("-ВК") || docName.Contains("_ВК")) return "ВК";
            if (docName.Contains("-ПТ") || docName.Contains("_ПТ")) return "ПТ";
            if (docName.Contains("-ОВ") || docName.Contains("_ОВ")) return AlbumByGrouping;
            return null;
        }

        /// <summary>Итоговое значение Т_Альбом для элемента.</summary>
        public static string Resolve(Document doc, Element elem, string documentAlbum)
        {
            if (documentAlbum != AlbumByGrouping) return documentAlbum;
            string grouping = Param.GetStringParamValue(doc, adskGparamGuid, elem) ?? "";
            return grouping.StartsWith("Т", StringComparison.Ordinal) ? "ОВ1" : "ОВ2";
        }
    }
}
