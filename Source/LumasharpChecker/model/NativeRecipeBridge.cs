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
        public sealed class PreparedRecipe
        {
            public string Name { get; }
            public string NativeRoot { get; }
            public string Folder { get; }
            public int FileCount { get; }
            internal PreparedRecipe(string name, string root, string folder, int count)
            {
                Name = name; NativeRoot = root; Folder = folder; FileCount = count;
            }
        }

        public sealed class StageReport
        {
            public string Recipe { get; set; } = "";
            public int Station { get; set; }
            public string Image { get; set; } = "";
            public string Locate { get; set; } = "";
            public string SelectWay { get; set; } = "";
            public string SelectFace { get; set; } = "";
            public string Error { get; set; } = "";
            public bool AiExecuted { get; set; }
            public int Exit { get; set; }

            public string Summary =>
                "定位：" + Name(Locate) + "　翻轉：" + Name(SelectWay) +
                "　認面：" + Name(SelectFace);

            private static string Name(string value) => value switch
            {
                "PASS" => "通過",
                "FAIL" => "失敗",
                "SKIPPED_RECIPE_OFF" => "Recipe 關閉／跳過",
                "NOT_CONFIGURED" => "未設定",
                "STATION_DISABLED" => "站別未啟用",
                _ => string.IsNullOrWhiteSpace(value) ? "未執行" : value
            };
        }

        /// <summary>Call recovered native Locate/SelectWay/SelectFace in an
        /// isolated subprocess, skipping vendor AI. The native light bulb is not
        /// called; Luma performs its own brightness measurement afterward.</summary>
        public static Task<StageReport> RunPreLightAsync(
            RecipeManager.RecipeManifest recipe, int station, string image, string appBase,
            string stopAfter = "light")
        {
            return Task.Run(() =>
            {
                if (station < 1 || station > 8)
                    throw new ArgumentOutOfRangeException(nameof(station));
                if (!new[] { "locate", "way", "face", "light" }.Contains(stopAfter))
                    throw new ArgumentException("無效的原生檢查階段。", nameof(stopAfter));
                if (string.IsNullOrWhiteSpace(image) || !File.Exists(image))
                    throw new FileNotFoundException("請先載入本站 OK DB 圖片。", image);
                PreparedRecipe native = Prepare(recipe, appBase);
                string exe = Path.Combine(native.NativeRoot, "LumaNativeFlowHost.exe");
                if (!File.Exists(exe))
                    throw new FileNotFoundException("缺少隔離原生非 AI 流程執行器。", exe);
                string resultDir = Path.Combine(native.NativeRoot, "LumaProbe");
                Directory.CreateDirectory(resultDir);
                string result = Path.Combine(resultDir, "flow-" + Guid.NewGuid().ToString("N") + ".json");
                var start = new ProcessStartInfo(exe,
                    "\"" + native.Name + "\" \"" + Path.GetFullPath(image) +
                    "\" \"" + result + "\" " + station + " " + stopAfter)
                {
                    WorkingDirectory = native.NativeRoot, UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(start) ??
                    throw new IOException("無法啟動原生非 AI 流程。");
                if (!process.WaitForExit(25000))
                {
                    process.Kill();
                    process.WaitForExit(3000);
                    throw new TimeoutException("原生定位流程超時，已停止此隔離子程序。");
                }
                if (!File.Exists(result))
                    throw new InvalidDataException("原生流程沒有回傳結果；ExitCode=" + process.ExitCode);
                StageReport report;
                using (var stream = File.OpenRead(result))
                    report = JsonSerializer.Deserialize<StageReport>(stream,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
                        throw new InvalidDataException("原生流程回傳空結果。");
                if (report.AiExecuted)
                    throw new InvalidOperationException("原生流程異常執行了 AI，禁止光源檢測。");
                if (!string.Equals(report.Recipe, native.Name, StringComparison.OrdinalIgnoreCase) ||
                    report.Station != station || report.Exit != process.ExitCode)
                    throw new InvalidDataException("原生回傳站別／Recipe／退出碼不一致。");
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("原生定位流程未通過：" +
                        report.Summary + (string.IsNullOrWhiteSpace(report.Error)
                            ? "" : "　" + report.Error));
                if (report.Locate != "PASS")
                    throw new InvalidOperationException("定位未通過，禁止將圖片判為光源 OK。");
                return report;
            });
        }

        public static PreparedRecipe Prepare(RecipeManager.RecipeManifest recipe, string appBase)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (!Guid.TryParseExact(recipe.Id, "N", out _))
                throw new InvalidDataException("Recipe ID 無效。");
            if (string.IsNullOrWhiteSpace(recipe.VendorRecipePath))
                throw new InvalidOperationException("目前 Recipe 尚未匯入原廠參數。請先按「匯入原廠資料」。");

            string vendorName = Path.GetFileName(recipe.VendorRecipePath.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(vendorName) || vendorName == "." || vendorName == ".." ||
                vendorName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                vendorName.IndexOfAny(new[] {'/', '\\', ':'}) >= 0)
                throw new InvalidDataException("原廠 Recipe 名稱不安全，無法載入。");

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
            CompareExactFiles(source, destination, sourceFiles);
            CheckViaOriginalCli(recipeCli, nativeRoot, vendorName, destination);
            return new PreparedRecipe(vendorName, nativeRoot, destination, sourceFiles.Count);
        }

        // R3 portable-review may allow editing the isolated Recipe copy.
        // Never label this launcher read-only; the production source is protected
        // by staging a separate, SHA-verified local copy.
        public static void OpenSandbox(PreparedRecipe prepared, int station, bool openLocate = false,
            IEnumerable<string> imagePaths = null, string currentImage = null, string tool = null)
        {
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
            if (Process.Start(start) == null)
                throw new IOException("原生 InspTools R3 無法啟動。");
        }

        private static List<string> EnumerateSafeSnapshot(string source)
        {
            if (!Directory.Exists(source) || IsLink(source))
                throw new DirectoryNotFoundException("Luma 的原廠 Recipe 副本不存在或包含連結。");
            if (!File.Exists(Path.Combine(source, "Setting.ini")) ||
                !Enumerable.Range(1, 8).All(i => File.Exists(Path.Combine(source, "station" + i + ".ini"))))
                throw new InvalidDataException("原廠 Recipe 副本不完整：需 Setting.ini 與 station1～8.ini。");
            var relative = new List<string>();
            Visit(source, source, relative);
            return relative.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void Visit(string root, string dir, List<string> files)
        {
            if (IsLink(dir)) throw new InvalidDataException("原廠 Recipe 內有資料夾連結：" + dir);
            foreach (string file in Directory.EnumerateFiles(dir))
            {
                if (IsLink(file)) throw new InvalidDataException("原廠 Recipe 內有檔案連結：" + file);
                files.Add(file.Substring(root.Length + 1));
            }
            foreach (string child in Directory.EnumerateDirectories(dir)) Visit(root, child, files);
        }

        private static bool IsLink(string path) =>
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

        private static void CompareExactFiles(string source, string destination, IReadOnlyList<string> files)
        {
            if (!Directory.Exists(destination) || IsLink(destination))
                throw new InvalidDataException("原生 Recipe 不是正常資料夾。");
            var found = new List<string>();
            Visit(destination, destination, found);
            if (found.Count != files.Count ||
                !found.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                     .SequenceEqual(files, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("原生 Recipe 已存在但檔案清單不同，禁止覆蓋。");
            using var sha = SHA256.Create();
            foreach (string file in files)
            {
                using var a = File.OpenRead(Path.Combine(source, file));
                byte[] first = sha.ComputeHash(a);
                using var b = File.OpenRead(Path.Combine(destination, file));
                byte[] second = sha.ComputeHash(b);
                if (!first.SequenceEqual(second))
                    throw new InvalidDataException("原生 Recipe 與 Luma 快照不一致：" + file + "，禁止覆蓋。");
            }
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
                throw new InvalidDataException("原生 InspTools R3 無法接受此 Recipe：" +
                    (string.IsNullOrWhiteSpace(error) ? output : error));
        }
    }
}
