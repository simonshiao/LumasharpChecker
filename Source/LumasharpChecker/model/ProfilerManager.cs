using CommunityToolkit.Mvvm.ComponentModel;
using Cyntec.Logger;
using Emgu.CV.Reg;
using Emgu.CV.Stitching;
using LumasharpChecker;
using LumasharpChecker.model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;


public class ProfileManager
{
    // 基礎路徑屬性
    public string BasePath { get; }

    // 配置屬性（使用可空型別）
    public string? Name { get; private set; }
    public double? BrightnessMin { get; set; }
    public double? BrightnessMax { get; set; }
    public double? SharpnessMin { get; set; }
    public double? SharpnessMax { get; set; }
    public double? BrightnessBaseValue { get; set; }
    public double? SharpnessBaseValue { get; set; }
    public string DetectionMode { get; set; } = InspectionMode.Brightness;
    public int? RoiX { get; set; }
    public int? RoiY { get; set; }
    public int? RoiH { get; set; }
    public int? RoiW { get; set; }
    public int? RoiX2 { get; set; }
    public int? RoiY2 { get; set; }
    public int? RoiH2 { get; set; }
    public int? RoiW2 { get; set; }
    public int? RoiX3 { get; set; }
    public int? RoiY3 { get; set; }
    public int? RoiH3 { get; set; }
    public int? RoiW3 { get; set; }
    public string? ImagePath { get; set; }
    public bool TemplateMatching { get; set; } = false; // 預設值
    //public bool UseMaxMinRange { get; set; }
    public bool EnableDynamicThreshold { get; set; }
    public int DynamicThresholdCount { get; set; }
    //private bool _useMaxMinRange;
    //public bool UseMaxMinRange
    //{
    //    get => _useMaxMinRange;
    //    set
    //    {
    //        if (_useMaxMinRange != value)
    //        {
    //            _useMaxMinRange = value;
    //            OnPropertyChanged(nameof(UseMaxMinRange));
    //        }
    //    }
    //}
    //public event PropertyChangedEventHandler PropertyChanged;
    //protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    //{
    //    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    //}
    //private static ProfileManager _instance;
    //public static ProfileManager Instance => _instance ??= new ProfileManager();

    // 配置檔案路徑
    private string? _profileDir;
    private string? _filePath;

    public ProfileManager() { }
    /// <summary>
    /// 構造函式，初始化基礎路徑並創建目錄
    /// </summary>
    public ProfileManager(string basePath)
    {
        BasePath = basePath;
        Directory.CreateDirectory(basePath); // 自動處理目錄不存在時
    }

    /// <summary>
    /// 保存配置到 JSON 檔案
    /// </summary>
    //public void SaveProfile()
    //{
    //    if (string.IsNullOrEmpty(Name))
    //        throw new InvalidOperationException("Profile name must be set before saving");

    //    _profileDir = Path.Combine(BasePath, Name);
    //    Directory.CreateDirectory(_profileDir); // 確保配置目錄存在

    //    _filePath = Path.Combine(_profileDir, "profile.json");

    //    // 序列化配置數據
    //    var profileData = new ProfileData
    //    {
    //        Name = Name,
    //        BrightnessMin = BrightnessMin,
    //        BrightnessMax = BrightnessMax,
    //        SharpnessMin = SharpnessMin,
    //        SharpnessMax = SharpnessMax,
    //        RoiX1 = RoiX1,
    //        RoiY1 = RoiY1,
    //        RoiX2 = RoiX2,
    //        RoiY2 = RoiY2,
    //        ImagePath = ImagePath,
    //        TemplateMatching = TemplateMatching
    //    };

    //    var options = new JsonSerializerOptions
    //    {
    //        WriteIndented = true,
    //        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    //    };

    //    File.WriteAllText(_filePath, JsonSerializer.Serialize(profileData, options));
    //}

