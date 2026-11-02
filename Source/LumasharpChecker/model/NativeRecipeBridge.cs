using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
            public double StatisticsMs { get; set; }
            public double PreviewMs { get; set; }
            public double MessageMs { get; set; }
            public bool PresentationDeferred { get; set; }
            public bool IsConfigurationError => new[] { Locate, SelectWay, SelectFace }.Any(s => s == "NOT_CONFIGURED" || s == "STATION_DISABLED");
            public double InspectionMs => (LocateMs ?? 0) + (SelectWayMs ?? 0) + (SelectFaceMs ?? 0) + (LightMs ?? 0);
            public string TimingDetails =>
                $"工具合計 {InspectionMs:F1} ms；整體 {TotalMs:F1} ms\n" +
                $"配方比對／驗證 {PreparationMs:F1} ms；原生往返 {NativeRoundTripMs:F1} ms（內含工具計算、讀圖與傳輸）\n" +
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
                var total = Stopwatch.StartNew();
                PreparedRecipe native = Prepare(recipe, appBase);
                double preparationMs = total.Elapsed.TotalMilliseconds;
                string resultDir = Path.Combine(native.NativeRoot, "LumaProbe");
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
        public static void ShutdownWorker() => NativeFlowWorker.Shutdown();

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
        /// The previous native copy is kept as a sibling backup for recovery.</summary>
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
            string stage = Path.Combine(recipeRoot, ".luma-refresh-" + Guid.NewGuid().ToString("N"));
            string backup = Path.Combine(recipeRoot, name + ".luma-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(recipeRoot);
            try
            {
                foreach (string relative in files)
                {
                    string target = Path.Combine(stage, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(Path.Combine(source, relative), target, overwrite: false);
                }
                CompareExactFiles(source, stage, files);
                if (Directory.Exists(destination)) Directory.Move(destination, backup);
                try { Directory.Move(stage, destination); }
                catch
                {
                    if (Directory.Exists(backup) && !Directory.Exists(destination)) Directory.Move(backup, destination);
                    throw;
                }
                return backup;
            }
            catch
            {
                if (Directory.Exists(stage)) Directory.Delete(stage, recursive: true);
                throw;
            }
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
            var files = EnumerateSafeSnapshot(nativeRecipe);
            string stage = lumaSnapshot + ".edit-stage-" + Guid.NewGuid().ToString("N");
            string backup = lumaSnapshot + ".edit-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            try
            {
                foreach (string relative in files)
                {
                    string target = Path.Combine(stage, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(Path.Combine(nativeRecipe, relative), target, overwrite: false);
                }
                CompareExactFiles(nativeRecipe, stage, files);
                if (Directory.Exists(lumaSnapshot)) Directory.Move(lumaSnapshot, backup);
                try { Directory.Move(stage, lumaSnapshot); }
                catch
                {
                    if (Directory.Exists(backup) && !Directory.Exists(lumaSnapshot)) Directory.Move(backup, lumaSnapshot);
                    throw;
                }
            }
            catch
            {
                if (Directory.Exists(stage)) Directory.Delete(stage, recursive: true);
                throw;
            }
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
            using var sha = SHA256.Create();
            var identity = new System.Text.StringBuilder();
            foreach (string file in files)
            {
                using var a = File.OpenRead(Path.Combine(source, file));
                byte[] first = sha.ComputeHash(a);
                using var b = File.OpenRead(Path.Combine(destination, file));
                byte[] second = sha.ComputeHash(b);
                if (!first.SequenceEqual(second))
                    throw new InvalidDataException("原生配方與 Luma 快照不一致：" + file + "，禁止覆蓋。");
                identity.Append(file).Append(':').Append(Convert.ToBase64String(first)).Append('\n');
            }
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
