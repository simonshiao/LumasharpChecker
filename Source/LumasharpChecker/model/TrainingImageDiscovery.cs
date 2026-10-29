using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace LumasharpChecker.model
{
    internal sealed class TrainingImageCandidate
    {
        public int Station { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public string? OkPath { get; set; }
        public string? NgPath { get; set; }
        public int OkCount { get; set; }
        public int NgCount { get; set; }
        public string Display => string.IsNullOrEmpty(FolderName) ? "不變更此站" : $"{FolderName}   OK {OkCount} / NG {NgCount}";
    }

    internal static class TrainingImageDiscovery
    {
        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

        // 從產品 CCD 資料夾或其子層辨識產品前綴；完整站號後必須是結尾或連字號。
        public static (string Root, string Product, List<TrainingImageCandidate> Candidates) Discover(string selectedPath, int stationCount)
        {
            if (!Directory.Exists(selectedPath)) throw new DirectoryNotFoundException("所選資料夾不存在。 ");
            var current = new DirectoryInfo(Path.GetFullPath(selectedPath));
            string? product = null;
            DirectoryInfo? root = null;
            for (; current != null; current = current.Parent)
            {
                var match = Regex.Match(current.Name, @"^(.+)-CCD(10|[1-9])(?:$|-)", RegexOptions.IgnoreCase);
                if (match.Success) product = match.Groups[1].Value;
                if (current.Name.Equals("TrainingImage", StringComparison.OrdinalIgnoreCase)) { root = current; break; }
            }
            if (root == null || string.IsNullOrWhiteSpace(product))
                throw new ArgumentException("請選取訓練圖庫中的產品站別資料夾，例如 R002-CCD1，或其內的站號／OK／NG 資料夾。 ");
            var candidates = new List<TrainingImageCandidate>();
            var pattern = "^" + Regex.Escape(product) + @"-CCD(10|[1-9])(?:$|-)";
            foreach (var folder in root.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                var match = Regex.Match(folder.Name, pattern, RegexOptions.IgnoreCase);
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out int station) || station > stationCount) continue;
                var original = Path.Combine(folder.FullName, "Origional", station.ToString());
                if (!Directory.Exists(original)) original = Path.Combine(folder.FullName, "Original", station.ToString());
                var ok = Path.Combine(original, "OK");
                var ng = Path.Combine(original, "NG");
                int okCount = CountImages(ok), ngCount = CountImages(ng);
                if (okCount == 0 && ngCount == 0) continue;
                candidates.Add(new TrainingImageCandidate { Station = station, FolderName = folder.Name,
                    OkPath = okCount > 0 ? ok : null, NgPath = ngCount > 0 ? ng : null,
                    OkCount = okCount, NgCount = ngCount });
            }
            if (candidates.Count == 0) throw new InvalidOperationException("找不到含圖片的 Origional\\站號\\OK／NG 資料夾。X／Y 圖庫未納入。 ");
            return (root.FullName, product, candidates);
        }

        private static int CountImages(string path) => Directory.Exists(path)
            ? Directory.EnumerateFiles(path).Count(file => Extensions.Contains(Path.GetExtension(file))) : 0;
    }
}
