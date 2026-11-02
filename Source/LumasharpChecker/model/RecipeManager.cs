using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using static ProfileManager;

namespace LumasharpChecker.model;

public sealed class RecipeManager
{
    private const int MaximumStations = 10;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string BasePath { get; }
    public string PreferencesPath { get; }
    private readonly string _profileLibraryPath;

    // 相對快取路徑以 App 目錄為根，確保整包移到公司電腦後仍可用。
    public string ResolveDatabasePath(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath)) return string.Empty;
        return Path.GetFullPath(Path.IsPathRooted(storedPath)
            ? storedPath
            : Path.Combine(Directory.GetParent(BasePath)!.FullName, storedPath));
    }

    public RecipeManager(string workingDirectory, string profileLibraryPath)
    {
        BasePath = Path.Combine(Path.GetFullPath(workingDirectory), "recipes");
        PreferencesPath = Path.Combine(Path.GetFullPath(workingDirectory), "app-settings.json");
        _profileLibraryPath = Path.GetFullPath(profileLibraryPath);
        Directory.CreateDirectory(BasePath);
    }

    public sealed class RecipeManifest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public int StationCount { get; set; } = 8;
        public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        public string UpdatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        public string? DatabaseDayFolder { get; set; }
        // Per-Recipe destination for inspection result images.
        public string? InspectionImageOutputFolder { get; set; }
        // 外部原廠 TrainingImage 的唯讀來源；圖片不複製進本地 Recipe。
        public string? TrainingImageRoot { get; set; }
        public string? TrainingProductCode { get; set; }
        public string? VendorRecipePath { get; set; }
        public string? LocalNativeRecipeName { get; set; }
        public List<RecipeStation> Stations { get; set; } = CreateStations();

        private static List<RecipeStation> CreateStations() =>
            Enumerable.Range(1, MaximumStations).Select(number => new RecipeStation { Number = number }).ToList();
    }

    public sealed class RecipeStation
    {
        public const int MaximumRecentImageCount = 500;
        public int Number { get; set; }
        public bool IsConfigured { get; set; }
        public int RecentImageCount { get; set; } = 1;
        public string? SourceProfileName { get; set; }
        // Local workflow choices belong to this Recipe/station, not the shared UI defaults.
        public string? FlowSelection { get; set; }
        public bool LightInspectionEnabled { get; set; } = true;
        public string? OkDatabasePath { get; set; }
        public string? NgDatabasePath { get; set; }
        public string? AllDatabasePath { get; set; }
        // 僅供原廠 AI 模型來源追溯，不表示 LumasharpChecker 可直接使用這些格式。
        public List<string> VendorAiModelFiles { get; set; } = new();
    }

    public sealed class AppPreferences
    {
        public int StationCount { get; set; } = 8;
        public string? LastRecipeId { get; set; }
        // Shared picker start folder for the work-image report library.
        public string? DatabaseDayFolder { get; set; }
        // Keep the original JSON name so settings from the previous build remain compatible.
        [JsonPropertyName("autoSelectLatestSharedReportDay")]
        public bool AutoUpdateLatestSharedReportDay { get; set; } = true;
    }

    public sealed class VendorRecipeFolder
    {
        public string Name { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public int StationCount { get; set; }
    }

    public sealed class VendorSnapshotDiff
    {
        public List<string> AddedFolders { get; } = new();
        public List<string> RemovedFolders { get; } = new();
        public List<string> Added { get; } = new();
        public List<string> Modified { get; } = new();
        public List<string> Removed { get; } = new();
        public bool HasChanges => AddedFolders.Count > 0 || RemovedFolders.Count > 0
            || Added.Count > 0 || Modified.Count > 0 || Removed.Count > 0;
    }

    public AppPreferences LoadPreferences()
    {
        if (!File.Exists(PreferencesPath)) return new AppPreferences();
        try
        {
            var preferences = JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(PreferencesPath), JsonOptions);
            return preferences is { StationCount: 8 or 10 } ? preferences : new AppPreferences();
        }
        catch
        {
            return new AppPreferences();
        }
    }

    public void SavePreferences(AppPreferences preferences)
    {
        if (preferences.StationCount is not (8 or 10))
            throw new ArgumentOutOfRangeException(nameof(preferences), "站數只能是 8 或 10。");
        WriteJsonAtomically(PreferencesPath, preferences);
    }

    public string LoadFlowSelection(RecipeManifest recipe, int stationNumber)
    {
        ValidateStationNumber(stationNumber);
        string? saved = recipe.Stations.FirstOrDefault(s => s.Number == stationNumber)?.FlowSelection;
        if (saved != null && saved.Length == 3 && saved.All(c => c == '0' || c == '1')) return saved;
        return ReadNativeFlowSelection(recipe, stationNumber);
    }

    public string ReadNativeFlowSelection(RecipeManifest recipe, int stationNumber)
    {
        ValidateStationNumber(stationNumber);
        string file = Path.Combine(GetRecipeDirectory(recipe), "vendor-source", "station" + stationNumber + ".ini");
        var enabled = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        bool main = false;
        if (File.Exists(file)) foreach (string line in File.ReadAllLines(file))
        {
            string text = line.Trim();
            if (text.StartsWith("[")) { main = text.Equals("[Main]", StringComparison.OrdinalIgnoreCase); continue; }
            if (!main) continue;
            int equals = text.IndexOf('='); if (equals < 0) continue;
            string key = text.Substring(0, equals).Trim(), value = text.Substring(equals + 1).Split(';', '#')[0].Trim();
            enabled[key] = value.Equals("True", StringComparison.OrdinalIgnoreCase) || value == "1";
        }
        return string.Concat(new[] { "LocateToolON", "SelectWayON", "SelectFaceON" }.Select(k => enabled.TryGetValue(k, out bool on) && on ? "1" : "0"));
    }

    public void SaveFlowSelection(RecipeManifest recipe, int stationNumber, string selection)
    {
        ValidateStationNumber(stationNumber);
        if (selection == null || selection.Length != 3 || selection.Any(c => c != '0' && c != '1'))
            throw new ArgumentException("無效的工具啟用設定。", nameof(selection));
        var station = recipe.Stations.FirstOrDefault(s => s.Number == stationNumber);
        if (station == null) { station = new RecipeStation { Number = stationNumber }; recipe.Stations.Add(station); }
        station.FlowSelection = selection;
        SaveManifest(recipe);
    }

    public bool LoadLightInspectionEnabled(RecipeManifest recipe, int stationNumber)
    {
        ValidateStationNumber(stationNumber);
        return recipe.Stations.FirstOrDefault(station => station.Number == stationNumber)?.LightInspectionEnabled ?? true;
    }

    public void SaveLightInspectionEnabled(RecipeManifest recipe, int stationNumber, bool enabled)
    {
        ValidateStationNumber(stationNumber);
        var station = recipe.Stations.FirstOrDefault(item => item.Number == stationNumber);
        if (station == null) { station = new RecipeStation { Number = stationNumber }; recipe.Stations.Add(station); }
        station.LightInspectionEnabled = enabled;
        SaveManifest(recipe);
    }

    public List<RecipeManifest> LoadAllRecipes()
    {
        var recipes = new List<RecipeManifest>();
        foreach (var directory in Directory.EnumerateDirectories(BasePath))
        {
            string manifestPath = Path.Combine(directory, "recipe.json");
            if (!File.Exists(manifestPath)) continue;
            try
            {
                var recipe = JsonSerializer.Deserialize<RecipeManifest>(File.ReadAllText(manifestPath), JsonOptions);
                if (recipe == null
                    || string.IsNullOrWhiteSpace(recipe.Name)
                    || !Guid.TryParseExact(recipe.Id, "N", out _)
                    || !string.Equals(Path.GetFileName(directory), recipe.Id, StringComparison.OrdinalIgnoreCase))
                    continue;
                NormalizeStations(recipe);
                recipes.Add(recipe);
            }
            catch
            {
                // Ignore an invalid recipe file without affecting other recipes or the legacy profile library.
            }
        }
        return recipes.OrderBy(recipe => recipe.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public RecipeManifest CreateRecipe(string name, int stationCount)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("配方名稱不可空白。", nameof(name));
        if (stationCount is not (8 or 10)) throw new ArgumentOutOfRangeException(nameof(stationCount));
        if (LoadAllRecipes().Any(recipe => string.Equals(recipe.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"已經有同名配方：{name}");

        var recipe = new RecipeManifest { Name = name, StationCount = stationCount };
        Directory.CreateDirectory(GetRecipeDirectory(recipe));
        for (int station = 1; station <= MaximumStations; station++)
            Directory.CreateDirectory(GetStationDirectory(recipe, station));
        SaveManifest(recipe);
        return recipe;
    }

    public void SaveManifest(RecipeManifest recipe)
    {
        if (recipe.StationCount is not (8 or 10)) throw new ArgumentOutOfRangeException(nameof(recipe));
        NormalizeStations(recipe);
        recipe.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        Directory.CreateDirectory(GetRecipeDirectory(recipe));
        for (int station = 1; station <= MaximumStations; station++)
            Directory.CreateDirectory(GetStationDirectory(recipe, station));
        WriteJsonAtomically(Path.Combine(GetRecipeDirectory(recipe), "recipe.json"), recipe);
    }

    public List<VendorRecipeFolder> DiscoverVendorRecipeFolders(string selectedRoot)
    {
        string root = Path.GetFullPath(selectedRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("找不到原廠資料夾。");
        var rootInfo = new DirectoryInfo(root);
        var folders = new List<DirectoryInfo>();
        if (IsVendorRecipeFolder(root)) folders.Add(rootInfo);
        else folders.AddRange(rootInfo.EnumerateDirectories().Where(directory => IsVendorRecipeFolder(directory.FullName)));

        return folders.OrderBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(folder => new VendorRecipeFolder
            {
                Name = folder.Name,
                SourcePath = folder.FullName,
                StationCount = Enumerable.Range(1, 10).Count(number => File.Exists(Path.Combine(folder.FullName, $"station{number}.ini")))
            }).ToList();
    }

    public VendorSnapshotDiff CompareVendorSnapshot(string sourcePath, RecipeManifest? existingRecipe)
    {
        ValidateVendorRecipeFolder(sourcePath);
        string source = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string baseFull = Path.GetFullPath(BasePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string sourcePrefix = source + Path.DirectorySeparatorChar;
        if (sourcePrefix.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase) ||
            baseFull.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("原廠來源與本機配方目錄不可互相包含。");

        var sourceFiles = GetVendorFileHashes(source);
        string snapshotPath = existingRecipe == null ? string.Empty : Path.Combine(GetRecipeDirectory(existingRecipe), "vendor-source");
        var snapshotFiles = Directory.Exists(snapshotPath) ? GetVendorFileHashes(snapshotPath) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sourceFolders = GetVendorFolders(source);
        var snapshotFolders = Directory.Exists(snapshotPath) ? GetVendorFolders(snapshotPath) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var diff = new VendorSnapshotDiff();
        diff.AddedFolders.AddRange(sourceFolders.Where(path => !snapshotFolders.Contains(path)));
        diff.RemovedFolders.AddRange(snapshotFolders.Where(path => !sourceFolders.Contains(path)));
        foreach (var pair in sourceFiles)
        {
            if (!snapshotFiles.TryGetValue(pair.Key, out string? existingHash)) diff.Added.Add(pair.Key);
            else if (!string.Equals(pair.Value, existingHash, StringComparison.OrdinalIgnoreCase)) diff.Modified.Add(pair.Key);
        }
        foreach (string path in snapshotFiles.Keys)
            if (!sourceFiles.ContainsKey(path)) diff.Removed.Add(path);
        diff.AddedFolders.Sort(StringComparer.CurrentCultureIgnoreCase);
        diff.RemovedFolders.Sort(StringComparer.CurrentCultureIgnoreCase);
        diff.Added.Sort(StringComparer.CurrentCultureIgnoreCase);
        diff.Modified.Sort(StringComparer.CurrentCultureIgnoreCase);
        diff.Removed.Sort(StringComparer.CurrentCultureIgnoreCase);
        return diff;
    }

    public RecipeManifest ImportOrUpdateVendorSnapshot(string sourcePath, RecipeManifest? existingRecipe)
    {
        string name = Path.GetFileName(Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (existingRecipe == null) return ImportVendorSnapshot(sourcePath, name);
        UpdateVendorSnapshot(existingRecipe, sourcePath);
        return existingRecipe;
    }

    public void UpdateVendorSnapshot(RecipeManifest recipe, string selectedPath)
    {
        if (recipe == null) throw new ArgumentNullException(nameof(recipe));
        ValidateVendorRecipeFolder(selectedPath);
        string recipeDir = GetRecipeDirectory(recipe);
        if (!File.Exists(Path.Combine(recipeDir, "recipe.json")))
            throw new InvalidOperationException("本機配方不存在，無法更新。");

        string source = Path.GetFullPath(selectedPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string baseFull = Path.GetFullPath(BasePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string sourcePrefix = source + Path.DirectorySeparatorChar;
        if (sourcePrefix.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase) ||
            baseFull.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("原廠來源與本機配方目錄不可互相包含。");

        string nonce = Guid.NewGuid().ToString("N");
        string stage = Path.Combine(recipeDir, ".vendor-stage-" + nonce);
        string target = Path.Combine(recipeDir, "vendor-source");
        string backup = Path.Combine(recipeDir, ".vendor-backup-" + nonce);
        string? previousPath = recipe.VendorRecipePath;
        string previousUpdatedAt = recipe.UpdatedAt;
        bool movedOld = false, movedNew = false;
        try
        {
            CopyVendorDirectory(source, stage);
            if (Directory.Exists(target)) { Directory.Move(target, backup); movedOld = true; }
            Directory.Move(stage, target); movedNew = true;
            recipe.VendorRecipePath = source;
            SaveManifest(recipe);
            // Preserve the previous snapshot so a completed update remains recoverable.
        }
        catch
        {
            recipe.VendorRecipePath = previousPath;
            recipe.UpdatedAt = previousUpdatedAt;
            if (movedNew && Directory.Exists(target)) Directory.Move(target, stage);
            if (movedOld && Directory.Exists(backup) && !Directory.Exists(target)) Directory.Move(backup, target);
            if (Directory.Exists(stage))
            {
                try { Directory.Delete(stage, true); }
                catch { /* A leftover hidden staging folder does not affect the active recipe. */ }
            }
            throw;
        }
    }

    private static bool IsVendorRecipeFolder(string path) =>
        Directory.Exists(path) && File.Exists(Path.Combine(path, "Setting.ini")) &&
        Enumerable.Range(1, 8).Any(number => File.Exists(Path.Combine(path, $"station{number}.ini")));

    private static void ValidateVendorRecipeFolder(string path)
    {
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("找不到原廠配方資料夾。");
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("原廠配方不能是連結資料夾。");
        if (!IsVendorRecipeFolder(path))
            throw new InvalidDataException("資料夾須包含 Setting.ini 與至少一個 station1.ini～station8.ini。");
    }

    private static Dictionary<string, string> GetVendorFileHashes(string root)
    {
        string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddVendorFileHashes(rootFull, rootFull, files);
        return files;
    }

    private static HashSet<string> GetVendorFolders(string root)
    {
        string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddVendorFolders(rootFull, rootFull, folders);
        return folders;
    }

    private static void AddVendorFolders(string root, string directory, HashSet<string> folders)
    {
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("原廠配方含連結資料夾，無法比對：" + directory);
        foreach (string child in Directory.EnumerateDirectories(directory))
        {
            if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("原廠配方含連結資料夾，無法比對：" + child);
            string relative = child.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            folders.Add(relative);
            AddVendorFolders(root, child, folders);
        }
    }

    private static void AddVendorFileHashes(string root, string directory, Dictionary<string, string> files)
    {
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("原廠配方含連結資料夾，無法比對：" + directory);
        foreach (string file in Directory.EnumerateFiles(directory))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("原廠配方含連結檔案，無法比對：" + file);
            string relative = file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(file);
            files[relative] = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }
        foreach (string child in Directory.EnumerateDirectories(directory))
            AddVendorFileHashes(root, child, files);
    }

    /// <summary>Creates an unconfigured Luma recipe containing a verified vendor source snapshot.
    /// This preserves vendor data; it does not translate inspection parameters.</summary>
    public RecipeManifest ImportVendorSnapshot(string selectedPath, string name)
    {
        string source = Path.GetFullPath(selectedPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException("找不到原廠配方資料夾。");
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("原廠配方不能是連結資料夾。");
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("配方名稱不可空白。");
        if (LoadAllRecipes().Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("已有同名配方，請使用不同名稱。");
        var stations = Enumerable.Range(1, 8).Where(n => File.Exists(Path.Combine(source, $"station{n}.ini"))).ToArray();
        if (stations.Length == 0 || !File.Exists(Path.Combine(source, "Setting.ini")))
            throw new InvalidDataException("請選擇含 Setting.ini 與 station1.ini 等站別檔案的原廠配方資料夾。");
        string baseFull = Path.GetFullPath(BasePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string sourcePrefix = source + Path.DirectorySeparatorChar;
        if (sourcePrefix.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase) ||
            baseFull.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("原廠來源與本機配方目錄不可互相包含。");

        var recipe = new RecipeManifest { Name = name, StationCount = 8, VendorRecipePath = source };
        string staging = Path.Combine(BasePath, ".vendor-import-" + recipe.Id);
        string finalPath = GetRecipeDirectory(recipe);
        if (Directory.Exists(staging) || Directory.Exists(finalPath)) throw new IOException("匯入目的地已存在。");
        try
        {
            Directory.CreateDirectory(staging);
            CopyVendorDirectory(source, Path.Combine(staging, "vendor-source"));
            Directory.Move(staging, finalPath);
            SaveManifest(recipe);
            return recipe;
        }
        catch
        {
            // A failed import must not appear in the recipe list. Keep any partially
            // staged data for diagnosis; never remove an existing recipe or source.
            throw;
        }
    }

    /// <summary>Attach a verified vendor snapshot to an existing Luma recipe.
    /// Existing Luma profiles and the previous snapshot are retained.</summary>
    public void AttachVendorSnapshot(RecipeManifest recipe, string selectedPath)
    {
        if (recipe == null) throw new ArgumentNullException(nameof(recipe));
        string recipeDir = GetRecipeDirectory(recipe);
        if (!File.Exists(Path.Combine(recipeDir, "recipe.json")))
            throw new InvalidOperationException("目前配方不存在，無法匯入。");
        string source = Path.GetFullPath(selectedPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException("找不到原廠配方資料夾。");
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("原廠配方不能是連結資料夾。");
        if (!File.Exists(Path.Combine(source, "Setting.ini")) ||
            !Enumerable.Range(1, 8).Any(n => File.Exists(Path.Combine(source, $"station{n}.ini"))))
            throw new InvalidDataException("請選擇含 Setting.ini 與 station1.ini 等站別檔案的原廠配方資料夾。");
        string baseFull = Path.GetFullPath(BasePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string sourcePrefix = source + Path.DirectorySeparatorChar;
        if (sourcePrefix.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase) || baseFull.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("原廠來源與本機配方目錄不可互相包含。");
        string nonce = Guid.NewGuid().ToString("N");
        string stage = Path.Combine(recipeDir, ".vendor-stage-" + nonce);
        string target = Path.Combine(recipeDir, "vendor-source");
        string backup = Path.Combine(recipeDir, "vendor-backup-" + nonce);
        string? previousPath = recipe.VendorRecipePath;
        bool movedOld = false, movedNew = false;
        CopyVendorDirectory(source, stage);
        try
        {
            if (Directory.Exists(target)) { Directory.Move(target, backup); movedOld = true; }
            Directory.Move(stage, target); movedNew = true;
            recipe.VendorRecipePath = source;
            SaveManifest(recipe);
        }
        catch
        {
            recipe.VendorRecipePath = previousPath;
            if (movedNew && Directory.Exists(target)) Directory.Move(target, stage);
            if (movedOld && Directory.Exists(backup) && !Directory.Exists(target)) Directory.Move(backup, target);
            throw;
        }
    }

    private static void CopyVendorDirectory(string source, string destination)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("原廠 Recipe 含連結，無法安全複製：" + source);
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("原廠 Recipe 含連結檔案：" + file);
            string target = Path.Combine(destination, Path.GetFileName(file));
            File.Copy(file, target, false);
            using var sha = SHA256.Create();
            using var a = File.OpenRead(file);
            using var b = File.OpenRead(target);
            if (!sha.ComputeHash(a).SequenceEqual(sha.ComputeHash(b)))
                throw new IOException("匯入檔案校驗失敗：" + file);
        }
        foreach (string dir in Directory.EnumerateDirectories(source))
            CopyVendorDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    /// <summary>
    /// 針對原廠 TrainingImage\R002-CCDn\Origional\n\OK|NG 轉成圖庫路徑。
    /// 此步驟不推導定位模型、檢測 ROI 或亮度閾值，也不變更原廠圖片。
    /// </summary>
    public RecipeManifest ImportTrainingImageRecipe(string selectedPath, bool cacheSamples = true)
    {
        if (!Directory.Exists(selectedPath))
            throw new DirectoryNotFoundException("TrainingImage 來源資料夾不存在：" + selectedPath);

        string? trainingRoot = null;
        string? product = null;
        var folder = new DirectoryInfo(Path.GetFullPath(selectedPath));
        for (var current = folder; current != null; current = current.Parent)
        {
            var match = Regex.Match(current.Name, @"^(.+)-CCD([1-9]|10)$", RegexOptions.IgnoreCase);
            if (match.Success) product = match.Groups[1].Value;
            if (current.Name.Equals("TrainingImage", StringComparison.OrdinalIgnoreCase))
            {
                trainingRoot = current.FullName;
                break;
            }
        }
        if (trainingRoot == null)
            throw new ArgumentException("請選取 TrainingImage、產品 CCD 資料夾或 Origional 下的站別資料夾。");
        if (string.IsNullOrWhiteSpace(product))
        {
            var candidates = Directory.GetDirectories(trainingRoot)
                .Select(path => Regex.Match(Path.GetFileName(path), @"^(.+)-CCD1$", RegexOptions.IgnoreCase))
                .Where(match => match.Success).Select(match => match.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (candidates.Length != 1)
                throw new ArgumentException("TrainingImage 中有多個產品；請選擇 R002-CCD1 這類產品站別資料夾。");
            product = candidates[0];
        }

        // 必須真的有影像才建立 Recipe，避免空站被當作有效樣本。
        var paths = new Dictionary<int, Tuple<string?, string?>>();
        for (int number = 1; number <= 8; number++)
        {
            var stationRoot = Path.Combine(trainingRoot, product + "-CCD" + number,
                "Origional", number.ToString());
            if (!Directory.Exists(stationRoot))
                stationRoot = Path.Combine(trainingRoot, product + "-CCD" + number,
                    "Original", number.ToString());
            string okPath = Path.Combine(stationRoot, "OK");
            string ngPath = Path.Combine(stationRoot, "NG");
            paths[number] = Tuple.Create(
                HasImageFiles(okPath) ? okPath : null,
                HasImageFiles(ngPath) ? ngPath : null);
        }
        if (!paths.Values.Any(item => item.Item1 != null || item.Item2 != null))
            throw new InvalidOperationException("找不到 CCD1～8 的 Origional\\站號\\OK／NG 影像。");

        string name = product + "（TrainingImage）";
        var recipe = LoadAllRecipes().FirstOrDefault(item =>
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (recipe == null)
            recipe = CreateRecipe(name, 8);
        else if (!string.IsNullOrWhiteSpace(recipe.TrainingImageRoot)
                 && !string.Equals(recipe.TrainingImageRoot, trainingRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("同名訓練圖庫配方已使用其他來源，請先核對。");

        recipe.StationCount = 8;
        recipe.TrainingImageRoot = trainingRoot;
        recipe.TrainingProductCode = product;
        recipe.DatabaseDayFolder = null;
        var vendorRecipe = Path.Combine(Directory.GetParent(trainingRoot)!.FullName, "Recipe", "3923-" + product);
        recipe.VendorRecipePath = Directory.Exists(vendorRecipe) ? vendorRecipe : null;
        string vendorModelRoot = Path.Combine(Directory.GetParent(trainingRoot)!.FullName, "Model");
        foreach (var station in recipe.Stations.Where(item => item.Number <= 8))
        {
            station.OkDatabasePath = cacheSamples
                ? CacheTrainingImages(paths[station.Number].Item1, product, station.Number, "OK")
                : paths[station.Number].Item1;
            station.NgDatabasePath = cacheSamples
                ? CacheTrainingImages(paths[station.Number].Item2, product, station.Number, "NG")
                : paths[station.Number].Item2;
            string modelStem = product + "-CCD" + station.Number;
            station.VendorAiModelFiles = new[] { ".h5d", ".pbd" }
                .Select(ext => Path.Combine(vendorModelRoot, modelStem + ext))
                .Where(File.Exists).ToList();
            // 不能因圖庫匯入而把沒有定位模型的站標記成檢測設定完成。
        }
        SaveManifest(recipe);
        return recipe;
    }

    private string? CacheTrainingImages(string? source, string product, int station, string category)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        // 每種分類最多取 6 張，做成可攜離線測試集；不碰原廠母本，也不重複複製完整訓練庫。
        var images = Directory.EnumerateFiles(source).Where(IsImageFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).Take(6).ToArray();
        if (images.Length == 0) return null;
        var appDir = Directory.GetParent(BasePath)!.FullName;
        string target = Path.Combine(appDir, "TrainingImageCache", product, station.ToString(), category);
        Directory.CreateDirectory(target);
        foreach (var image in images)
        {
            string output = Path.Combine(target, Path.GetFileName(image));
            if (!File.Exists(output)) File.Copy(image, output);
        }
        return Path.Combine("TrainingImageCache", product, station.ToString(), category);
    }

    private static bool IsImageFile(string path)
    {
        string ext = Path.GetExtension(path);
        return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasImageFiles(string directory)
    {
        if (!Directory.Exists(directory)) return false;
        return Directory.EnumerateFiles(directory).Any(IsImageFile);
    }

    public ProfileData? LoadStation(RecipeManifest recipe, int stationNumber)
    {
        ValidateStationNumber(stationNumber);
        var station = recipe.Stations.Single(item => item.Number == stationNumber);
        string stationDirectory = GetStationDirectory(recipe, stationNumber);
        string profilePath = Path.Combine(stationDirectory, "profile.json");
        string patternPath = Path.Combine(stationDirectory, "pattern.jpg");
        if (!station.IsConfigured || !File.Exists(profilePath) || !File.Exists(patternPath)) return null;

        var profile = JsonSerializer.Deserialize<ProfileData>(File.ReadAllText(profilePath), JsonOptions);
        if (profile == null) return null;
        profile.Name = GetStationProfileName(recipe, stationNumber);
        profile.ImagePath = patternPath;
        profile.Bitmap = ProfileManager.LoadBitmapFromPath(patternPath);
        profile.ImageWidth = profile.Bitmap.PixelWidth;
        profile.ImageHeight = profile.Bitmap.PixelHeight;
        return profile;
    }

    public ProfileData AssignProfile(RecipeManifest recipe, int stationNumber, ProfileData sourceProfile)
    {
        ValidateStationNumber(stationNumber);
        string sourceImagePath = ResolveProfileImage(sourceProfile);
        string stationDirectory = GetStationDirectory(recipe, stationNumber);
        Directory.CreateDirectory(stationDirectory);
        string patternPath = Path.Combine(stationDirectory, "pattern.jpg");
        File.Copy(sourceImagePath, patternPath, true);

        var profileJson = JsonSerializer.Serialize(sourceProfile, JsonOptions);
        var profile = JsonSerializer.Deserialize<ProfileData>(profileJson, JsonOptions)
            ?? throw new InvalidOperationException("無法複製設定檔。");
        profile.Name = GetStationProfileName(recipe, stationNumber);
        profile.ImagePath = patternPath;
        profile.Bitmap = ProfileManager.LoadBitmapFromPath(patternPath);
        profile.ImageWidth = profile.Bitmap.PixelWidth;
        profile.ImageHeight = profile.Bitmap.PixelHeight;

        recipe.Stations.Single(item => item.Number == stationNumber).SourceProfileName = sourceProfile.Name;
        SaveStation(recipe, stationNumber, profile);
        return profile;
    }

    public void SaveStation(RecipeManifest recipe, int stationNumber, ProfileData profile)
    {
        ValidateStationNumber(stationNumber);
        string stationDirectory = GetStationDirectory(recipe, stationNumber);
        Directory.CreateDirectory(stationDirectory);
        string patternPath = Path.Combine(stationDirectory, "pattern.jpg");
        if (!File.Exists(patternPath))
        {
            string sourceImagePath = ResolveProfileImage(profile);
            File.Copy(sourceImagePath, patternPath, true);
        }

        string storedName = GetStationProfileName(recipe, stationNumber);
        var storedProfile = JsonSerializer.Deserialize<ProfileData>(JsonSerializer.Serialize(profile, JsonOptions), JsonOptions)
            ?? throw new InvalidOperationException("無法保存站別設定。");
        storedProfile.Name = storedName;
        storedProfile.ImagePath = "pattern.jpg";
        WriteJsonAtomically(Path.Combine(stationDirectory, "profile.json"), storedProfile);
        profile.Name = storedName;
        profile.ImagePath = patternPath;
        recipe.Stations.Single(item => item.Number == stationNumber).IsConfigured = true;
        SaveManifest(recipe);
    }

    public void DeleteRecipe(RecipeManifest recipe)
    {
        string baseDirectory = Path.GetFullPath(BasePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string recipeDirectory = Path.GetFullPath(GetRecipeDirectory(recipe));
        if (!recipeDirectory.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("配方路徑不在配方資料夾內。");
        if (Directory.Exists(recipeDirectory)) Directory.Delete(recipeDirectory, true);
    }

    public string GetProfileBasePath(RecipeManifest recipe) => GetRecipeDirectory(recipe);

    // ── 共用報告格式掃描 ──

    public sealed class SharedReportStationInfo
    {
        public int Number { get; init; }
        public string StationFolder { get; init; } = string.Empty;
        public bool HasImages { get; init; }
        public bool HasOkSubfolder { get; init; }
        public bool HasNgSubfolder { get; init; }
    }

    public sealed class SharedReportDayInfo
    {
        public string DayFolder { get; init; } = string.Empty;
        public List<SharedReportStationInfo> Stations { get; init; } = new();
    }

    /// <summary>掃描 月\日\n站 格式的共用報告資料夾，支援選取共用根目錄、月份或日期。</summary>
    public List<(string MonthFolder, List<SharedReportDayInfo> Days)> DiscoverSharedReportDays(string selectedMonthFolder)
    {
        string root = Path.GetFullPath(selectedMonthFolder);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("找不到月資料夾：" + root);

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root };
        foreach (string monthOrDay in Directory.GetDirectories(root))
        {
            candidates.Add(monthOrDay);
            foreach (string day in Directory.GetDirectories(monthOrDay)) candidates.Add(day);
        }

        var results = new Dictionary<string, List<SharedReportDayInfo>>(StringComparer.OrdinalIgnoreCase);
        var stationName = new Regex(@"^(\d{1,2})(站)?(_\d+)?$", RegexOptions.IgnoreCase);
        foreach (string dayFolder in candidates.OrderBy(path => path.Length))
        {
            var stations = new Dictionary<int, SharedReportStationInfo>();
            foreach (string sub in Directory.GetDirectories(dayFolder))
            {
                var match = stationName.Match(Path.GetFileName(sub));
                if (!match.Success) continue;
                if (int.TryParse(match.Groups[1].Value, out int num) && num is >= 1 and <= MaximumStations)
                {
                    bool hasImages = Directory.EnumerateFiles(sub, "*", SearchOption.AllDirectories)
                        .Any(IsImageFile);
                    if (!hasImages) continue; // 沒圖片的站跳過

                    string okFolder = Path.Combine(sub, "OK"), ngFolder = Path.Combine(sub, "NG");
                    bool hasOk = Directory.Exists(okFolder)
                        && Directory.EnumerateFiles(okFolder, "*", SearchOption.AllDirectories).Any(IsImageFile);
                    bool hasNg = Directory.Exists(ngFolder)
                        && Directory.EnumerateFiles(ngFolder, "*", SearchOption.AllDirectories).Any(IsImageFile);

                    if (stations.TryGetValue(num, out var existing))
                    {
                        // 同一站多個版本：取有圖片且字典序最小的
                        if (!existing.HasImages || string.Compare(sub, existing.StationFolder, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            stations[num] = new SharedReportStationInfo
                            {
                                Number = num, StationFolder = sub,
                                HasImages = hasImages, HasOkSubfolder = hasOk, HasNgSubfolder = hasNg
                            };
                        }
                    }
                    else
                    {
                        stations[num] = new SharedReportStationInfo
                        {
                            Number = num, StationFolder = sub,
                            HasImages = hasImages, HasOkSubfolder = hasOk, HasNgSubfolder = hasNg
                        };
                    }
                }
            }

            if (stations.Count == 0) continue;

            // A month folder can also contain numeric day folders. Do not mistake those
            // for station folders when the real station folders are one level deeper.
            bool containsNestedStationLayout = stations.Values.Any(station =>
                Directory.GetDirectories(station.StationFolder).Any(child =>
                    stationName.IsMatch(Path.GetFileName(child)) &&
                    Directory.EnumerateFiles(child, "*", SearchOption.AllDirectories).Any(IsImageFile)));
            if (containsNestedStationLayout) continue;

            var dayInfo = new SharedReportDayInfo
            {
                DayFolder = dayFolder,
                Stations = stations.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList()
            };
            string monthFolder = Directory.GetParent(dayFolder)?.FullName ?? dayFolder;
            if (!results.TryGetValue(monthFolder, out var days)) results[monthFolder] = days = new();
            days.Add(dayInfo);
        }

        if (results.Count == 0)
            throw new InvalidDataException(
                "資料夾結構不符：找不到 <月>\\<日>\\<n站> 結構，或站資料夾內沒有圖片。");

        return results.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => (item.Key, item.Value.OrderBy(day => day.DayFolder, StringComparer.OrdinalIgnoreCase).ToList()))
            .ToList();
    }

    public static string GetStationDirectoryName(int stationNumber)
    {
        ValidateStationNumber(stationNumber);
        return $"station{stationNumber:00}";
    }

    private string GetRecipeDirectory(RecipeManifest recipe)
    {
        if (!Guid.TryParseExact(recipe.Id, "N", out _))
            throw new InvalidOperationException("配方識別碼格式無效。");
        return Path.Combine(BasePath, recipe.Id);
    }

    private string GetStationDirectory(RecipeManifest recipe, int stationNumber) =>
        Path.Combine(GetRecipeDirectory(recipe), $"station{stationNumber:00}");

    private static string GetStationProfileName(RecipeManifest recipe, int stationNumber) =>
        $"{recipe.Id}_{GetStationDirectoryName(stationNumber)}";

    private string ResolveProfileImage(ProfileData profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.ImagePath) && File.Exists(profile.ImagePath)) return profile.ImagePath;
        if (!string.IsNullOrWhiteSpace(profile.Name))
        {
            string libraryImage = Path.Combine(_profileLibraryPath, profile.Name, "pattern.jpg");
            if (File.Exists(libraryImage)) return libraryImage;
        }
        throw new FileNotFoundException("找不到設定檔的基準圖片，無法加入 Recipe。");
    }

    private static void NormalizeStations(RecipeManifest recipe)
    {
        recipe.Stations ??= new List<RecipeStation>();
        for (int number = 1; number <= MaximumStations; number++)
        {
            if (!recipe.Stations.Any(station => station.Number == number))
                recipe.Stations.Add(new RecipeStation { Number = number });
        }
        recipe.Stations = recipe.Stations.Where(station => station.Number >= 1 && station.Number <= MaximumStations)
            .GroupBy(station => station.Number)
            .Select(group => group.First())
            .OrderBy(station => station.Number)
            .ToList();
        foreach (RecipeStation station in recipe.Stations)
        {
            if (station.RecentImageCount < 1) station.RecentImageCount = 1;
            if (station.RecentImageCount > RecipeStation.MaximumRecentImageCount)
                station.RecentImageCount = RecipeStation.MaximumRecentImageCount;
        }
    }

    private static void ValidateStationNumber(int stationNumber)
    {
        if (stationNumber is < 1 or > MaximumStations)
            throw new ArgumentOutOfRangeException(nameof(stationNumber));
    }

    private static void WriteJsonAtomically<T>(string path, T value)
    {
        string temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value, JsonOptions));
        if (File.Exists(path))
            File.Replace(temporaryPath, path, null);
        else
            File.Move(temporaryPath, path);
    }
}