    /// <summary>
    /// 從 JSON 檔案加載配置
    /// </summary>
    public bool LoadProfile(string name = "")
    {
        Name = name;
        _profileDir = Path.Combine(BasePath, Name);
        _filePath = Path.Combine(_profileDir, "profile.json");

        try
        {
            // 檢查檔案是否存在
            if (!File.Exists(_filePath))
            {
                Logmanager.loger.Warn($"Profile檔案不存在: {_filePath}");
                return false;
            }

            var json = File.ReadAllText(_filePath);
            var profileData = JsonSerializer.Deserialize<ProfileData>(json);

            if (profileData != null)
            {
                Name = profileData.Name ?? Name;
                ApplyBrightnessThreshold(profileData);
                //SharpnessMin = profileData.SharpnessMin;
                var records = _dbManager.GetSharpnessRecords(this.Name);
                if (profileData.EnableDynamicThreshold)
                {
                    if (records.Count < profileData.DynamicThresholdCount)
                    {
                        SharpnessMin = CalculateSharpnessMin(profileData);
                    }
                    else
                    {
                        double mean = records.Average(r => r.Sharpness);
                        double std = Math.Sqrt(records.Average(r => Math.Pow(r.Sharpness - mean, 2)));
                        double calculatedMin = mean - 2 * std;
                        SharpnessMin = calculatedMin < 0 ? 0 : calculatedMin;
                    }
                }
                else
                {
                    SharpnessMin = CalculateSharpnessMin(profileData);
                }
                BrightnessBaseValue = profileData.BrightnessBaseValue ?? profileData.BrightnessMean ?? profileData.BrightnessMin;
                SharpnessBaseValue = profileData.SharpnessBaseValue ?? profileData.SharpnessMean ?? profileData.SharpnessMin;
                DetectionMode = string.IsNullOrWhiteSpace(profileData.DetectionMode) ? InspectionMode.Brightness : profileData.DetectionMode;


                //SharpnessMax = profileData.SharpnessMax;
                //RoiX1 = profileData.RoiX1;
                //RoiY1 = profileData.RoiY1;
                //RoiX2 = profileData.RoiX2;
                //RoiY2 = profileData.RoiY2;
                RoiX = profileData.PatternMatchRoiX;
                RoiY = profileData.PatternMatchRoiY;
                RoiW = profileData.PatternMatchRoiW;
                RoiH = profileData.PatternMatchRoiH;
                RoiX2 = profileData.EffectiveBrightnessRoiX;
                RoiY2 = profileData.EffectiveBrightnessRoiY;
                RoiW2 = profileData.EffectiveBrightnessRoiW;
                RoiH2 = profileData.EffectiveBrightnessRoiH;
                RoiX3 = profileData.EffectiveSharpnessRoiX;
                RoiY3 = profileData.EffectiveSharpnessRoiY;
                RoiW3 = profileData.EffectiveSharpnessRoiW;
                RoiH3 = profileData.EffectiveSharpnessRoiH;
                ImagePath = ResolveImagePath(profileData.ImagePath, _profileDir);
                TemplateMatching = profileData.TemplateMatching;
                EnableDynamicThreshold = profileData.EnableDynamicThreshold;
                DynamicThresholdCount = profileData.DynamicThresholdCount;
            }
        }
        catch (JsonException ex)
        {
            Logmanager.loger.Error($"Load Profile失敗 - JSON格式錯誤, _filePath: {_filePath}, ex: {ex}");
            return false;
        }
        catch (Exception ex)
        {
            Logmanager.loger.Error($"Load Profile失敗, _filePath: {_filePath}, ex: {ex} ");
            return false;
        }
        return true;
    }

