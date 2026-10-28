using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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
        // 外部原廠 TrainingImage 的唯讀來源；圖片不複製進本地 Recipe。
        public string? TrainingImageRoot { get; set; }
        public string? TrainingProductCode { get; set; }
        public string? VendorRecipePath { get; set; }
        public List<RecipeStation> Stations { get; set; } = CreateStations();

        private static List<RecipeStation> CreateStations() =>
            Enumerable.Range(1, MaximumStations).Select(number => new RecipeStation { Number = number }).ToList();
    }

    public sealed class RecipeStation
    {
        public int Number { get; set; }
        public bool IsConfigured { get; set; }
        public string? SourceProfileName { get; set; }
        public string? OkDatabasePath { get; set; }
        public string? NgDatabasePath { get; set; }
        // 僅供原廠 AI 模型來源追溯，不表示 LumasharpChecker 可直接使用這些格式。
        public List<string> VendorAiModelFiles { get; set; } = new();
    }

    public sealed class AppPreferences
    {
        public int StationCount { get; set; } = 8;
        public string? LastRecipeId { get; set; }
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
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Recipe 名稱不可空白。", nameof(name));
        if (stationCount is not (8 or 10)) throw new ArgumentOutOfRangeException(nameof(stationCount));
        if (LoadAllRecipes().Any(recipe => string.Equals(recipe.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"已經有同名 Recipe：{name}");

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
            throw new InvalidOperationException("同名 TrainingImage Recipe 已使用其他來源，請先核對。");

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
            throw new InvalidOperationException("Recipe 路徑不在 Recipe 資料夾內。");
        if (Directory.Exists(recipeDirectory)) Directory.Delete(recipeDirectory, true);
    }

    public string GetProfileBasePath(RecipeManifest recipe) => GetRecipeDirectory(recipe);

    public static string GetStationDirectoryName(int stationNumber)
    {
        ValidateStationNumber(stationNumber);
        return $"station{stationNumber:00}";
    }

    private string GetRecipeDirectory(RecipeManifest recipe)
    {
        if (!Guid.TryParseExact(recipe.Id, "N", out _))
            throw new InvalidOperationException("Recipe ID 格式無效。");
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
