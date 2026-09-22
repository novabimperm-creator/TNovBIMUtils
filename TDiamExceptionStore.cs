using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using TNovCommon;

namespace TNovBIMUtils
{
    internal static class TDiamExceptionStore
    {
        public const string FileName = "TDiamExceptions.json";

        /// <summary>Как часто перепроверять файл правил на диске (сервер может быть недоступен/медленным).</summary>
        static readonly TimeSpan RevalidateInterval = TimeSpan.FromSeconds(30);

        static string _filePathCached;
        static string _cachedPath;
        static DateTime _cachedWriteTime;
        static DateTime _lastCheckUtc = DateTime.MinValue;
        static List<TDiamException> _cached;

        public static string GetFilePath()
        {
            if (_filePathCached != null)
                return _filePathCached.Length == 0 ? null : _filePathCached;
            try
            {
                TNovConfig config = TNovConfigLoad.LoadConfig();
                if (config == null || string.IsNullOrEmpty(config.ServerPath))
                {
                    _filePathCached = "";
                    return null;
                }
                _filePathCached = config.ServerPath + FileName;
                return _filePathCached;
            }
            catch
            {
                _filePathCached = "";
                return null;
            }
        }

        public static List<TDiamException> Defaults()
        {
            return new List<TDiamException>
            {
                new TDiamException { Match = "K-FLEX", Source = TDiamException.SourceSize },
                new TDiamException { Match = "LD Pride", Source = TDiamException.SourceArticle }
            };
        }

        public static List<TDiamException> Load()
        {
            return Clone(GetRules());
        }

        /// <summary>
        /// Правила для чтения. Вызывается на каждый элемент из обновителя,
        /// поэтому файл на сервере опрашивается не чаще RevalidateInterval,
        /// а результат отдаётся без копирования.
        /// </summary>
        public static List<TDiamException> GetRules()
        {
            DateTime nowUtc = DateTime.UtcNow;
            if (_cached != null && nowUtc - _lastCheckUtc < RevalidateInterval)
                return _cached;

            string path = GetFilePath();
            DateTime writeTime = DateTime.MinValue;
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    writeTime = File.GetLastWriteTimeUtc(path);
            }
            catch
            {
                // сервер недоступен — работаем на том, что уже есть в кэше
                if (_cached != null)
                {
                    _lastCheckUtc = nowUtc;
                    return _cached;
                }
            }

            _lastCheckUtc = nowUtc;
            if (_cached != null && _cachedPath == path && _cachedWriteTime == writeTime)
                return _cached;

            _cached = ReadFromDisk(path);
            _cachedPath = path;
            _cachedWriteTime = writeTime;
            return _cached;
        }

        public static void Save(IEnumerable<TDiamException> rules)
        {
            string path = GetFilePath();
            if (string.IsNullOrEmpty(path))
                throw new InvalidOperationException("Не задан путь к серверной папке _TNov.");

            var list = new List<TDiamException>();
            if (rules != null)
            {
                foreach (TDiamException rule in rules)
                {
                    if (rule == null || string.IsNullOrWhiteSpace(rule.Match)) continue;
                    list.Add(new TDiamException
                    {
                        Match = rule.Match.Trim(),
                        Source = NormalizeSource(rule.Source)
                    });
                }
            }

            File.WriteAllText(path, JsonConvert.SerializeObject(list, Formatting.Indented));
            Invalidate();
        }

        public static void Invalidate()
        {
            _cached = null;
            _cachedPath = null;
            _cachedWriteTime = DateTime.MinValue;
            _lastCheckUtc = DateTime.MinValue;
        }

        static List<TDiamException> ReadFromDisk(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return Defaults();

            try
            {
                string json = File.ReadAllText(path);
                var list = JsonConvert.DeserializeObject<List<TDiamException>>(json);
                if (list == null) return Defaults();
                foreach (TDiamException rule in list)
                    rule.Source = NormalizeSource(rule.Source);
                return list;
            }
            catch
            {
                return Defaults();
            }
        }

        static string NormalizeSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return TDiamException.SourceSize;
            if (source.IndexOf("артикул", StringComparison.OrdinalIgnoreCase) >= 0
                || source.IndexOf("article", StringComparison.OrdinalIgnoreCase) >= 0)
                return TDiamException.SourceArticle;
            return TDiamException.SourceSize;
        }

        static List<TDiamException> Clone(List<TDiamException> source)
        {
            var copy = new List<TDiamException>();
            if (source == null) return copy;
            foreach (TDiamException rule in source)
            {
                copy.Add(new TDiamException
                {
                    Match = rule.Match,
                    Source = rule.Source
                });
            }
            return copy;
        }
    }
}
