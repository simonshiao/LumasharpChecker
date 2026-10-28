using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        File.Move(temporaryPath, path, true);
    }
}
