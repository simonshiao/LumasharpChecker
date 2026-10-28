using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Emgu.CV;

namespace LumasharpChecker.model
{
    /// <summary>站別定位模板庫。主 pattern.jpg 永不覆寫，附加模板存放 stationXX/patterns。</summary>
    public sealed class StationPatternLibrary
    {
        public const int MaxTemplates = 8; // 主樣板 + 最多七張額外樣板
        private static readonly HashSet<string> Extensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp" };
        private static readonly Regex VariantName =
            new Regex(@"^pattern_[0-9]{2}\.(jpg|jpeg|png|bmp)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public string PrimaryPath { get; }
        public string VariantDirectory { get; }
        private readonly Size _primarySize;
        private readonly Rectangle _patternRoi;

        public StationPatternLibrary(string primaryPath, Rectangle patternRoi)
        {
            PrimaryPath = Path.GetFullPath(primaryPath);
            if (!File.Exists(PrimaryPath))
                throw new FileNotFoundException("目前沒有定位樣板，請先完成新增參數。", PrimaryPath);
            using (Mat image = CvInvoke.Imread(PrimaryPath))
            {
                if (image.IsEmpty) throw new InvalidOperationException("主樣板無法載入。");
                _primarySize = image.Size;
            }
            _patternRoi = patternRoi;
            if (patternRoi.Width <= 0 || patternRoi.Height <= 0 ||
                Rectangle.Intersect(patternRoi, new Rectangle(Point.Empty, _primarySize)) != patternRoi)
                throw new InvalidOperationException("主樣板的定位 ROI 不完整，請先校準。");
            VariantDirectory = Path.Combine(Path.GetDirectoryName(PrimaryPath)!, "patterns");
        }

        public IReadOnlyList<string> AdditionalPaths =>
            Directory.Exists(VariantDirectory)
                ? Directory.EnumerateFiles(VariantDirectory)
                    .Where(f => VariantName.IsMatch(Path.GetFileName(f)))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList()
                : Array.Empty<string>();

        public int AddFiles(IEnumerable<string> paths)
        {
            var candidates = paths.Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (candidates.Count == 0) return 0;
            var current = AdditionalPaths;
            if (current.Count + candidates.Count >= MaxTemplates)
                throw new InvalidOperationException($"同一站最多 {MaxTemplates} 張樣板（含原始樣板）。目前已有 {current.Count + 1} 張。");

            // 先驗證所有選取圖片，再進行檔案寫入，避免尺寸不符時只存一部分。
            foreach (string path in candidates)
            {
                if (!File.Exists(path) || !Extensions.Contains(Path.GetExtension(path)))
                    throw new InvalidOperationException("找不到可讀取的圖片：" + path);
                using Mat mat = CvInvoke.Imread(path);
                if (mat.IsEmpty || mat.Size != _primarySize)
                    throw new InvalidOperationException("附加樣板尺寸必須與原樣板一致：" + Path.GetFileName(path));
            }

            Directory.CreateDirectory(VariantDirectory);
            var alreadyUsed = new HashSet<int>(current
                .Select(file => int.TryParse(Path.GetFileNameWithoutExtension(file).Substring(8),
                    out int number) ? number : 0));
            int saved = 0;
            foreach (string path in candidates)
            {
                int index = Enumerable.Range(2, MaxTemplates - 1)
                    .FirstOrDefault(number => !alreadyUsed.Contains(number));
                if (index == 0) throw new InvalidOperationException("樣板數量已達上限。");
                string target = Path.Combine(VariantDirectory, $"pattern_{index:00}{Path.GetExtension(path).ToLowerInvariant()}");
                if (!string.Equals(path, target, StringComparison.OrdinalIgnoreCase))
                    File.Copy(path, target, overwrite: false);
                alreadyUsed.Add(index);
                saved++;
            }
            return saved;
        }

        public void Remove(string selectedPath)
        {
            string path = Path.GetFullPath(selectedPath);
            string parent = Path.GetDirectoryName(path)!;
            if (!string.Equals(parent.TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(VariantDirectory).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase) ||
                !VariantName.IsMatch(Path.GetFileName(path)))
                throw new InvalidOperationException("只能刪除此站附加的定位樣板，不能刪除原始 pattern.jpg。");
            File.Delete(path);
        }
    }
}
