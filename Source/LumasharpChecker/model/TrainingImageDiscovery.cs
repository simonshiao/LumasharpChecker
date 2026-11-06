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
        public bool ClearPaths { get; set; }
        public string Display => ClearPaths ? "清空本站圖庫（ALL／OK／NG）"
            : string.IsNullOrEmpty(FolderName) ? "不變更此站" : $"{FolderName}   OK {OkCount} / NG {NgCount}";
    }

    internal static class TrainingImageDiscovery
    {
        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

        // CCD is a camera identifier; actual station numbers come from Original/Origional children.
        private const string CameraPattern = @"^(.+)-CCD0*(10|[1-9])(?:$|-)";
        public static (string Root, string Product, List<TrainingImageCandidate> Candidates) Discover(string selectedPath, int stationCount)
        {
            if (!Directory.Exists(selectedPath)) throw new DirectoryNotFoundException("所選資料夾不存在。");
            var current = new DirectoryInfo(Path.GetFullPath(selectedPath));
            string? product = null;
            DirectoryInfo? root = null;
            for (; current != null; current = current.Parent)
            {
                var match = Regex.Match(current.Name, CameraPattern, RegexOptions.IgnoreCase);
                if (match.Success) product = match.Groups[1].Value;
                if (current.Name.Equals("TrainingImage", StringComparison.OrdinalIgnoreCase)) { root = current; break; }
            }
            if (root == null)
                throw new ArgumentException("請選取 TrainingImage、產品 CCD 資料夾，或其內的站號／OK／NG 資料夾。");
            var folders = root.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            if (string.IsNullOrWhiteSpace(product))
            {
                var products = folders.Select(d => Regex.Match(d.Name, CameraPattern, RegexOptions.IgnoreCase))
                    .Where(m => m.Success).Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (products.Length == 0) throw new ArgumentException("TrainingImage 中找不到產品 CCD 資料夾，例如 20160H-CCD01 或 R002-CCD1。");
                if (products.Length > 1) throw new ArgumentException("TrainingImage 中有多個產品前綴，請選取要匯入的產品 CCD 資料夾。");
                product = products[0];
            }
            var candidates = new List<TrainingImageCandidate>();
            var pattern = "^" + Regex.Escape(product) + @"-CCD0*(10|[1-9])(?:$|-)";
            foreach (var folder in folders)
            {
                if (!Regex.IsMatch(folder.Name, pattern, RegexOptions.IgnoreCase)) continue;
                foreach (var original in folder.EnumerateDirectories().Where(d =>
                    d.Name.Equals("Origional", StringComparison.OrdinalIgnoreCase) || d.Name.Equals("Original", StringComparison.OrdinalIgnoreCase)))
                foreach (var stationFolder in original.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(stationFolder.Name, out int station) || station < 1 || station > stationCount) continue;
                    var ok = Path.Combine(stationFolder.FullName, "OK");
                    var ng = Path.Combine(stationFolder.FullName, "NG");
                    int okCount = CountImages(ok), ngCount = CountImages(ng);
                    if (okCount == 0 && ngCount == 0) continue;
                    candidates.Add(new TrainingImageCandidate { Station = station, FolderName = folder.Name,
                        OkPath = okCount > 0 ? ok : null, NgPath = ngCount > 0 ? ng : null,
                        OkCount = okCount, NgCount = ngCount });
                }
            }
            if (candidates.Count == 0) throw new InvalidOperationException("找不到含圖片的 Origional／Original 站號下 OK／NG 資料夾，X／Y 圖庫不納入。");
            return (root.FullName, product!, candidates.OrderBy(c => c.Station).ThenBy(c => c.FolderName, StringComparer.OrdinalIgnoreCase).ToList());
        }

        private static int CountImages(string path) => Directory.Exists(path)
            ? Directory.EnumerateFiles(path).Count(file => Extensions.Contains(Path.GetExtension(file))) : 0;
    }
}