    public List<ProfileData> allProfiles = new();
    public void SaveProfile(string newProfileName, Rectangle roi, Rectangle brightnessRoi, Rectangle sharpnessRoi, AnalysisResult ar, string patternImagePath, bool enableDynamicThreshold, int dynamicThresholdCount, string detectionMode = InspectionMode.Brightness)
    {


        var profileData = new ProfileData();
        //if (UseMaxMinRange)
        //{
        //    profileData.BrightnessMin = ar.Brightness?.Percentile?.Lower;
        //    profileData.BrightnessMax = ar.Brightness?.Percentile?.Upper;
        //}

        profileData.BrightnessMax = ar.Brightness.ThreeSigmaUpper;
        profileData.BrightnessMin = ar.Brightness.ThreeSigmaLower;
        profileData.BrightnessMean = ar.Brightness.Mean;
        profileData.BrightnessSigma = ar.Brightness.Sigma;
        profileData.BrightnessValues = ar.BrightnessValues;
        profileData.BrightnessPercentileLower = ar.Brightness?.Percentile?.Lower;
        profileData.BrightnessPercentileUpper = ar.Brightness?.Percentile?.Upper;
        profileData.BrightnessBaseValue = ar.Brightness.Mean;
        profileData.DetectionMode = string.IsNullOrWhiteSpace(detectionMode) ? InspectionMode.Brightness : detectionMode;

           
        profileData.Name = newProfileName;
        //profileData.BrightnessMin = ar.Brightness?.Percentile?.Lower;
        //profileData.BrightnessMax = ar.Brightness?.Percentile?.Upper;
        profileData.SharpnessMin = ar.Sharpness?.Percentile?.Lower;
        profileData.SharpnessMax = ar.Sharpness?.Percentile?.Upper;
        profileData.SharpnessMean = ar.Sharpness?.Mean;
        profileData.SharpnessSigma = ar.Sharpness?.Sigma;
        profileData.SharpnessValues = ar.SharpnessValues;
        profileData.SharpnessPercentileLower = ar.Sharpness?.Percentile?.Lower;
        profileData.SharpnessPercentileUpper = ar.Sharpness?.Percentile?.Upper;
        profileData.SharpnessBaseValue = ar.Sharpness?.Mean;
        profileData.BrightnessLimitMode = LimitMode.Percent;
        profileData.BrightnessPercentLower = 10;
        profileData.BrightnessPercentUpper = 10;
        profileData.BrightnessFixedMin = profileData.BrightnessMin;
        profileData.BrightnessFixedMax = profileData.BrightnessMax;
        profileData.SharpnessLimitMode = LimitMode.Percent;
        profileData.SharpnessPercentLower = 10;
        profileData.SharpnessFixedMin = profileData.SharpnessMin;
        profileData.ImagePath = Path.Combine(new string[] { BasePath, newProfileName, @"pattern.jpg" });
        profileData.PatternMatchRoiX = roi.X;
        profileData.PatternMatchRoiY = roi.Y;
        profileData.PatternMatchRoiW = roi.Width;
        profileData.PatternMatchRoiH = roi.Height;
        profileData.MonitorRoiX = brightnessRoi.X;
        profileData.MonitorRoiY = brightnessRoi.Y;
        profileData.MonitorRoiW = brightnessRoi.Width;
        profileData.MonitorRoiH = brightnessRoi.Height;
        profileData.BrightnessRoiX = brightnessRoi.X;
        profileData.BrightnessRoiY = brightnessRoi.Y;
        profileData.BrightnessRoiW = brightnessRoi.Width;
        profileData.BrightnessRoiH = brightnessRoi.Height;
        profileData.SharpnessRoiX = sharpnessRoi.X;
        profileData.SharpnessRoiY = sharpnessRoi.Y;
        profileData.SharpnessRoiW = sharpnessRoi.Width;
        profileData.SharpnessRoiH = sharpnessRoi.Height;
        profileData.ModifyDateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        profileData.EnableDynamicThreshold  = enableDynamicThreshold;
        profileData.DynamicThresholdCount = dynamicThresholdCount;
        {
            //假如全部全距小於10 extend to 10
            //var 全距 = ar.Brightness?.Percentile?.Upper - ar.Brightness?.Percentile?.Lower;
            //if (Math.Abs((decimal)全距) <= 10)
            //{
            //    var midd = ar.Brightness?.Percentile?.Lower + 全距 / 2;
            //    profileData.BrightnessMin = midd - 5;
            //    profileData.BrightnessMax = midd + 5;
            //}
            if (ar.Brightness.Sigma <1.0 )
            {
                profileData.BrightnessMin = ar.Brightness.Mean - 3;
                profileData.BrightnessMax = ar.Brightness.Mean + 3;

            }
        }

        try
        {
            var s = JsonSerializer.Serialize<ProfileData>(profileData, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
            DirectoryInfo rootDir = new DirectoryInfo(BasePath);
            string d = Path.Combine(rootDir.FullName, newProfileName);
            Directory.CreateDirectory(d);
            File.WriteAllText(Path.Combine(d, "profile.json"), s);
            File.Copy(patternImagePath, profileData.ImagePath, true);
        }
        catch (Exception)
        {
            throw;
        }
    }
    public void SaveProfileSettings(ProfileData profileData)
    {
        if (string.IsNullOrWhiteSpace(profileData.Name))
            throw new InvalidOperationException("Profile name must be set before saving");

        string profileDir = Path.Combine(BasePath, profileData.Name);
        Directory.CreateDirectory(profileDir);
        string filePath = Path.Combine(profileDir, "profile.json");
        var json = JsonSerializer.Serialize(profileData, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        File.WriteAllText(filePath, json);
    }

    public void DeleteProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
            throw new InvalidOperationException("Profile name must be set before deleting");

        string baseFullPath = Path.GetFullPath(BasePath);
        string profileDir = Path.GetFullPath(Path.Combine(BasePath, profileName));
        if (!profileDir.StartsWith(baseFullPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Profile path is outside the profile directory");

        if (Directory.Exists(profileDir))
        {
            Directory.Delete(profileDir, true);
        }
    }

    public List<ProfileData> LoadAllProfile()
    {
        allProfiles.Clear();
        DirectoryInfo rootDir = new DirectoryInfo(BasePath);

        // 獲取直接子資料夾（非遞歸）
        DirectoryInfo[] subDirs = rootDir.GetDirectories();

        Trace.WriteLine("直接子資料夾（包含詳細資訊）：");
        foreach (DirectoryInfo dir in subDirs)
        {
            Trace.WriteLine($"名稱: {dir.Name}, 創建時間: {dir.CreationTime}");

            bool hasProfileJson = File.Exists(Path.Combine(dir.FullName, "profile.json"));
            bool hasPatternJpg = File.Exists(Path.Combine(dir.FullName, "pattern.jpg"));

            if (hasPatternJpg && hasProfileJson)
            {
                ProfileData? profileData;
                try
                {
                    var json = File.ReadAllText(Path.Combine(dir.FullName, "profile.json"));
                    profileData = JsonSerializer.Deserialize<ProfileData>(json);
                    profileData.Bitmap = LoadBitmapFromPath(profileData.ImagePath);
                    profileData.ImageHeight = profileData.Bitmap.Height;
                    profileData.ImageWidth = profileData.Bitmap.Width;
                    if (profileData is not null) allProfiles.Add(profileData);
                }
                catch (Exception ex)
                {
                    Logmanager.loger.Error($"讀取profile失敗, FullName: {dir.FullName}, ex: {ex} ");
                }
            }
        }
        return allProfiles;
    }
    private ProfileData currentProfileData;
    public void SetCurrentProfile(ProfileData pd)
    {
        currentProfileData = pd;
    }

    /// <summary>
    /// 獲取當前配置目錄路徑
    /// </summary>
    public string? ProfileDir => _profileDir;

    /// <summary>
    /// 獲取當前配置的字典形式
    /// </summary>
    public Dictionary<string, object?> GetProfile()
    {
        return new Dictionary<string, object?>
        {
            ["name"] = Name,
            ["brightness_min"] = BrightnessMin,
            ["brightness_max"] = BrightnessMax,
            ["sharpness_min"] = SharpnessMin,
            ["sharpness_max"] = SharpnessMax,
            //["roi_x1"] = RoiX1,
            //["roi_y1"] = RoiY1,
            //["roi_x2"] = RoiX2,
            //["roi_y2"] = RoiY2,
            ["imagePath"] = ImagePath,
            ["template_matching"] = TemplateMatching,
            ["roi_x"] = RoiX,
            ["roi_y"] = RoiY,
            ["roi_w"] = RoiW,
            ["roi_h"] = RoiH,
        };
    }


    public static BitmapImage LoadBitmapFromPath(string imagePath)
    {
        BitmapImage bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(imagePath, UriKind.RelativeOrAbsolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad; // 强制加载到内存
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static string? ResolveImagePath(string? imagePath, string? profileDirectory)
    {
        if (!string.IsNullOrWhiteSpace(imagePath))
        {
            if (File.Exists(imagePath)) return Path.GetFullPath(imagePath);
            if (!string.IsNullOrWhiteSpace(profileDirectory))
            {
                string relativeImagePath = Path.Combine(profileDirectory, imagePath);
                if (File.Exists(relativeImagePath)) return Path.GetFullPath(relativeImagePath);
            }
        }

        if (!string.IsNullOrWhiteSpace(profileDirectory))
        {
            string localPatternPath = Path.Combine(profileDirectory, "pattern.jpg");
            if (File.Exists(localPatternPath)) return Path.GetFullPath(localPatternPath);
        }

        return imagePath;
    }

    // 內部用於序列化的數據結構
    public class ProfileData
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("brightness_min")]
        public double? BrightnessMin { get; set; }
        [JsonPropertyName("brightness_max")]
        public double? BrightnessMax { get; set; }
        [JsonPropertyName("brightness_mean")]
        public double? BrightnessMean { get; set; }
        [JsonPropertyName("brightness_sigma")]
        public double? BrightnessSigma { get; set; }
        [JsonPropertyName("brightness_values")]
        public List<double>? BrightnessValues { get; set; }
        [JsonPropertyName("brightness_percentile_lower")]
        public double? BrightnessPercentileLower { get; set; }
        [JsonPropertyName("brightness_percentile_upper")]
        public double? BrightnessPercentileUpper { get; set; }
        [JsonPropertyName("brightness_base_value")]
        public double? BrightnessBaseValue { get; set; }
        [JsonPropertyName("detection_mode")]
        public string DetectionMode { get; set; } = InspectionMode.Brightness;
        [JsonPropertyName("brightness_limit_mode")]
        public string BrightnessLimitMode { get; set; } = LimitMode.Percent;
        [JsonPropertyName("brightness_percent_lower")]
        public double BrightnessPercentLower { get; set; } = 10;
        [JsonPropertyName("brightness_percent_upper")]
        public double BrightnessPercentUpper { get; set; } = 10;
        [JsonPropertyName("brightness_fixed_min")]
        public double? BrightnessFixedMin { get; set; }
        [JsonPropertyName("brightness_fixed_max")]
        public double? BrightnessFixedMax { get; set; }
        [JsonPropertyName("sharpness_min")]
        public double? SharpnessMin { get; set; }
        [JsonPropertyName("sharpness_max")]
        public double? SharpnessMax { get; set; }
        [JsonPropertyName("sharpness_mean")]
        public double? SharpnessMean { get; set; }
        [JsonPropertyName("sharpness_sigma")]
        public double? SharpnessSigma { get; set; }
        [JsonPropertyName("sharpness_values")]
        public List<double>? SharpnessValues { get; set; }
        [JsonPropertyName("sharpness_percentile_lower")]
        public double? SharpnessPercentileLower { get; set; }
        [JsonPropertyName("sharpness_percentile_upper")]
        public double? SharpnessPercentileUpper { get; set; }
        [JsonPropertyName("sharpness_base_value")]
        public double? SharpnessBaseValue { get; set; }
        [JsonPropertyName("sharpness_limit_mode")]
        public string SharpnessLimitMode { get; set; } = LimitMode.Percent;
        [JsonPropertyName("sharpness_percent_lower")]
        public double SharpnessPercentLower { get; set; } = 10;
        [JsonPropertyName("sharpness_fixed_min")]
        public double? SharpnessFixedMin { get; set; }
        //[JsonPropertyName("roi_x1")]
        //public int? RoiX1 { get; set; }
        //[JsonPropertyName("roi_y1")]
        //public int? RoiY1 { get; set; }
        //[JsonPropertyName("roi_x2")]
        //public int? RoiX2 { get; set; }
        //[JsonPropertyName("roi_y2")]
        //public int? RoiY2 { get; set; }
        [JsonPropertyName("imagePath")]
        public string? ImagePath { get; set; }
        [JsonPropertyName("template_matching")]
        public bool TemplateMatching { get; set; } = true;

        [JsonPropertyName("patternmatch_roi_x")]
        public int? PatternMatchRoiX { get; set; } = 0;
        [JsonPropertyName("patternmatch_roi_y")]
        public int? PatternMatchRoiY { get; set; } = 0;
        [JsonPropertyName("patternmatch_roi_w")]
        public int? PatternMatchRoiW { get; set; } = 0;
        [JsonPropertyName("patternmatch_roi_h")]
        public int? PatternMatchRoiH { get; set; } = 0;

        [JsonPropertyName("monitor_roi_x")]
        public int? MonitorRoiX { get; set; } = 0;
        [JsonPropertyName("monitor_roi_y")]
        public int? MonitorRoiY { get; set; } = 0;
        [JsonPropertyName("monitor_roi_w")]
        public int? MonitorRoiW { get; set; } = 0;
        [JsonPropertyName("monitor_roi_h")]
        public int? MonitorRoiH { get; set; } = 0;

        [JsonPropertyName("brightness_roi_x")]
        public int? BrightnessRoiX { get; set; }
        [JsonPropertyName("brightness_roi_y")]
        public int? BrightnessRoiY { get; set; }
        [JsonPropertyName("brightness_roi_w")]
        public int? BrightnessRoiW { get; set; }
        [JsonPropertyName("brightness_roi_h")]
        public int? BrightnessRoiH { get; set; }

        [JsonPropertyName("sharpness_roi_x")]
        public int? SharpnessRoiX { get; set; }
        [JsonPropertyName("sharpness_roi_y")]
        public int? SharpnessRoiY { get; set; }
        [JsonPropertyName("sharpness_roi_w")]
        public int? SharpnessRoiW { get; set; }
        [JsonPropertyName("sharpness_roi_h")]
        public int? SharpnessRoiH { get; set; }

        [JsonIgnore]
        public int? EffectiveBrightnessRoiX => HasBrightnessRoi ? BrightnessRoiX : MonitorRoiX;
        [JsonIgnore]
        public int? EffectiveBrightnessRoiY => HasBrightnessRoi ? BrightnessRoiY : MonitorRoiY;
        [JsonIgnore]
        public int? EffectiveBrightnessRoiW => HasBrightnessRoi ? BrightnessRoiW : MonitorRoiW;
        [JsonIgnore]
        public int? EffectiveBrightnessRoiH => HasBrightnessRoi ? BrightnessRoiH : MonitorRoiH;
        [JsonIgnore]
        public int? EffectiveSharpnessRoiX => HasSharpnessRoi ? SharpnessRoiX : MonitorRoiX;
        [JsonIgnore]
        public int? EffectiveSharpnessRoiY => HasSharpnessRoi ? SharpnessRoiY : MonitorRoiY;
        [JsonIgnore]
        public int? EffectiveSharpnessRoiW => HasSharpnessRoi ? SharpnessRoiW : MonitorRoiW;
        [JsonIgnore]
        public int? EffectiveSharpnessRoiH => HasSharpnessRoi ? SharpnessRoiH : MonitorRoiH;
        [JsonIgnore]
        private bool HasBrightnessRoi => BrightnessRoiW.GetValueOrDefault() > 0 && BrightnessRoiH.GetValueOrDefault() > 0;
        [JsonIgnore]
        private bool HasSharpnessRoi => SharpnessRoiW.GetValueOrDefault() > 0 && SharpnessRoiH.GetValueOrDefault() > 0;

        public string? ModifyDateTime { get; set; }
        [JsonIgnore]
        public BitmapImage Bitmap { get; set; }
        [JsonIgnore]
        public double ImageWidth { get; set; }
        [JsonIgnore]
        public double ImageHeight { get; set; }
        [JsonPropertyName("dynamicThresholdCount")]
        public int DynamicThresholdCount { get; set; }
        [JsonPropertyName("enableDynamicThreshold")]
        public bool EnableDynamicThreshold { get; set; }
    }
    private DBManager _dbManager = new DBManager();

    public static class LimitMode
    {
        public const string Percent = "Percent";
        public const string Fixed = "Fixed";
    }

    public static class InspectionMode
    {
        public const string Brightness = "亮度";
        public const string BrightnessSharpness = "亮度+銳利度";
    }

    private void ApplyBrightnessThreshold(ProfileData profileData)
    {
        if (profileData.BrightnessLimitMode == LimitMode.Fixed)
        {
            BrightnessMin = profileData.BrightnessFixedMin ?? profileData.BrightnessMin;
            BrightnessMax = profileData.BrightnessFixedMax ?? profileData.BrightnessMax;
            return;
        }

        double? baseValue = profileData.BrightnessBaseValue ?? profileData.BrightnessMean;
        if (baseValue.HasValue)
        {
            BrightnessMin = baseValue.Value * (1 - profileData.BrightnessPercentLower / 100.0);
            BrightnessMax = baseValue.Value * (1 + profileData.BrightnessPercentUpper / 100.0);
            return;
        }

        BrightnessMin = profileData.BrightnessMin;
        BrightnessMax = profileData.BrightnessMax;
    }

    private double? CalculateSharpnessMin(ProfileData profileData)
    {
        if (profileData.SharpnessLimitMode == LimitMode.Fixed)
        {
            return profileData.SharpnessFixedMin ?? profileData.SharpnessMin;
        }

        double? baseValue = profileData.SharpnessBaseValue ?? profileData.SharpnessMean;
        if (baseValue.HasValue)
        {
            return baseValue.Value * (1 - profileData.SharpnessPercentLower / 100.0);
        }

        return profileData.SharpnessMin;
    }
}
