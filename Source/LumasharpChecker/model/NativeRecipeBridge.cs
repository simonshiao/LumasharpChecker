using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LumasharpChecker.model
{
    /// <summary>
    /// Bridges Luma's existing, SHA-verified vendor-source snapshot to the
    /// decoded InspTools R3 Recipe loader. No Recipe conversion or AI startup.
    /// The native portable application is isolated under Luma's AppBase/NativeR3.
    /// </summary>
    public static class NativeRecipeBridge
    {
        private static string NativeName(RecipeManager.RecipeManifest recipe)
        {
            if (!string.IsNullOrWhiteSpace(recipe.VendorRecipePath))
                return Path.GetFileName(recipe.VendorRecipePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (recipe.LocalNativeRecipeName == "LUMA-" + recipe.Id) return recipe.LocalNativeRecipeName;
            throw new InvalidOperationException("目前配方尚未建立原生參數，請建立新配方或匯入原廠配方。");
        }

        public static void InitializeLocalRecipe(RecipeManager.RecipeManifest recipe, string appBase)
        {
            if (!Guid.TryParseExact(recipe.Id, "N", out _)) throw new InvalidDataException("配方識別碼無效。");
            string name = "LUMA-" + recipe.Id;
            string root = Path.Combine(Path.GetFullPath(appBase), "NativeR3");
            string cli = Path.Combine(root, "InspToolsRecipeCli.exe");
            if (!File.Exists(cli)) throw new FileNotFoundException("找不到 R3 配方建立工具。", cli);
            var info = new ProcessStartInfo(cli, "--recipe-create-empty " + name)
            { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
              RedirectStandardOutput = true, RedirectStandardError = true };
            using var process = Process.Start(info) ?? throw new IOException("無法啟動配方建立工具。");
            if (!process.WaitForExit(120000)) { process.Kill(); throw new TimeoutException("建立原生配方逾時。"); }
            string error = process.StandardError.ReadToEnd();
            if (process.ExitCode != 0) throw new InvalidDataException("建立原生配方失敗：" + error);
            string native = Path.Combine(root, "Recipe", name);
            string baseline = FindT1StationOneBaseline(appBase);
            InitializeNewRecipeParameterRanges(native, baseline);
            InitializeNewRecipeLightRoi(appBase, recipe.Id);
            var baselineSize = FindT1StationOneImageSize(appBase, baseline);
            if (baselineSize.Width > 0 && baselineSize.Height > 0)
            {
                foreach (var station in recipe.Stations.Where(item => item.Number <= Math.Min(8, recipe.StationCount)))
                {
                    station.InitialRoiScalePending = true;
                    station.InitialRoiReferenceWidth = baselineSize.Width;
                    station.InitialRoiReferenceHeight = baselineSize.Height;
                }
            }
            string snapshot = Path.Combine(Path.GetFullPath(appBase), "recipes", recipe.Id, "vendor-source");
            var files = EnumerateSafeSnapshot(native);
            foreach (string relative in files)
            {
                string target = Path.Combine(snapshot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(native, relative), target, false);
            }
            CompareExactFiles(native, snapshot, files);
            recipe.LocalNativeRecipeName = name;
        }

        private static (int Width, int Height) FindT1StationOneImageSize(string appBase, string baselinePath)
        {
            string? baselineDirectory = Path.GetDirectoryName(baselinePath);
            if (baselineDirectory != null && Path.GetFileName(baselineDirectory).Equals("vendor-source", StringComparison.OrdinalIgnoreCase))
            {
                string? t1RecipeDirectory = Directory.GetParent(baselineDirectory)?.FullName;
                string stationDirectory = Path.Combine(t1RecipeDirectory ?? string.Empty, RecipeManager.GetStationDirectoryName(1));
                if (Directory.Exists(stationDirectory))
                {
                    foreach (string imagePath in Directory.EnumerateFiles(stationDirectory)
                                 .Where(path => Path.GetFileName(path).IndexOf("_pattern", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        try
                        {
                            using Image image = Image.FromFile(imagePath);
                            if (image.Width > 0 && image.Height > 0) return (image.Width, image.Height);
                        }
                        catch (OutOfMemoryException) { }
                        catch (ArgumentException) { }
                    }
                }
            }

            string settingsPath = Path.Combine(Path.GetFullPath(appBase), "RecipeDefaults", "t1-light-roi.json");
            if (File.Exists(settingsPath))
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(settingsPath));
                JsonElement root = document.RootElement;
                int width = root.TryGetProperty("ReferenceImageWidth", out JsonElement widthElement) ? widthElement.GetInt32() : 0;
                int height = root.TryGetProperty("ReferenceImageHeight", out JsonElement heightElement) ? heightElement.GetInt32() : 0;
                return (width, height);
            }
            return (0, 0);
        }

        /// <summary>
        /// Uses t1 Station 1 as the parameter baseline for every station in a new recipe.
        /// Recipe-local station switches and external data/model paths remain owned by the
        /// newly generated recipe; inspection limits are then widened for first-time setup.
        /// </summary>
        private static string FindT1StationOneBaseline(string appBase)
        {
            string recipesFolder = Path.Combine(Path.GetFullPath(appBase), "recipes");
            foreach (string directory in Directory.EnumerateDirectories(recipesFolder))
            {
                string manifestPath = Path.Combine(directory, "recipe.json");
                if (!File.Exists(manifestPath)) continue;
                try
                {
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
                    if (!document.RootElement.TryGetProperty("name", out JsonElement name)
                        || !string.Equals(name.GetString(), "t1", StringComparison.OrdinalIgnoreCase)) continue;
                    string candidate = Path.Combine(directory, "vendor-source", "station1.ini");
                    if (File.Exists(candidate)) return candidate;
                }
                catch (JsonException)
                {
                    // Ignore unrelated or partially written manifests and continue searching.
                }
            }
            string packagedBaseline = Path.Combine(Path.GetFullPath(appBase), "RecipeDefaults", "t1-station1.ini");
            if (File.Exists(packagedBaseline)) return packagedBaseline;
            throw new FileNotFoundException("找不到 t1 配方第一站的參數檔，無法建立預設基準。", recipesFolder);
        }

        private static void InitializeNewRecipeLightRoi(string appBase, string recipeId)
        {
            // Light ROI margins belong to the located object, not the full sensor image.
            // The editor initializes each station from its actual located dimensions.
            for (int station = 1; station <= 8; station++)
            {
                string folder = Path.Combine(Path.GetFullPath(appBase), "recipes", recipeId,
                    RecipeManager.GetStationDirectoryName(station));
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "light-inspection.json");
                if (File.Exists(path)) continue;
                var settings = new LightInspectionSettings { HasRoi = false };
                File.WriteAllText(path, JsonSerializer.Serialize(settings,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
        }

        public static bool ApplyInitialRoiScaleForImage(RecipeManager recipeManager,
            RecipeManager.RecipeManifest recipe, int stationNumber, string imagePath, string appBase)
        {
            if (recipeManager == null) throw new ArgumentNullException(nameof(recipeManager));
            if (recipe == null || recipe.LocalNativeRecipeName != "LUMA-" + recipe.Id) return false;
            var station = recipe.Stations.FirstOrDefault(item => item.Number == stationNumber);
            if (station == null || !station.InitialRoiScalePending) return false;
            if (station.InitialRoiReferenceWidth <= 0 || station.InitialRoiReferenceHeight <= 0 ||
                string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath)) return false;

            int targetWidth, targetHeight;
            using (Image image = Image.FromFile(imagePath))
            {
                targetWidth = image.Width;
                targetHeight = image.Height;
            }
            if (targetWidth <= 0 || targetHeight <= 0) return false;

            string source = Path.Combine(Path.GetFullPath(appBase), "recipes", recipe.Id,
                "vendor-source", "station" + stationNumber + ".ini");
            if (!File.Exists(source)) throw new FileNotFoundException("新配方缺少初始定位參數。", source);
            string native = Path.Combine(Path.GetFullPath(appBase), "NativeR3", "Recipe",
                recipe.LocalNativeRecipeName, "station" + stationNumber + ".ini");
            byte[] sourceBackup = File.ReadAllBytes(source);
            byte[]? nativeBackup = File.Exists(native) ? File.ReadAllBytes(native) : null;
            if (nativeBackup != null && !sourceBackup.SequenceEqual(nativeBackup))
                throw new InvalidDataException("新配方的隔離定位參數與來源不一致，未套用初始 ROI 縮放。");

            Encoding encoding = GetIniEncoding(sourceBackup);
            string[] lines = File.ReadAllLines(source);
            bool changed = ScaleInitialRoiLines(lines, station.InitialRoiReferenceWidth,
                station.InitialRoiReferenceHeight, targetWidth, targetHeight);
            try
            {
                if (changed)
                {
                    WriteIniAtomically(source, lines, encoding);
                    if (nativeBackup != null) WriteIniAtomically(native, lines, encoding);
                }
                station.InitialRoiScalePending = false;
                recipeManager.SaveManifest(recipe);
                return changed;
            }
            catch
            {
                File.WriteAllBytes(source, sourceBackup);
                if (nativeBackup != null) File.WriteAllBytes(native, nativeBackup);
                station.InitialRoiScalePending = true;
                throw;
            }
        }

        private static bool ScaleInitialRoiLines(string[] lines, int referenceWidth, int referenceHeight,
            int targetWidth, int targetHeight)
        {
            string[] prefixes = { "MaxSearchRect", "SIMPSearchRect", "LocateSearchRect",
                "QuadSearchRectL", "QuadSearchRectR", "QuadSearchRectT", "QuadSearchRectB",
                "Quad_Mask_T", "Quad_Mask_B", "Quad_Mask_L", "Quad_Mask_R",
                "PadSearchRectL", "PadSearchRectR" };
            var allowed = new HashSet<string>(prefixes, StringComparer.OrdinalIgnoreCase);
            var sections = GetIniSections(lines);
            double scaleX = (double)targetWidth / referenceWidth;
            double scaleY = (double)targetHeight / referenceHeight;
            bool changed = false;

            foreach (IniSectionRange section in sections)
            {
                var groups = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
                var lineIndexes = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
                for (int index = section.Start; index < section.End; index++)
                {
                    string key = GetIniKey(lines[index]);
                    int separator = key.LastIndexOf('_');
                    if (separator <= 0) continue;
                    string prefix = key.Substring(0, separator);
                    string axis = key.Substring(separator + 1).ToUpperInvariant();
                    if (!allowed.Contains(prefix) || !IsOneOf(axis, "X", "Y", "W", "H") ||
                        !int.TryParse(GetIniValue(lines[index]), System.Globalization.NumberStyles.Integer,
                            System.Globalization.CultureInfo.InvariantCulture, out int value)) continue;
                    if (!groups.TryGetValue(prefix, out var values))
                    {
                        values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        groups[prefix] = values;
                        lineIndexes[prefix] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    }
                    values[axis] = value;
                    lineIndexes[prefix][axis] = index;
                }

                foreach (var pair in groups)
                {
                    if (!new[] { "X", "Y", "W", "H" }.All(pair.Value.ContainsKey)) continue;
                    int x = ScaleCoordinate(pair.Value["X"], scaleX, targetWidth);
                    int y = ScaleCoordinate(pair.Value["Y"], scaleY, targetHeight);
                    int width = ScaleLength(pair.Value["W"], scaleX, targetWidth - x);
                    int height = ScaleLength(pair.Value["H"], scaleY, targetHeight - y);
                    var scaled = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    { ["X"] = x, ["Y"] = y, ["W"] = width, ["H"] = height };
                    foreach (var value in scaled)
                    {
                        int lineIndex = lineIndexes[pair.Key][value.Key];
                        if (pair.Value[value.Key] == value.Value) continue;
                        lines[lineIndex] = SetIniValue(lines[lineIndex], value.Value.ToString(
                            System.Globalization.CultureInfo.InvariantCulture));
                        changed = true;
                    }
                }
            }
            return changed;
        }

        private static int ScaleCoordinate(int value, double scale, int limit)
        {
            int scaled = (int)Math.Round(value * scale, MidpointRounding.AwayFromZero);
            return Math.Max(0, Math.Min(Math.Max(0, limit - 1), scaled));
        }

        private static int ScaleLength(int value, double scale, int remaining)
        {
            int scaled = (int)Math.Round(value * scale, MidpointRounding.AwayFromZero);
            return Math.Max(0, Math.Min(Math.Max(0, remaining), scaled));
        }

        private static void WriteIniAtomically(string path, string[] lines, Encoding encoding)
        {
            string temporary = path + ".roi-scale-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllLines(temporary, lines, encoding);
                File.Replace(temporary, path, null);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static void InitializeNewRecipeParameterRanges(string nativeRecipeFolder, string baselinePath)
        {
            string[] baselineLines = File.ReadAllLines(baselinePath);
            List<IniSectionRange> baselineSections = GetIniSections(baselineLines);
            for (int station = 1; station <= 8; station++)
            {
                string path = Path.Combine(nativeRecipeFolder, "station" + station + ".ini");
                if (!File.Exists(path))
                    throw new FileNotFoundException("新配方缺少站別參數檔，無法套用預設檢查範圍。", path);

                byte[] bytes = File.ReadAllBytes(path);
                Encoding encoding = GetIniEncoding(bytes);
                string[] lines = File.ReadAllLines(path);
                var sections = GetIniSections(lines);
                bool changed = ApplyStationOneBaseline(lines, sections, baselineLines, baselineSections);

                foreach (var section in sections)
                {
                    bool isLocate = string.Equals(section.Name, "Locate", StringComparison.OrdinalIgnoreCase);
                    bool isSelectWay = section.Name.StartsWith("SelectWayTool", StringComparison.OrdinalIgnoreCase);
                    bool supportsFaceSelection = SectionHasKey(lines, section, "EnableSelectFace");

                    if (!isLocate && !isSelectWay && !supportsFaceSelection) continue;

                    for (int lineIndex = section.Start; lineIndex < section.End; lineIndex++)
                    {
                        string key = GetIniKey(lines[lineIndex]);
                        if (string.IsNullOrEmpty(key)) continue;

                        bool isQuadBinaryDefault = IsOneOf(key,
                            "Quad_THD_Max_L", "Quad_THD_Max_R", "Quad_THD_Max_T", "Quad_THD_Max_B");
                        bool isRange =
                            (isLocate && IsOneOf(key, "LocateHeightMin", "LocateHeightMax", "LocateWidthMin", "LocateWidthMax")) ||
                            (isSelectWay && (IsInspectionRuleRange(key) || IsOneOf(key, "MatchScoreMin", "MatchScoreMax"))) ||
                            (supportsFaceSelection && IsFaceInspectionRange(key));
                        if (!isRange && !isQuadBinaryDefault) continue;

                        string value = isQuadBinaryDefault ? "185" :
                            key.EndsWith("Min", StringComparison.OrdinalIgnoreCase) ? "0" : "99999";
                        string updated = SetIniValue(lines[lineIndex], value);
                        if (!string.Equals(updated, lines[lineIndex], StringComparison.Ordinal))
                        {
                            lines[lineIndex] = updated;
                            changed = true;
                        }
                    }
                }

                if (changed) File.WriteAllLines(path, lines, encoding);
            }
        }

        private static bool ApplyStationOneBaseline(string[] targetLines,
            List<IniSectionRange> targetSections, string[] baselineLines,
            List<IniSectionRange> baselineSections)
        {
            bool changed = false;
            foreach (IniSectionRange baselineSection in baselineSections)
            {
                IniSectionRange? targetSection = targetSections.FirstOrDefault(section =>
                    string.Equals(section.Name, baselineSection.Name, StringComparison.OrdinalIgnoreCase));
                if (targetSection == null) continue;

                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int index = baselineSection.Start; index < baselineSection.End; index++)
                {
                    string key = GetIniKey(baselineLines[index]);
                    if (string.IsNullOrEmpty(key) || IsStationSpecificValue(baselineSection.Name, key)) continue;
                    values[key] = GetIniValue(baselineLines[index]);
                }

                for (int index = targetSection.Start; index < targetSection.End; index++)
                {
                    string key = GetIniKey(targetLines[index]);
                    if (!values.TryGetValue(key, out string? value)) continue;
                    string updated = SetIniValue(targetLines[index], value);
                    if (!string.Equals(updated, targetLines[index], StringComparison.Ordinal))
                    {
                        targetLines[index] = updated;
                        changed = true;
                    }
                }
            }
            return changed;
        }

        private static bool IsStationSpecificValue(string section, string key)
        {
            if (section.Equals("Main", StringComparison.OrdinalIgnoreCase)
                && (key.Equals("StationON", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("Initialized", StringComparison.OrdinalIgnoreCase))) return true;
            return key.IndexOf("Path", StringComparison.OrdinalIgnoreCase) >= 0
                || key.IndexOf("Folder", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetIniValue(string line)
        {
            int equals = line.IndexOf('=');
            if (equals < 0) return string.Empty;
            string value = line.Substring(equals + 1).Trim();
            int commentIndex = value.IndexOfAny(new[] { ';', '#' });
            return (commentIndex < 0 ? value : value.Substring(0, commentIndex)).Trim();
        }

        private sealed class IniSectionRange
        {
            public string Name { get; set; } = "";
            public int Start { get; set; }
            public int End { get; set; }
        }

        private static List<IniSectionRange> GetIniSections(string[] lines)
        {
            var sections = new List<IniSectionRange>();
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (line.Length < 3 || line[0] != '[' || line[line.Length - 1] != ']') continue;
                if (sections.Count > 0) sections[sections.Count - 1].End = index;
                sections.Add(new IniSectionRange
                {
                    Name = line.Substring(1, line.Length - 2).Trim(),
                    Start = index + 1,
                    End = lines.Length
                });
            }
            return sections;
        }

        private static bool SectionHasKey(string[] lines, IniSectionRange section, string expectedKey)
        {
            for (int index = section.Start; index < section.End; index++)
                if (string.Equals(GetIniKey(lines[index]), expectedKey, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string GetIniKey(string line)
        {
            int equals = line.IndexOf('=');
            return equals < 0 ? "" : line.Substring(0, equals).Trim();
        }

        private static string SetIniValue(string line, string value)
        {
            int equals = line.IndexOf('=');
            if (equals < 0) return line;
            string right = line.Substring(equals + 1);
            int commentIndex = right.IndexOfAny(new[] { ';', '#' });
            string comment = commentIndex < 0 ? "" : " " + right.Substring(commentIndex).TrimStart();
            return line.Substring(0, equals + 1) + " " + value + comment;
        }

        private static Encoding GetIniEncoding(byte[] bytes)
        {
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return Encoding.Unicode;
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) return Encoding.BigEndianUnicode;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return new UTF8Encoding(true);
            return Encoding.Default;
        }

        private static bool IsInspectionRuleRange(string key) =>
            System.Text.RegularExpressions.Regex.IsMatch(key, @"^InspRule[1-4]_(Min|Max)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        private static bool IsFaceInspectionRange(string key)
        {
            if (key.IndexOf("THD", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return IsInspectionRuleRange(key)
                || System.Text.RegularExpressions.Regex.IsMatch(key, @"^MB_InspRule[1-4]_(Min|Max)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                || System.Text.RegularExpressions.Regex.IsMatch(key, @"^BlobLine(Min|Max|WidthMin|WidthMax|HeightMin|HeightMax|AreaMin|AreaMax)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                || IsOneOf(key, "WidthMin", "WidthMax", "HeightMin", "HeightMax", "AreaMin", "AreaMax", "MatchScoreMin", "MatchScoreMax");
        }

        private static bool IsOneOf(string value, params string[] choices) =>
            choices.Any(choice => string.Equals(value, choice, StringComparison.OrdinalIgnoreCase));

        public sealed class PreparedRecipe
        {
            public string Name { get; }
            public string NativeRoot { get; }
            public string Folder { get; }
            public int FileCount { get; }
            public string LumaSnapshot { get; }
            public string Revision { get; }
            internal PreparedRecipe(string name, string root, string folder, int count, string lumaSnapshot, string revision)
            {
                Name = name; NativeRoot = root; Folder = folder; FileCount = count; LumaSnapshot = lumaSnapshot; Revision = revision;
            }
        }

        public sealed class StageReport
        {
            public string RequestId { get; set; } = "";
            public int WorkerProcessId { get; set; }
            public bool RecipeReused { get; set; }
            public double WorkerMs { get; set; }
            public double TotalMs { get; set; }
            public double PreparationMs { get; set; }
            public double NativeRoundTripMs { get; set; }
            public double ImageReadMs { get; set; }
            public double FrameExportMs { get; set; }
            public double StatisticsMs { get; set; }
            public double PreviewMs { get; set; }
            public double MessageMs { get; set; }
            public bool PresentationDeferred { get; set; }
            public bool IsConfigurationError => new[] { Locate, SelectWay, SelectFace }.Any(s => s == "NOT_CONFIGURED" || s == "STATION_DISABLED");
            public double InspectionMs => (LocateMs ?? 0) + (SelectWayMs ?? 0) + (SelectFaceMs ?? 0) + (LightMs ?? 0);
            public string TimingDetails =>
                $"工具合計 {InspectionMs:F1} ms；整體 {TotalMs:F1} ms\n" +
                $"配方比對／驗證 {PreparationMs:F1} ms；原生往返 {NativeRoundTripMs:F1} ms（內含工具計算、讀圖與傳輸）\n" +
                $"原生讀圖 {ImageReadMs:F1} ms；導正圖輸出 {FrameExportMs:F1} ms\n" +
                (PresentationDeferred ? "連續模式：本張未更新畫面與統計，批次結束後只顯示最後一張。" :
                $"清單／統計／結果綁定 {StatisticsMs:F1} ms；預覽載入 {PreviewMs:F1} ms；訊息更新 {MessageMs:F1} ms");
            public string Recipe { get; set; } = "";
            public int Station { get; set; }
            public string Image { get; set; } = "";
            public string Locate { get; set; } = "";
            public string SelectWay { get; set; } = "";
            public string SelectFace { get; set; } = "";
            public string Error { get; set; } = "";
            public bool AiExecuted { get; set; }
            public int Exit { get; set; }
            public double? LocateMs { get; set; }
            public double? SelectWayMs { get; set; }
            public double? SelectFaceMs { get; set; }
            public int? FaceIndex { get; set; }
            public string SelectWayDetail { get; set; } = "";
            public string OutputImage { get; set; } = "";
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int ImageX { get; set; }
            public int ImageY { get; set; }
            public double? LightMs { get; set; }
            public string Light { get; set; } = "";

            public string Summary =>
                "定位：" + Name(Locate) + "　選向：" + Name(SelectWay) +
                "　選面：" + Name(SelectFace) + (FaceIndex.HasValue ? "（" + "ABCD"[FaceIndex.Value - 1] + "）" : "");

            private static string Name(string value) => value switch
            {
                "PASS" => "通過",
                "FAIL" => "失敗",
                "FLIPPED" => "已執行／已翻轉",
                "UNCHANGED" => "已執行／未翻轉",
                "SKIPPED_RECIPE_OFF" => "已略過",
                "NOT_CONFIGURED" => "未設定",
                "STATION_DISABLED" => "站別未啟用",
                _ => string.IsNullOrWhiteSpace(value) ? "未執行" : value
            };
        }

        /// <summary>Call recovered native Locate/SelectWay/SelectFace in a
        /// reusable isolated worker, skipping vendor AI. The native light bulb is not
        /// called; Luma performs its own brightness measurement afterward.</summary>
        public static Task<StageReport> RunPreLightAsync(
            RecipeManager.RecipeManifest recipe, int station, string image, string appBase,
            string stopAfter = "light", string selection = null)
        {
            return Task.Run(async () =>
            {
                if (station < 1 || station > 8)
                    throw new ArgumentOutOfRangeException(nameof(station));
                if (!new[] { "locate", "way", "face", "light" }.Contains(stopAfter))
                    throw new ArgumentException("無效的原生檢查階段。", nameof(stopAfter));
                if (selection != null && (selection.Length != 3 || selection.Any(c => c != '0' && c != '1')))
                    throw new ArgumentException("無效的工具選擇。", nameof(selection));
                if (string.IsNullOrWhiteSpace(image) || !File.Exists(image))
                    throw new FileNotFoundException("請先載入本站 OK DB 圖片。", image);
                // Defer old probe cleanup until a user starts an inspection. Startup stays read-only.
                CleanupStaleArtifacts();
                var total = Stopwatch.StartNew();
                PreparedRecipe native = Prepare(recipe, appBase);
                double preparationMs = total.Elapsed.TotalMilliseconds;
                string resultDir = SessionProbeDirectory;
                Directory.CreateDirectory(resultDir);
                string result = Path.Combine(resultDir, "flow-" + Guid.NewGuid().ToString("N") + ".json");
                var roundTrip = Stopwatch.StartNew();
                var report = await NativeFlowWorker.InspectAsync(native, station, image, result, stopAfter, selection).ConfigureAwait(false);
                roundTrip.Stop(); report.PreparationMs = preparationMs; report.NativeRoundTripMs = roundTrip.Elapsed.TotalMilliseconds;
                total.Stop(); report.TotalMs = total.Elapsed.TotalMilliseconds;
                return report;
            });
        }

        private static readonly object PreparationSync = new object();
        private static string _validatedKey = "";
        private static string ProbeRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LumasharpChecker", "LumaProbe");
        private static readonly string SessionProbeDirectory = Path.Combine(
            Path.GetFullPath(ProbeRoot),
            "session-" + Process.GetCurrentProcess().Id + "-" + Guid.NewGuid().ToString("N"));

        public static void ShutdownWorker() => NativeFlowWorker.Shutdown();

        public static void CleanupSessionArtifacts()
        {
            string probeRoot = Path.GetFullPath(ProbeRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string sessionDirectory = Path.GetFullPath(SessionProbeDirectory);
            if (sessionDirectory.StartsWith(probeRoot, StringComparison.OrdinalIgnoreCase))
            {
                string relative = sessionDirectory.Substring(probeRoot.Length);
                if (relative.StartsWith("session-", StringComparison.OrdinalIgnoreCase) &&
                    relative.IndexOf(Path.DirectorySeparatorChar) < 0 &&
                    relative.IndexOf(Path.AltDirectorySeparatorChar) < 0 &&
                    Directory.Exists(sessionDirectory))
                {
                    DeleteGeneratedProbeFiles(sessionDirectory);
                    try { Directory.Delete(sessionDirectory, recursive: false); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        /// <summary>
        /// Removes only probe artifacts created by this app. Older builds wrote
        /// flow-{guid}.bmp/json files directly under LumaProbe and could leave
        /// multiple gigabytes behind after a crash. Current session folders are
        /// removed only when their recorded process is no longer running.
        /// </summary>
        public static void CleanupStaleArtifacts()
        {
            // Only clean the per-user flow cache; application binaries stay untouched.
            CleanupStaleArtifactsInRoot(ProbeRoot);
        }

        private static void CleanupStaleArtifactsInRoot(string root)
        {
            string probeRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(probeRoot) || IsReparsePoint(probeRoot)) return;
            string prefix = probeRoot + Path.DirectorySeparatorChar;

            DeleteGeneratedProbeFiles(probeRoot);

            foreach (string directory in Directory.EnumerateDirectories(probeRoot, "session-*", SearchOption.TopDirectoryOnly))
            {
                string fullPath = Path.GetFullPath(directory);
                string name = Path.GetFileName(fullPath);
                var match = System.Text.RegularExpressions.Regex.Match(name,
                    @"^session-(\d+)-[0-9a-f]{32}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !match.Success || IsReparsePoint(fullPath)) continue;

                int processId;
                if (!int.TryParse(match.Groups[1].Value, out processId)) continue;
                bool processAlive;
                try { using var process = Process.GetProcessById(processId); processAlive = !process.HasExited; }
                catch (ArgumentException) { processAlive = false; }
                catch (InvalidOperationException) { processAlive = false; }
                if (processAlive) continue;

                DeleteGeneratedProbeFiles(fullPath);
                try { Directory.Delete(fullPath, recursive: false); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static void DeleteGeneratedProbeFiles(string directory)
        {
            if (!Directory.Exists(directory) || IsReparsePoint(directory)) return;
            foreach (string file in Directory.EnumerateFiles(directory, "flow-*", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileName(file);
                if (!System.Text.RegularExpressions.Regex.IsMatch(name,
                    @"^flow-[0-9a-f]{32}\.(bmp|json)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                    IsReparsePoint(file)) continue;
                try { File.Delete(file); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static bool IsReparsePoint(string path)
        {
            try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
            catch (IOException) { return true; }
            catch (UnauthorizedAccessException) { return true; }
        }

        public static bool IsTrustedProbeImage(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath)) return false;
            try
            {
                string fullPath = Path.GetFullPath(imagePath);
                string fileName = Path.GetFileName(fullPath);
                if (!System.Text.RegularExpressions.Regex.IsMatch(fileName,
                    @"^flow-[0-9a-f]{32}\.(bmp|png)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return false;

                string rootPath = Path.GetFullPath(ProbeRoot)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string prefix = rootPath + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || IsReparsePoint(rootPath)) return false;

                string relative = fullPath.Substring(prefix.Length);
                string[] parts = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                    StringSplitOptions.RemoveEmptyEntries);
                return parts.Length == 2 &&
                    System.Text.RegularExpressions.Regex.IsMatch(parts[0],
                        @"^session-\d+-[0-9a-f]{32}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) &&
                    System.Text.RegularExpressions.Regex.IsMatch(parts[1],
                        @"^flow-[0-9a-f]{32}\.bmp$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) &&
                    !IsReparsePoint(Path.Combine(rootPath, parts[0])) && !IsReparsePoint(fullPath);
            }
            catch (ArgumentException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (NotSupportedException) { }
            return false;
        }

        public static PreparedRecipe Prepare(RecipeManager.RecipeManifest recipe, string appBase)
        {
            lock (PreparationSync) return PrepareCore(recipe, appBase);
        }

        private static PreparedRecipe PrepareCore(RecipeManager.RecipeManifest recipe, string appBase)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (!Guid.TryParseExact(recipe.Id, "N", out _))
                throw new InvalidDataException("配方識別碼無效。");
            string vendorName = NativeName(recipe);
            if (string.IsNullOrWhiteSpace(vendorName) || vendorName == "." || vendorName == ".." ||
                vendorName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                vendorName.IndexOfAny(new[] {'/', '\\', ':'}) >= 0)
                throw new InvalidDataException("原廠配方名稱不安全，無法載入。");

            string source = Path.Combine(Path.GetFullPath(appBase), "recipes", recipe.Id, "vendor-source");
            string nativeRoot = Path.Combine(Path.GetFullPath(appBase), "NativeR3");
            string nativeExecutable = Path.Combine(nativeRoot, "InspToolsStandalone.exe");
            string recipeCli = Path.Combine(nativeRoot, "InspToolsRecipeCli.exe");
            string runtimeDll = Path.Combine(nativeRoot, "InspTools.dll");
            if (!File.Exists(nativeExecutable) || !File.Exists(recipeCli) || !File.Exists(runtimeDll))
                throw new FileNotFoundException("找不到隔離的 InspTools R3。請先部署已驗證的 R3 可攜版到 App-net472-Slim/NativeR3。");
            if (!File.Exists(Path.Combine(nativeRoot, "portable-review.marker")) ||
                File.ReadAllText(Path.Combine(nativeRoot, "portable-review.marker")).Trim() !=
                    "INSPTOOLS_PORTABLE_REVIEW_R3_V1")
                throw new InvalidOperationException("InspTools R3 隔離執行環境尚未通過 Portable Review Gate。");

            var sourceFiles = EnumerateSafeSnapshot(source);
            string destination = Path.Combine(nativeRoot, "Recipe", vendorName);
            if (!Directory.Exists(destination))
            {
                string stage = Path.Combine(nativeRoot, "Recipe", ".luma-stage-" + Guid.NewGuid().ToString("N"));
                try
                {
                    foreach (string relative in sourceFiles)
                    {
                        string target = Path.Combine(stage, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        File.Copy(Path.Combine(source, relative), target, overwrite: false);
                    }
                    CompareExactFiles(source, stage, sourceFiles);
                    // Never overwrite an existing native Recipe, even if another process creates it.
                    Directory.Move(stage, destination);
                }
                catch
                {
                    if (Directory.Exists(stage)) Directory.Delete(stage, recursive: true);
                    throw;
                }
            }
            // An already-loaded R3 Recipe must be identical to the Luma snapshot.
            // A mismatch is a hard error, never an implicit overwrite.
            string revision = CompareExactFiles(source, destination, sourceFiles);
            string validationKey = source + "|" + nativeRoot + "|" + vendorName + "|" + revision + "|" +
                File.GetLastWriteTimeUtc(recipeCli).Ticks + "|" + File.GetLastWriteTimeUtc(runtimeDll).Ticks;
            if (_validatedKey != validationKey)
            {
                CheckViaOriginalCli(recipeCli, nativeRoot, vendorName, destination);
                _validatedKey = validationKey;
            }
            return new PreparedRecipe(vendorName, nativeRoot, destination, sourceFiles.Count, source, revision);
        }

        /// <summary>Refreshes the isolated native Recipe from the imported Luma snapshot.
        /// The rollback copy is removed after a verified successful replacement.</summary>
        public static string RefreshNativeCopy(RecipeManager.RecipeManifest recipe, string appBase)
        {
            ShutdownWorker();
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            string source = Path.Combine(Path.GetFullPath(appBase), "recipes", recipe.Id, "vendor-source");
            var files = EnumerateSafeSnapshot(source);
            string nativeRoot = Path.Combine(Path.GetFullPath(appBase), "NativeR3");
            string name = NativeName(recipe);
            string recipeRoot = Path.Combine(nativeRoot, "Recipe");
            string destination = Path.Combine(recipeRoot, name);
            RecipeSnapshotStorage.Replace(source, destination, () => { });
            return destination;
        }

        // R3 portable-review may allow editing the isolated Recipe copy.
        // Never label this launcher read-only; the production source is protected
        // by staging a separate, SHA-verified local copy.
        public static Task OpenSandbox(PreparedRecipe prepared, int station, bool openLocate = false,
            IEnumerable<string> imagePaths = null, string currentImage = null, string tool = null)
        {
            ShutdownWorker();
            if (station < 1 || station > 8)
                throw new ArgumentOutOfRangeException(nameof(station));
            string exe = Path.Combine(prepared.NativeRoot, "InspToolsStandalone.exe");
            string imageArgs = "";
            if ((openLocate || tool == "way" || tool == "face") && imagePaths != null)
            {
                var images = imagePaths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                int selected = Array.FindIndex(images, x => string.Equals(x, currentImage, StringComparison.OrdinalIgnoreCase));
                if (selected < 0 && File.Exists(currentImage))
                {
                    images = (new[] { currentImage }).Concat(images).ToArray();
                    selected = 0;
                }
                if (images.Length > 0)
                {
                    string folder = Path.Combine(prepared.NativeRoot, "_luma_image_lists");
                    Directory.CreateDirectory(folder);
                    string listFile = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".txt");
                    File.WriteAllLines(listFile, images);
                    imageArgs = " --luma-images \"" + listFile + "\" --luma-index " + Math.Max(0, selected);
                }
            }
            var start = new ProcessStartInfo(exe,
                "--recipe-native-r3 \"" + prepared.Name + "\" --station " + station +
                (openLocate ? " --open-locate" : "") +
                (tool == "way" ? " --open-select-way" : tool == "face" ? " --open-select-face" : "") + imageArgs)
            {
                WorkingDirectory = prepared.NativeRoot,
                UseShellExecute = false
            };
            var process = Process.Start(start) ?? throw new IOException("原生 InspTools R3 無法啟動。");
            string lumaSnapshot = prepared.LumaSnapshot;
            string nativeRecipe = prepared.Folder;
            return Task.Run(() =>
            {
                try
                {
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                        throw new IOException("R3 未成功儲存，未同步配方。退出碼：" + process.ExitCode);
                    SyncSavedNativeCopy(nativeRecipe, lumaSnapshot);
                }
                finally { process.Dispose(); }
            });
        }

        private static void SyncSavedNativeCopy(string nativeRecipe, string lumaSnapshot)
        {
            EnumerateSafeSnapshot(nativeRecipe);
            RecipeSnapshotStorage.Replace(nativeRecipe, lumaSnapshot, () => { });
        }

        private static List<string> EnumerateSafeSnapshot(string source)
        {
            if (!Directory.Exists(source) || IsLink(source))
                throw new DirectoryNotFoundException("Luma 的原廠 Recipe 副本不存在或包含連結。");
            if (!File.Exists(Path.Combine(source, "Setting.ini")) ||
                !Enumerable.Range(1, 8).All(i => File.Exists(Path.Combine(source, "station" + i + ".ini"))))
                throw new InvalidDataException("原廠配方副本不完整：需 Setting.ini 與 station1～8.ini。");
            var relative = new List<string>();
            Visit(source, source, relative);
            return relative.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void Visit(string root, string dir, List<string> files)
        {
            if (IsLink(dir)) throw new InvalidDataException("原廠配方內有資料夾連結：" + dir);
            foreach (string file in Directory.EnumerateFiles(dir))
            {
                if (IsLink(file)) throw new InvalidDataException("原廠配方內有檔案連結：" + file);
                files.Add(file.Substring(root.Length + 1));
            }
            foreach (string child in Directory.EnumerateDirectories(dir)) Visit(root, child, files);
        }

        private static bool IsLink(string path) =>
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

        private static string CompareExactFiles(string source, string destination, IReadOnlyList<string> files)
        {
            if (!Directory.Exists(destination) || IsLink(destination))
                throw new InvalidDataException("原生配方不是正常資料夾。");
            var found = new List<string>();
            Visit(destination, destination, found);
            if (found.Count != files.Count ||
                !found.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                     .SequenceEqual(files, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("原生配方已存在但檔案清單不同，禁止覆蓋。");
            var digests = new string[files.Count];
            try
            {
                System.Threading.Tasks.Parallel.For(0, files.Count,
                    new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount) }, i =>
                    {
                        using var digest = new SHA256Cng();
                        using var a = File.OpenRead(Path.Combine(source, files[i]));
                        byte[] first = digest.ComputeHash(a);
                        using var b = File.OpenRead(Path.Combine(destination, files[i]));
                        if (!first.SequenceEqual(digest.ComputeHash(b)))
                            throw new InvalidDataException("原生配方與 Luma 快照不一致：" + files[i] + "，禁止覆蓋。");
                        digests[i] = Convert.ToBase64String(first);
                    });
            }
            catch (AggregateException ex)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.Flatten().InnerExceptions[0]).Throw();
                throw;
            }
            var identity = new System.Text.StringBuilder();
            for (int i = 0; i < files.Count; i++) identity.Append(files[i]).Append(':').Append(digests[i]).Append('\n');
            using var sha = new SHA256Cng();
            return Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(identity.ToString())));
        }

        private static void CheckViaOriginalCli(string cli, string cwd, string recipeName, string folder)
        {
            var info = new ProcessStartInfo(cli, "--recipe-check \"" + recipeName + "\"")
            {
                WorkingDirectory = cwd,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(info) ?? throw new IOException("原廠 R3 Recipe CLI 啟動失敗。");
            if (!process.WaitForExit(10000))
            {
                process.Kill();
                throw new TimeoutException("原生 Recipe CLI 驗證逾時。");
            }
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            // The existing CLI's exit code is its contract: CheckStructure()
            // returns 0 only for a valid native Recipe. A GUI parent process
            // may not receive the console's text in the same encoding, so do
            // not reject a successful validation based on the printed path.
            // CompareExactFiles() already verified each file and its SHA-256.
            if (process.ExitCode != 0)
                throw new InvalidDataException("原生 InspTools R3 無法接受此配方：" +
                    (string.IsNullOrWhiteSpace(error) ? output : error));
        }
    }
}
