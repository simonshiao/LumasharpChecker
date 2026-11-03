using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Emgu.CV;
using LumasharpChecker.model;
using LumasharpChecker.view;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic;
using static ProfileManager;
using System.Windows.Media;
using System.Windows.Threading;
namespace LumasharpChecker.viewmodel
{
    public class TestImageResultRow
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ProcessedImagePath { get; set; } = string.Empty;
        public int FrameOriginX { get; set; }
        public int FrameOriginY { get; set; }
        public bool ShowProcessedPreview { get; set; }
        public string InspectionPipeline { get; set; } = "Profile";
        public double Brightness { get; set; }
        public double? BrightnessMin { get; set; }
        public double? BrightnessMax { get; set; }
        public double BrightnessDeltaPercent { get; set; }
        public double Sharpness { get; set; }
        public bool BrightnessSafe { get; set; }
        public bool BrightnessMeasured { get; set; } = true;
        public bool LightInspectionSkipped { get; set; }
        public bool SharpnessSafe { get; set; }
        public bool IncludeSharpness { get; set; } = true;
        public bool IsMatched { get; set; }
        /// <summary>Expected class from the source DB folder (OK/NG); this is not the detector output.</summary>
        public string ReferenceClass { get; set; } = "未標記";
        public int RoiX { get; set; }
        public int RoiY { get; set; }
        public int RoiW { get; set; }
        public int RoiH { get; set; }
        public int BrightnessRoiX { get; set; }
        public int BrightnessRoiY { get; set; }
        public int BrightnessRoiW { get; set; }
        public int BrightnessRoiH { get; set; }
        public int SharpnessRoiX { get; set; }
        public int SharpnessRoiY { get; set; }
        public int SharpnessRoiW { get; set; }
        public int SharpnessRoiH { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsOverallPass => (LightInspectionSkipped || BrightnessSafe) && (!IncludeSharpness || SharpnessSafe) && IsMatched;
        public bool IsRetest => InspectionPipeline == "新版流程" && !BrightnessMeasured && !LightInspectionSkipped;
        public string BrightnessResult => !BrightnessMeasured ? "未執行" : BrightnessSafe ? "OK" : "NG";
        public string BrightnessDeltaPercentText => double.IsNaN(BrightnessDeltaPercent) ? "N/A" : $"{BrightnessDeltaPercent:+0.00;-0.00;0.00}%";
        public string SharpnessResult => IncludeSharpness ? (SharpnessSafe ? "OK" : "NG") : "N/A";
        public string OverallResult => IsRetest ? "Retest" : IsOverallPass ? "OK" : "NG";
        public string MatchResult => IsRetest ? "未完成" : IsMatched ? "OK" : "NG";
        public string ClassificationCheck => ReferenceClass switch
        {
            _ when IsRetest => "待複判",
            "OK" when IsOverallPass => "正確",
            "OK" => "OK 誤判 NG",
            "NG" when !IsOverallPass => "正確",
            "NG" => "NG 誤判 OK",
            _ => "未標記"
        };
        public string RoiDisplay => IncludeSharpness
            ? $"亮度 X:{BrightnessRoiX} Y:{BrightnessRoiY} W:{BrightnessRoiW} H:{BrightnessRoiH} / 銳利度 X:{SharpnessRoiX} Y:{SharpnessRoiY} W:{SharpnessRoiW} H:{SharpnessRoiH}"
            : $"亮度 X:{BrightnessRoiX} Y:{BrightnessRoiY} W:{BrightnessRoiW} H:{BrightnessRoiH}";
    }

    public class TestImageTreeNode
    {
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
        public ObservableCollection<TestImageTreeNode> Children { get; set; } = new();
    }

    public sealed class StationDatabasePathViewModel
    {
        public int Number { get; init; }
        public string OkPath { get; init; } = string.Empty;
        public string NgPath { get; init; } = string.Empty;
        public string AllPath { get; init; } = string.Empty;
        public bool OkDirectoryExists { get; init; }
        public bool NgDirectoryExists { get; init; }
        public bool AllDirectoryExists { get; init; }
        public string OkStatus => string.IsNullOrWhiteSpace(OkPath) ? "未設定" : OkDirectoryExists ? "可用" : "不存在";
        public string NgStatus => string.IsNullOrWhiteSpace(NgPath) ? "未設定" : NgDirectoryExists ? "可用" : "不存在";
        public string AllStatus => string.IsNullOrWhiteSpace(AllPath) ? "未設定" : AllDirectoryExists ? "可用" : "不存在";
        public string OkPathDisplay => string.IsNullOrWhiteSpace(OkPath) ? "—" : OkPath;
        public string NgPathDisplay => string.IsNullOrWhiteSpace(NgPath) ? "—" : NgPath;
        public string AllPathDisplay => string.IsNullOrWhiteSpace(AllPath) ? "—" : AllPath;
        public Brush OkStatusBrush => string.IsNullOrWhiteSpace(OkPath) ? Brushes.LightGray :
            OkDirectoryExists ? Brushes.MediumSeaGreen : Brushes.IndianRed;
        public Brush NgStatusBrush => string.IsNullOrWhiteSpace(NgPath) ? Brushes.LightGray :
            NgDirectoryExists ? Brushes.MediumSeaGreen : Brushes.IndianRed;
        public Brush AllStatusBrush => string.IsNullOrWhiteSpace(AllPath) ? Brushes.LightGray :
            AllDirectoryExists ? Brushes.MediumSeaGreen : Brushes.IndianRed;
    }

    public static class BatchPreviewModes
    {
        public const string All = "全部縮圖";
        public const string NgOnly = "只顯示 NG 縮圖";
        public const string None = "不顯示縮圖";
    }

    partial class mainViewModel : ObservableObject
    {
        private readonly ConcurrentDictionary<string, processInfo> _processMonitor = new();
        private ProfileManager profileManager = new("profile");
        private readonly RecipeManager recipeManager;
        private readonly RecipeManager.AppPreferences _preferences;
        private readonly DispatcherTimer _sharedReportDateMonitor;
        private readonly DispatcherTimer _sharedReportDateDebounce;
        private FileSystemWatcher? _sharedReportDateWatcher;
        private string? _sharedReportWatchedRoot;
        private int _sharedReportDateCheckRunning;
        private bool _isInitializing;
        private bool _isLoadingRecipe;
        private bool _continuousTestLoopActive;
        private bool _continuousTestPaused;
        private int _continuousTestNextIndex;
        private string? _activeStationDatabase;
        private int _databaseLoadGeneration;
        private readonly Dictionary<string, int> _testImageResultIndexes = new(StringComparer.OrdinalIgnoreCase);
        private int _brightnessOkResultCount;
        private int _brightnessNgResultCount;
        private int _sharpnessOkResultCount;
        private int _overallOkResultCount;

        // 對外暴露可綁定集合（無需手動觸發通知，ObservableCollection 自帶集合變化事件）
        public ObservableCollection<processInfo> ProcessList { get; } = new();

        /// <summary>
        /// 新配方各站首次開啟定位工具時，依本站目前圖片尺寸套用初始 ROI 比例。
        /// 完成後會清除待套用標記，避免覆蓋使用者後續調整。
        /// </summary>
        public bool ApplyInitialRoiScaleForCurrentImage()
        {
            if (CurrentRecipe == null || string.IsNullOrWhiteSpace(CurrentTestImagePath)) return false;
            return NativeRecipeBridge.ApplyInitialRoiScaleForImage(recipeManager, CurrentRecipe,
                CurrentStationNumber, CurrentTestImagePath, AppDomain.CurrentDomain.BaseDirectory);
        }

        public void SafeAddOrUpdate(string key, processInfo info)
        {
            // 若當前非 UI 線程，則調度到主線程執行
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => AddOrUpdateProcess(key, info));
            }
            else
            {
                AddOrUpdateProcess(key, info);
            }
        }
        // 添加或更新數據的方法
        public void AddOrUpdateProcess(string key, processInfo info)
        {
            // 更新 ConcurrentDictionary
            _processMonitor.AddOrUpdate(key, info, (k, old) => info);

            // 同步到 ObservableCollection（需在主線程操作）
            var existing = ProcessList.FirstOrDefault(p => p.Profile == info.Profile);
            if (existing != null)
            {
                var index = ProcessList.IndexOf(existing);
                ProcessList[index] = info;  // 直接替換對象以觸發更新
            }
            else
            {
                ProcessList.Add(info);
            }
        }

        private string _serverStatus = "未連接";
        public string ServerStatus
        {
            get => _serverStatus;
            set => SetProperty(ref _serverStatus, value);
        }
        private Brush _brightnessColor = Brushes.Black;
        public Brush BrightnessColor
        {
            get => _brightnessColor;
            set => SetProperty(ref _brightnessColor, value);
        }

        private Brush _sharpnessColor = Brushes.Black;
        public Brush SharpnessColor
        {
            get => _sharpnessColor;
            set => SetProperty(ref _sharpnessColor, value);
        }

        //public mainViewModel() 
        //{
        //    //var Listener = new model.Listener();
        //    //Listener.Start();

        //    var _server = new model.AsyncTcpServer();
        //    _server.ListerStatusChanged += _server_StatusChanged;
        //    _server.ProcessStatusChanged += _server_ProcessStatusChanged;  
        //    _server.StartAsync();

        //    var t = profileManager.LoadAllProfile();

        //    _items = new ObservableCollection<ProfileData>(t);

        //}

        private void _server_StatusChanged(string obj)
        {
            ServerStatus = obj;
        }

        private void _server_ProcessStatusChanged(processInfo obj)
        {
            if (obj.Status == "Complete")
            {
                SafeAddOrUpdate(obj.Profile, obj);
            }
        }
        [ObservableProperty]
        private ObservableCollection<ProfileData> _items = new();

        [ObservableProperty]
        private ObservableCollection<RecipeManager.RecipeManifest> _recipes = new();

        [ObservableProperty]
        private RecipeManager.RecipeManifest? _selectedRecipe;

        [ObservableProperty]
        private RecipeManager.RecipeManifest? _currentRecipe;

        [ObservableProperty]
        private ObservableCollection<int> _stationCountOptions = new(new[] { 8, 10 });

        [ObservableProperty]
        private int _stationCount = 8;

        [ObservableProperty]
        private ObservableCollection<StationSlotViewModel> _stationSlots = new();

        [ObservableProperty]
        private int _globalRecentImageCount = 1;

        [ObservableProperty]
        private ObservableCollection<StationDatabasePathViewModel> _stationDatabasePaths = new();

        [ObservableProperty]
        private string _databaseDayFolderDisplay = string.Empty;

        public string SharedReportSourceFolderDisplay =>
            string.IsNullOrWhiteSpace(_preferences.DatabaseDayFolder) ? "尚未設定" : _preferences.DatabaseDayFolder;

        public bool AutoUpdateLatestSharedReportDay
        {
            get => _preferences.AutoUpdateLatestSharedReportDay;
            set
            {
                if (_preferences.AutoUpdateLatestSharedReportDay == value) return;
                bool previousValue = _preferences.AutoUpdateLatestSharedReportDay;
                _preferences.AutoUpdateLatestSharedReportDay = value;
                try
                {
                    recipeManager.SavePreferences(_preferences);
                    OnPropertyChanged();
                    DatabaseSaveMessage = value
                        ? "已儲存：自動偵測並更新最新日期。"
                        : "已儲存：自動日期更新已關閉。";
                    if (value)
                    {
                        ConfigureSharedReportDateWatcher(_preferences.DatabaseDayFolder
                            ?? CurrentRecipe?.DatabaseDayFolder ?? DefaultSharedReportRoot);
                        _ = CheckLatestSharedReportDateAsync();
                    }
                    else
                    {
                        _sharedReportDateDebounce.Stop();
                        ConfigureSharedReportDateWatcher(null);
                    }
                }
                catch (Exception ex)
                {
                    _preferences.AutoUpdateLatestSharedReportDay = previousValue;
                    OnPropertyChanged();
                    DatabaseSaveMessage = "自動日期更新設定無法儲存：" + ex.Message;
                }
            }
        }

        [ObservableProperty]
        private string _inspectionImageOutputFolderDisplay = string.Empty;

        [ObservableProperty]
        private string _databaseSaveMessage = "選取後自動儲存";

        [ObservableProperty]
        private bool _isRoiEditing;

        [ObservableProperty]
        private string _selectedRoiType = "定位";

        [ObservableProperty]
        private ObservableCollection<string> _roiTypeItems = new(new[] { "定位", "亮度", "銳利度" });

        [ObservableProperty]
        private int _currentStationNumber = 1;

        [ObservableProperty]
        private string _stationStatusMessage = "請先開啟配方與設定，選擇或新增配方。";

        [ObservableProperty]
        private ObservableCollection<string> _limitModeItems = new(new[] { ProfileManager.LimitMode.Percent, ProfileManager.LimitMode.Fixed });

        [ObservableProperty]
        private ObservableCollection<string> _inspectionModeItems = new(new[] { ProfileManager.InspectionMode.Brightness, ProfileManager.InspectionMode.BrightnessSharpness });

        [ObservableProperty]
        private ObservableCollection<string> _batchPreviewModeItems = new(new[] { BatchPreviewModes.All, BatchPreviewModes.NgOnly, BatchPreviewModes.None });

        [ObservableProperty]
        private ObservableCollection<double> _baselinePercentileItems = new(new[] { 1.0, 2.5, 5.0 });

        [ObservableProperty]
        private ProfileData? _selectedItem;

        // Recipe station profiles are independent copies. This separate field
        // selects only a shared library template to copy into the active station.
        [ObservableProperty]
        private ProfileData? _selectedRecipeTemplate;

        [ObservableProperty]
        private ProfileData? _currentProfile;

        [RelayCommand]
        private void OnSelectionChanged()
        {
            OnPropertyChanged(nameof(CanApplyLibraryProfileToStation));
            if (CurrentRecipe != null) return;
            LoadCurrentProfile(SelectedItem, isLibraryProfile: true);
        }

        private void LoadCurrentProfile(ProfileData? profile, bool isLibraryProfile)
        {
            CurrentProfile = profile;
            if (profile == null)
            {
                ResetProfileEditor();
                UpdateDisplayImage();
                return;
            }

            Trace.WriteLine($"Selected: {profile.Name}");
            if (isLibraryProfile) profileManager.SetCurrentProfile(profile);
            LoadInspectionSettings(profile);
            OriginalX = profile.PatternMatchRoiX ?? 0;
            OriginalY = profile.PatternMatchRoiY ?? 0;
            OriginalWidth = profile.PatternMatchRoiW ?? 0;
            OriginalHeight = profile.PatternMatchRoiH ?? 0;
            OriginalX2 = profile.EffectiveBrightnessRoiX ?? 0;
            OriginalY2 = profile.EffectiveBrightnessRoiY ?? 0;
            OriginalWidth2 = profile.EffectiveBrightnessRoiW ?? 0;
            OriginalHeight2 = profile.EffectiveBrightnessRoiH ?? 0;
            OriginalX3 = profile.EffectiveSharpnessRoiX ?? 0;
            OriginalY3 = profile.EffectiveSharpnessRoiY ?? 0;
            OriginalWidth3 = profile.EffectiveSharpnessRoiW ?? 0;
            OriginalHeight3 = profile.EffectiveSharpnessRoiH ?? 0;
            UpdateScale(oldsize);
            UpdateDisplayImage();
        }

        private void ResetProfileEditor()
        {
            BrightnessLimitMode = ProfileManager.LimitMode.Percent;
            BrightnessBaseValue = 0;
            BrightnessPercentLower = 10;
            BrightnessPercentUpper = 10;
            BrightnessFixedMin = 0;
            BrightnessFixedMax = 0;
            SharpnessLimitMode = ProfileManager.LimitMode.Percent;
            SharpnessBaseValue = 0;
            SharpnessPercentLower = 10;
            SharpnessFixedMin = 0;
            DetectionMode = ProfileManager.InspectionMode.Brightness;
            OriginalX = OriginalY = OriginalWidth = OriginalHeight = 0;
            OriginalX2 = OriginalY2 = OriginalWidth2 = OriginalHeight2 = 0;
            OriginalX3 = OriginalY3 = OriginalWidth3 = OriginalHeight3 = 0;
        }

        private void UpdateDisplayImage()
        {
            // Main preview uses loaded inspection images; legacy profile templates are not display sources.
            if (CurrentTestImageBitmap != null)
            {
                DisplayImage = CurrentTestImageBitmap;
            }
            else
            {
                DisplayImage = null;
            }

            OnPropertyChanged(nameof(StationSettingsImage));
        }

        public BitmapImage? StationSettingsImage => CurrentProfile?.Bitmap ?? DisplayImage;


        [ObservableProperty]
        private double _originalX = 100;  // 原始坐标X

        [ObservableProperty]
        private double _originalY = 50;   // 原始坐标Y

        [ObservableProperty]
        private double _originalWidth = 200; // 原始宽度

        [ObservableProperty]
        private double _originalHeight = 150; // 原始高度

        [ObservableProperty]
        private double _originalX2 = 100;  // 原始坐标X

        [ObservableProperty]
        private double _originalY2 = 50;   // 原始坐标Y

        [ObservableProperty]
        private double _originalWidth2 = 200; // 原始宽度

        [ObservableProperty]
        private double _originalHeight2 = 150; // 原始高度

        [ObservableProperty]
        private double _originalX3 = 100;

        [ObservableProperty]
        private double _originalY3 = 50;

        [ObservableProperty]
        private double _originalWidth3 = 200;

        [ObservableProperty]
        private double _originalHeight3 = 150;

        // 缩放比例（自动计算）
        [ObservableProperty]
        private double _scaleFactor = 2;

        // 动态计算显示坐标
        public double RectX => OriginalX ;
        public double RectY => OriginalY ;
        public double RectWidth => OriginalWidth ;
        public double RectHeight => OriginalHeight ;

        public double RectX2 => OriginalX2;
        public double RectY2 => OriginalY2;
        public double RectWidth2 => OriginalWidth2;
        public double RectHeight2 => OriginalHeight2;
        public double RectX3 => OriginalX3;
        public double RectY3 => OriginalY3;
        public double RectWidth3 => OriginalWidth3;
        public double RectHeight3 => OriginalHeight3;

        // 线条粗细反向缩放
        public double StrokeThickness => 4;// / ScaleFactor;
        public double StrokeThickness2 => 4;// / ScaleFactor;
        public double StrokeThickness3 => 4;

        private void UpdateDisplayRoiProperties()
        {
            OnPropertyChanged(nameof(DisplayRectX));
            OnPropertyChanged(nameof(DisplayRectY));
            OnPropertyChanged(nameof(DisplayRectWidth));
            OnPropertyChanged(nameof(DisplayRectHeight));
            OnPropertyChanged(nameof(DisplayRectX2));
            OnPropertyChanged(nameof(DisplayRectY2));
            OnPropertyChanged(nameof(DisplayRectWidth2));
            OnPropertyChanged(nameof(DisplayRectHeight2));
            OnPropertyChanged(nameof(DisplayRectX3));
            OnPropertyChanged(nameof(DisplayRectY3));
            OnPropertyChanged(nameof(DisplayRectWidth3));
            OnPropertyChanged(nameof(DisplayRectHeight3));
            OnPropertyChanged(nameof(IsGreenBoxVisible));
            OnPropertyChanged(nameof(IsBrightnessBoxVisible));
        }

        // Main preview displays actual inspection results only. Missing results never fall back to legacy templates.
        public double DisplayRectX => TestImageROI.X;
        public double DisplayRectY => TestImageROI.Y;
        public double DisplayRectWidth => TestImageROI.Width;
        public double DisplayRectHeight => TestImageROI.Height;
        public double DisplayRectX2 => TestImageBrightnessROI.X;
        public double DisplayRectY2 => TestImageBrightnessROI.Y;
        public double DisplayRectWidth2 => TestImageBrightnessROI.Width;
        public double DisplayRectHeight2 => TestImageBrightnessROI.Height;
        public double DisplayRectX3 => TestImageSharpnessROI.X;
        public double DisplayRectY3 => TestImageSharpnessROI.Y;
        public double DisplayRectWidth3 => TestImageSharpnessROI.Width;
        public double DisplayRectHeight3 => TestImageSharpnessROI.Height;
        public bool IsGreenBoxVisible => TestImageROI.Width > 0 && TestImageROI.Height > 0;
        public bool IsBrightnessBoxVisible => TestImageBrightnessROI.Width > 0 && TestImageBrightnessROI.Height > 0;

        private Debouncer _refreshDebouncer = new Debouncer(50);
        Size oldsize;
        // 窗口尺寸变化处理
        public void UpdateScale(Size newSize)
        {
            _refreshDebouncer.Debounce(() => {
                // 实际更新逻辑
            
                //ScaleFactor = newSize.Width / OriginalWidth; // 假设原始设计宽度800
                OnPropertyChanged(nameof(RectX));
                OnPropertyChanged(nameof(RectY));
                OnPropertyChanged(nameof(RectWidth));
                OnPropertyChanged(nameof(RectHeight));
                OnPropertyChanged(nameof(StrokeThickness));
                OnPropertyChanged(nameof(RectX2));
                OnPropertyChanged(nameof(RectY2));
                OnPropertyChanged(nameof(RectWidth2));
                OnPropertyChanged(nameof(RectHeight2));
                OnPropertyChanged(nameof(StrokeThickness2));
                OnPropertyChanged(nameof(RectX3));
                OnPropertyChanged(nameof(RectY3));
                OnPropertyChanged(nameof(RectWidth3));
                OnPropertyChanged(nameof(RectHeight3));
                OnPropertyChanged(nameof(StrokeThickness3));
                oldsize = newSize;
            });
            
        }
        public class Debouncer : IDisposable
        {
            private Timer _timer;
            private readonly int _delayMs;
            private Action _action;

            public Debouncer(int delayMs = 300)
            {
                _delayMs = delayMs;
                _timer = new Timer(_ => ExecuteAction(), null, Timeout.Infinite, Timeout.Infinite);
            }

            public void Debounce(Action action)
            {
                _action = action;
                _timer.Change(_delayMs, Timeout.Infinite);
            }

            private void ExecuteAction()
            {
                _action?.Invoke();
                _action = null;
            }

            public void Dispose() => _timer.Dispose();
        }

        private readonly IFileDialogService _fileDialogService;

        [ObservableProperty]
        private ObservableCollection<string> _selectedFiles = new();


        private readonly IWindowService _windowService;

        // 透過依賴注入取得服務
        public mainViewModel(IWindowService windowService)
        {
            _windowService = windowService;
            ServerStatus = "LumasharpChecker";

            recipeManager = new RecipeManager(Environment.CurrentDirectory, profileManager.BasePath);
            _preferences = recipeManager.LoadPreferences();
            _sharedReportDateMonitor = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
            _sharedReportDateMonitor.Tick += SharedReportDateMonitor_Tick;
            _sharedReportDateDebounce = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            _sharedReportDateDebounce.Tick += SharedReportDateDebounce_Tick;
            _isInitializing = true;
            StationCount = _preferences.StationCount;
            _isInitializing = false;

            var t = profileManager.LoadAllProfile();
            _items = new ObservableCollection<ProfileData>(t);
            Recipes = new ObservableCollection<RecipeManager.RecipeManifest>(recipeManager.LoadAllRecipes());
            if(_items.Count!=0)
                _selectedItem = _items[0];
            OnSelectionChanged();
            RefreshStationSlots();
            SelectedRecipe = Recipes.FirstOrDefault(recipe =>
                string.Equals(recipe.Id, _preferences.LastRecipeId, StringComparison.OrdinalIgnoreCase));
            if (SelectedRecipe != null) LoadSelectedRecipe();
            if (_preferences.AutoUpdateLatestSharedReportDay)
                ConfigureSharedReportDateWatcher(_preferences.DatabaseDayFolder
                    ?? CurrentRecipe?.DatabaseDayFolder ?? DefaultSharedReportRoot);
            _sharedReportDateMonitor.Start();
            if (CurrentRecipe != null && _preferences.AutoUpdateLatestSharedReportDay)
                _ = CheckLatestSharedReportDateAsync();
        }

        private const string DefaultVendorRecipeRoot = @"C:\DIAChip8Side\Recipe";
        private const string DefaultSharedReportRoot = @"D:\外觀通共用報告\蕪湖\welding";

        private static bool IsSameVendorSource(RecipeManager.RecipeManifest recipe, string sourcePath)
        {
            return !string.IsNullOrWhiteSpace(recipe.VendorRecipePath) &&
                string.Equals(Path.GetFullPath(recipe.VendorRecipePath).TrimEnd('\\', '/'),
                    Path.GetFullPath(sourcePath).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }

        // Called after the main window is shown; comparison does not modify recipes.
        public void CheckDefaultVendorRecipeChanges()
        {
            if (!Directory.Exists(DefaultVendorRecipeRoot)) return;
            try
            {
                var folders = recipeManager.DiscoverVendorRecipeFolders(DefaultVendorRecipeRoot);
                var matched = folders.Select(folder => new
                {
                    Folder = folder,
                    Existing = Recipes.FirstOrDefault(recipe =>
                        string.Equals(recipe.Name, folder.Name, StringComparison.OrdinalIgnoreCase))
                }).ToList();
                // First import is explicitly initiated with the button.
                if (!matched.Any(item => item.Existing != null && IsSameVendorSource(item.Existing, item.Folder.SourcePath))) return;
                var changes = matched.Where(item => item.Existing == null ||
                    (IsSameVendorSource(item.Existing, item.Folder.SourcePath) &&
                     recipeManager.CompareVendorSnapshot(item.Folder.SourcePath, item.Existing).HasChanges))
                    .Select(item => item.Folder).ToList();
                if (changes.Count == 0) return;
                string names = string.Join("\n", changes.Take(10).Select(folder => "・" + folder.Name));
                if (changes.Count > 10) names += $"\n……另有 {changes.Count - 10} 個";
                if (MessageBox.Show(Application.Current.MainWindow,
                    $"外觀機配方有新增或變更（{changes.Count} 個）：\n{names}\n\n要更新本機副本嗎？站別設定與原廠資料會保留。",
                    "發現原廠配方變動", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    ImportVendorFolders(changes, Application.Current.MainWindow);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.MainWindow, "無法檢查原廠配方變動：" + ex.Message +
                    "\n可在配方管理使用「自動匯入」或「其他匯入」。", "配方檢查", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private void ImportDefaultVendorRecipeSnapshot()
        {
            if (!Directory.Exists(DefaultVendorRecipeRoot))
            {
                MessageBox.Show("找不到外觀機配方資料夾：\n" + DefaultVendorRecipeRoot +
                    "\n請確認路徑，或使用「其他匯入」。", "自動匯入", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            OpenVendorImport(DefaultVendorRecipeRoot);
        }

        [RelayCommand]
        private void CreateRecipe()
        {
            string name = Interaction.InputBox("輸入配方名稱。新配方全部 8 站會以 t1 第一站作為參數預設基準；定位、選面、選向範圍預設為 0–99999。", "建立配方", "新配方").Trim();
            if (string.IsNullOrWhiteSpace(name)) return;
            try
            {
                var recipe = recipeManager.CreateRecipe(name, StationCount);
                try
                {
                    NativeRecipeBridge.InitializeLocalRecipe(recipe, AppDomain.CurrentDomain.BaseDirectory);
                    recipeManager.SaveManifest(recipe);
                }
                catch
                {
                    recipeManager.DeleteRecipe(recipe);
                    throw;
                }
                Recipes.Add(recipe);
                SelectedRecipe = recipe;
                LoadSelectedRecipe();
                SaveSettingsMessage = $"已建立配方：{name}（全部 8 站依 t1 第一站帶入參數；定位、選面、選向範圍 0–99999）";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "建立配方失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private void ImportVendorRecipeSnapshot()
        {
            Window owner = Application.Current.Windows.OfType<RecipeManagerWindow>()
                .FirstOrDefault(window => window.IsVisible) ?? Application.Current.MainWindow;
            string? folder = ExplorerFolderPicker.PickFolder(owner, "選擇原廠配方資料庫資料夾", Environment.CurrentDirectory);
            if (folder != null) OpenVendorImport(folder);
        }

        private void OpenVendorImport(string folder)
        {
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting)
            {
                MessageBox.Show("檢測進行中，請先完成或停止。", "匯入原廠資料", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Window owner = Application.Current.Windows.OfType<RecipeManagerWindow>()
                .FirstOrDefault(window => window.IsVisible) ?? Application.Current.MainWindow;
            try
            {
                var importWindow = new VendorRecipeImportWindow(owner, recipeManager, Recipes.ToList(), folder);
                if (importWindow.ShowDialog() == true) ImportVendorFolders(importWindow.SelectedFolders, owner);
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, ex.Message, "原廠配方匯入失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ImportVendorFolders(IEnumerable<RecipeManager.VendorRecipeFolder> folders, Window owner)
        {
            int created = 0, updated = 0;
            var failures = new List<string>();
            foreach (var source in folders)
            {
                try
                {
                    var existing = Recipes.FirstOrDefault(recipe =>
                        string.Equals(recipe.Name, source.Name, StringComparison.OrdinalIgnoreCase));
                    var imported = recipeManager.ImportOrUpdateVendorSnapshot(source.SourcePath, existing);
                    if (existing == null) { Recipes.Add(imported); created++; }
                    else updated++;
                }
                catch (Exception ex) { failures.Add($"{source.Name}：{ex.Message}"); }
            }
            SaveSettingsMessage = $"原廠配方匯入完成：新建 {created} 個、更新 {updated} 個，失敗 {failures.Count} 個。";
            string result = SaveSettingsMessage + "\n站別檢測設定已保留；原廠參數仍是獨立副本。已載入的配方請重新載入。";
            if (failures.Count > 0) result += "\n\n" + string.Join("\n", failures.Take(8));
            MessageBox.Show(owner, result, failures.Count == 0 ? "匯入完成" : "匯入部分完成",
                MessageBoxButton.OK, failures.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        [RelayCommand]
        private void ApplyTrainingImageToCurrentRecipe()
        {
            if (CurrentRecipe == null) return;
            Window owner = Application.Current.Windows.OfType<DatabaseSetupWindow>()
                .FirstOrDefault(window => window.IsVisible) ?? Application.Current.MainWindow;
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting)
            {
                MessageBox.Show(owner, "檢測進行中，請完成或停止後再設定圖庫。", "訓練圖庫", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                const string defaultPath = @"D:\2-新任務\welding\DIAChip8Side\TrainingImage";
                string? folder = ExplorerFolderPicker.PickFolder(owner,
                    "選擇產品 CCD 資料夾，例如 R002-CCD1", Directory.Exists(defaultPath) ? defaultPath : Environment.CurrentDirectory);
                if (folder == null) return;
                var found = TrainingImageDiscovery.Discover(folder, CurrentRecipe.StationCount);
                var dialog = new TrainingImageSelectionWindow(owner, found.Product, found.Candidates, CurrentRecipe.StationCount);
                if (dialog.ShowDialog() != true) return;
                var selected = dialog.SelectedCandidates;
                string summary = string.Join("\n", selected.Select(item =>
                    $"第 {item.Station:00} 站：{item.FolderName}（OK {item.OkCount}／NG {item.NgCount}）"));
                if (MessageBox.Show(owner, $"將下列圖庫路徑套用到目前配方「{CurrentRecipe.Name}」：\n\n{summary}\n\n所選站別原有 OK／NG 路徑會被替換；沒有圖片的分類會清空，ALL 路徑也會清空並改由 OK＋NG 計算。其他站別及檢測參數不變。\n\n是否套用？",
                    "確認訓練圖庫", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                var recipe = CurrentRecipe;
                var oldPaths = selected.Select(item =>
                {
                    var station = recipe.Stations.Single(s => s.Number == item.Station);
                    return (Station: station, OkPath: station.OkDatabasePath,
                        NgPath: station.NgDatabasePath, AllPath: station.AllDatabasePath);
                }).ToList();
                var oldRoot = recipe.TrainingImageRoot;
                var oldProduct = recipe.TrainingProductCode;
                var oldDay = recipe.DatabaseDayFolder;
                try
                {
                    foreach (var item in selected)
                    {
                        var station = recipe.Stations.Single(s => s.Number == item.Station);
                        station.OkDatabasePath = item.OkPath;
                        station.NgDatabasePath = item.NgPath;
                        station.AllDatabasePath = null;
                    }
                    recipe.TrainingImageRoot = found.Root;
                    recipe.TrainingProductCode = found.Product;
                    recipe.DatabaseDayFolder = null;
                    recipeManager.SaveManifest(recipe);
                }
                catch
                {
                    foreach (var old in oldPaths)
                    {
                        old.Station.OkDatabasePath = old.OkPath;
                        old.Station.NgDatabasePath = old.NgPath;
                        old.Station.AllDatabasePath = old.AllPath;
                    }
                    recipe.TrainingImageRoot = oldRoot; recipe.TrainingProductCode = oldProduct; recipe.DatabaseDayFolder = oldDay;
                    throw;
                }
                DatabaseDayFolderDisplay = found.Root;
                RefreshStationDatabasePaths(); RefreshStationSlots();
                if (_activeStationDatabase != null && selected.Any(item => item.Station == CurrentStationNumber))
                { ResetTestImageState(); LoadStationDatabase(_activeStationDatabase); }
                DatabaseSaveMessage = $"已套用 {selected.Count} 站訓練圖庫。";
                MessageBox.Show(owner, DatabaseSaveMessage, "訓練圖庫", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, "訓練圖庫設定失敗：\n" + ex.Message, "訓練圖庫", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private void ImportTrainingImage()
        {
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting)
            {
                SaveSettingsMessage = "圖片檢測進行中，請完成後再匯入。";
                return;
            }
            var defaultPath = @"D:\0_軟體開發區\6.模板與共用資源區\原廠母本\DIAChip8Side_Welding\TrainingImage\R002-CCD1";
            try
            {
                Window owner = Application.Current.Windows.OfType<DatabaseSetupWindow>()
                    .FirstOrDefault(window => window.IsVisible) ?? Application.Current.MainWindow;
                string? selectedFolder = view.ExplorerFolderPicker.PickFolder(owner,
                    "匯入訓練圖庫", Directory.Exists(defaultPath) ? defaultPath : Environment.CurrentDirectory);
                if (selectedFolder == null) return;
                ImportTrainingImageRecipeFromPath(selectedFolder);
            }
            catch (Exception ex)
            {
                SaveSettingsMessage = "訓練圖庫匯入失敗：" + ex.Message;
                MessageBox.Show(SaveSettingsMessage, "訓練圖庫匯入失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private void ImportWorkDatabase()
        {
            if (CurrentRecipe == null)
            {
                SaveSettingsMessage = "請先載入配方，再匯入作業圖庫。";
                return;
            }
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting)
            {
                SaveSettingsMessage = "圖片檢測進行中，請完成後再匯入作業圖庫。";
                return;
            }

            try
            {
                Window owner = Application.Current.Windows.OfType<DatabaseSetupWindow>()
                    .FirstOrDefault(window => window.IsVisible) ?? Application.Current.MainWindow;
                string? folder = _preferences.DatabaseDayFolder;
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                {
                    string initialFolder = !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder) ? folder
                        : Directory.Exists(DefaultSharedReportRoot) ? DefaultSharedReportRoot
                        : Environment.CurrentDirectory;
                    folder = view.ExplorerFolderPicker.PickFolder(
                        owner,
                        "匯入作業圖庫 · 選擇共用報告、月份或日期資料夾",
                        initialFolder);
                    if (folder == null) return;
                }

                var discovered = recipeManager.DiscoverSharedReportDays(folder);
                if (discovered.Count == 0)
                {
                    SaveSettingsMessage = "資料夾結構不符：找不到有效的日期/站別資料夾。";
                    MessageBox.Show(owner, SaveSettingsMessage, "匯入作業圖庫", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SaveSharedReportSourceFolder(ResolveSharedReportRootFolder(discovered, folder));

                var availableDays = discovered.SelectMany(item => item.Days).ToList();
                RecipeManager.SharedReportDayInfo targetDay;
                if (_preferences.AutoUpdateLatestSharedReportDay)
                {
                    targetDay = SelectLatestSharedReportDay(availableDays);
                }
                else if (availableDays.Count > 1)
                {
                    // 多日期 → 彈選擇對話窗
                    var dayLists = discovered.Select(d => d.MonthFolder).Distinct().ToList();
                    var dialog = new view.SharedReportImportDialog(
                        discovered.SelectMany(d => d.Days).ToList(),
                        "選擇要匯入的日期",
                        dayLists.Count > 1 ? string.Join(", ", dayLists) : dayLists[0]);
                    if (!dialog.ShowDialog()!.Value || dialog.SelectedDay == null) return;
                    targetDay = dialog.SelectedDay;
                }
                else
                {
                    targetDay = availableDays[0];
                }

                ApplySharedReportDay(targetDay, owner, requireConfirmation: true, automatic: false);
            }
            catch (Exception ex)
            {
                SaveSettingsMessage = "作業圖庫匯入失敗：" + ex.Message;
                MessageBox.Show(SaveSettingsMessage, "匯入作業圖庫失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void ConfigureSharedReportSourceFolder(Window owner)
        {
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting)
            {
                DatabaseSaveMessage = "圖片仍在檢測中，請完成後再設定作業圖庫來源路徑。";
                return;
            }

            string initialFolder = !string.IsNullOrWhiteSpace(_preferences.DatabaseDayFolder)
                && Directory.Exists(_preferences.DatabaseDayFolder)
                ? _preferences.DatabaseDayFolder
                : Directory.Exists(DefaultSharedReportRoot) ? DefaultSharedReportRoot : Environment.CurrentDirectory;
            string? selected = view.ExplorerFolderPicker.PickFolder(
                owner, "設定作業圖庫來源路徑", initialFolder);
            if (selected == null) return;

            var discovered = recipeManager.DiscoverSharedReportDays(selected);
            string sharedRoot = ResolveSharedReportRootFolder(discovered, selected);
            SaveSharedReportSourceFolder(sharedRoot);
            DatabaseSaveMessage = "作業圖庫來源路徑已儲存：" + sharedRoot;
        }

        private void SaveSharedReportSourceFolder(string folder)
        {
            string? previous = _preferences.DatabaseDayFolder;
            _preferences.DatabaseDayFolder = Path.GetFullPath(folder);
            try
            {
                recipeManager.SavePreferences(_preferences);
                OnPropertyChanged(nameof(SharedReportSourceFolderDisplay));
                ConfigureSharedReportDateWatcher(_preferences.DatabaseDayFolder);
            }
            catch
            {
                _preferences.DatabaseDayFolder = previous;
                OnPropertyChanged(nameof(SharedReportSourceFolderDisplay));
                throw;
            }
        }

        private async void SharedReportDateMonitor_Tick(object? sender, EventArgs e)
            => await CheckLatestSharedReportDateAsync();

        private void SharedReportDateWatcher_Changed(object sender, FileSystemEventArgs e)
            => ScheduleSharedReportDateCheck();

        private void SharedReportDateWatcher_Renamed(object sender, RenamedEventArgs e)
            => ScheduleSharedReportDateCheck();

        private void SharedReportDateWatcher_Error(object sender, ErrorEventArgs e)
            => ScheduleSharedReportDateCheck();

        private void ScheduleSharedReportDateCheck()
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_preferences.AutoUpdateLatestSharedReportDay || CurrentRecipe == null) return;
                _sharedReportDateDebounce.Stop();
                _sharedReportDateDebounce.Start();
            }));
        }

        private void SharedReportDateDebounce_Tick(object? sender, EventArgs e)
        {
            _sharedReportDateDebounce.Stop();
            _ = CheckLatestSharedReportDateAsync();
        }

        private void ConfigureSharedReportDateWatcher(string? root)
        {
            if (!_preferences.AutoUpdateLatestSharedReportDay || string.IsNullOrWhiteSpace(root))
                root = null;
            else
            {
                try { root = Path.GetFullPath(root); }
                catch { root = null; }
            }

            if (AreSameDirectory(_sharedReportWatchedRoot, root)) return;
            _sharedReportDateWatcher?.Dispose();
            _sharedReportDateWatcher = null;
            _sharedReportWatchedRoot = null;
            if (root == null || !Directory.Exists(root)) return;

            try
            {
                var watcher = new FileSystemWatcher(root)
                {
                    IncludeSubdirectories = true,
                    InternalBufferSize = 32 * 1024,
                    NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName
                        | NotifyFilters.LastWrite | NotifyFilters.CreationTime
                };
                watcher.Created += SharedReportDateWatcher_Changed;
                watcher.Changed += SharedReportDateWatcher_Changed;
                watcher.Deleted += SharedReportDateWatcher_Changed;
                watcher.Renamed += SharedReportDateWatcher_Renamed;
                watcher.Error += SharedReportDateWatcher_Error;
                _sharedReportDateWatcher = watcher;
                _sharedReportWatchedRoot = root;
                watcher.EnableRaisingEvents = true;
            }
            catch
            {
                _sharedReportDateWatcher?.Dispose();
                _sharedReportDateWatcher = null;
                _sharedReportWatchedRoot = null;
            }
        }

        private async Task CheckLatestSharedReportDateAsync()
        {
            if (!_preferences.AutoUpdateLatestSharedReportDay || CurrentRecipe == null
                || IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting)
                return;
            if (Interlocked.CompareExchange(ref _sharedReportDateCheckRunning, 1, 0) != 0)
                return;

            string recipeId = CurrentRecipe.Id;
            string startingFolder = _preferences.DatabaseDayFolder
                ?? CurrentRecipe.DatabaseDayFolder
                ?? @"D:\外觀通共用報告\蕪湖\welding";
            try
            {
                var scan = await Task.Run(() =>
                {
                    var firstScan = recipeManager.DiscoverSharedReportDays(startingFolder);
                    string sharedRoot = ResolveSharedReportRootFolder(firstScan, startingFolder);
                    var discovered = AreSameDirectory(sharedRoot, startingFolder)
                        ? firstScan
                        : recipeManager.DiscoverSharedReportDays(sharedRoot);
                    var days = discovered.SelectMany(item => item.Days).ToList();
                    return (Root: sharedRoot, Day: SelectLatestSharedReportDay(days));
                });

                if (!_preferences.AutoUpdateLatestSharedReportDay
                    || CurrentRecipe?.Id != recipeId
                    || IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting)
                    return;

                if (!AreSameDirectory(_preferences.DatabaseDayFolder, scan.Root))
                {
                    SaveSharedReportSourceFolder(scan.Root);
                }
                ConfigureSharedReportDateWatcher(scan.Root);

                if (AreSameDirectory(CurrentRecipe.DatabaseDayFolder, scan.Day.DayFolder)
                    && !SharedReportDayNeedsRefresh(CurrentRecipe, scan.Day))
                    return;

                Window owner = Application.Current.Windows.OfType<DatabaseSetupWindow>()
                    .FirstOrDefault(window => window.IsVisible) ?? Application.Current.MainWindow;
                ApplySharedReportDay(scan.Day, owner, requireConfirmation: false, automatic: true);
            }
            catch (Exception ex)
            {
                DatabaseSaveMessage = "自動檢查最新圖庫日期失敗：" + ex.GetBaseException().Message;
            }
            finally
            {
                Interlocked.Exchange(ref _sharedReportDateCheckRunning, 0);
            }
        }

        private void ApplySharedReportDay(
            RecipeManager.SharedReportDayInfo targetDay,
            Window owner,
            bool requireConfirmation,
            bool automatic)
        {
            if (CurrentRecipe == null) return;
            int stationCount = targetDay.Stations.Count(station => station.Number <= CurrentRecipe.StationCount);
            int withOkNg = targetDay.Stations.Count(station => station.Number <= CurrentRecipe.StationCount
                && (station.HasOkSubfolder || station.HasNgSubfolder));
            if (requireConfirmation)
            {
                bool confirm = MessageBox.Show(
                    owner,
                    $"將匯入 {targetDay.DayFolder}\n" +
                    $"各站 ALL/OK/NG 圖庫路徑，共 {stationCount} 站（{withOkNg} 站有 OK/NG 分類）。\n" +
                    "是否繼續？",
                    "確認匯入作業圖庫", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
                if (!confirm) return;
            }

            int okStations = 0, importedStations = 0, clearedStations = 0;
            var skipList = new List<string>();
            for (int i = 0; i < CurrentRecipe.Stations.Count && CurrentRecipe.Stations[i].Number <= CurrentRecipe.StationCount; i++)
            {
                var station = CurrentRecipe.Stations[i];
                var info = targetDay.Stations.FirstOrDefault(item => item.Number == station.Number);
                if (info == null || !info.HasImages)
                {
                    if (ClearStationDatabasePaths(station)) clearedStations++;
                    skipList.Add($"第 {station.Number} 站（當日無資料，已清除舊路徑）");
                    continue;
                }
                try
                {
                    SetStationAllDatabaseFolder(station.Number, info.StationFolder);
                    importedStations++;
                    if (info.HasOkSubfolder || info.HasNgSubfolder) okStations++;
                }
                catch
                {
                    if (ClearStationDatabasePaths(station)) clearedStations++;
                    skipList.Add($"第 {station.Number} 站（匯入失敗，已清除舊路徑）");
                }
            }

            CurrentRecipe.DatabaseDayFolder = targetDay.DayFolder;
            DatabaseDayFolderDisplay = targetDay.DayFolder;
            recipeManager.SaveManifest(CurrentRecipe);
            RefreshStationDatabasePaths();
            RefreshStationSlots();
            OnPropertyChanged(nameof(RecipeConfigurationSummary));

            string? activeDatabase = _activeStationDatabase;
            ResetTestImageState();
            if (activeDatabase != null && !LoadStationDatabase(activeDatabase))
                _activeStationDatabase = null;

            string action = automatic ? "已自動更新作業圖庫" : "已匯入作業圖庫";
            SaveSettingsMessage = $"{action}：{importedStations}/{CurrentRecipe.StationCount} 站；{okStations} 站有 OK/NG 分類；已清除 {clearedStations} 站舊路徑。";
            if (okStations == 0)
                SaveSettingsMessage += " 本次圖片未分 OK/NG，請在有圖站別使用 ALL DB 載入。";
            if (skipList.Count > 0)
                SaveSettingsMessage += $" 跳過：{string.Join(", ", skipList.Take(8))}";
            DatabaseSaveMessage = automatic
                ? $"已自動切換至最新圖庫日期：{GetSharedReportDayLabel(targetDay.DayFolder)}"
                : SaveSettingsMessage;
        }

        private static RecipeManager.SharedReportDayInfo SelectLatestSharedReportDay(
            IEnumerable<RecipeManager.SharedReportDayInfo> days)
            => days.OrderByDescending(GetSharedReportDaySortKey)
                .ThenByDescending(day => Directory.GetLastWriteTimeUtc(day.DayFolder))
                .First();

        private static string ResolveSharedReportRootFolder(
            List<(string MonthFolder, List<RecipeManager.SharedReportDayInfo> Days)> discovered,
            string selectedFolder)
        {
            var roots = discovered
                .Select(item => Directory.GetParent(item.MonthFolder)?.FullName ?? item.MonthFolder)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return roots.Count == 1 ? roots[0] : Path.GetFullPath(selectedFolder);
        }

        private static bool AreSameDirectory(string? left, string? right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
            string normalizedLeft = Path.GetFullPath(left!)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedRight = Path.GetFullPath(right!)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }

        private bool SharedReportDayNeedsRefresh(
            RecipeManager.RecipeManifest recipe,
            RecipeManager.SharedReportDayInfo day)
        {
            foreach (var station in recipe.Stations.Where(item => item.Number <= recipe.StationCount))
            {
                var info = day.Stations.FirstOrDefault(item => item.Number == station.Number && item.HasImages);
                string? expectedAll = info?.StationFolder;
                string? expectedOk = expectedAll != null && Directory.Exists(Path.Combine(expectedAll, "OK"))
                    ? Path.Combine(expectedAll, "OK") : null;
                string? expectedNg = expectedAll != null && Directory.Exists(Path.Combine(expectedAll, "NG"))
                    ? Path.Combine(expectedAll, "NG") : null;

                if (!MatchesOptionalDirectory(station.AllDatabasePath, expectedAll)
                    || !MatchesOptionalDirectory(station.OkDatabasePath, expectedOk)
                    || !MatchesOptionalDirectory(station.NgDatabasePath, expectedNg))
                    return true;
            }
            return false;
        }

        private bool MatchesOptionalDirectory(string? storedPath, string? expectedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath) || string.IsNullOrWhiteSpace(expectedPath))
                return string.IsNullOrWhiteSpace(storedPath) && string.IsNullOrWhiteSpace(expectedPath);
            return AreSameDirectory(recipeManager.ResolveDatabasePath(storedPath), expectedPath);
        }

        private static string GetSharedReportDayLabel(string dayFolder)
        {
            string fullPath = dayFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string day = Path.GetFileName(fullPath);
            string month = Directory.GetParent(fullPath)?.Name ?? string.Empty;
            if (int.TryParse(month, NumberStyles.Integer, CultureInfo.InvariantCulture, out int monthNumber)
                && int.TryParse(day, NumberStyles.Integer, CultureInfo.InvariantCulture, out int dayNumber)
                && monthNumber is >= 1 and <= 12 && dayNumber >= 1
                && dayNumber <= DateTime.DaysInMonth(2000, monthNumber))
                return $"{monthNumber:00}/{dayNumber:00}";
            return day;
        }

        private static int GetSharedReportDaySortKey(RecipeManager.SharedReportDayInfo day)
        {
            string dayFolder = day.DayFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string dayName = Path.GetFileName(dayFolder);
            string monthName = Directory.GetParent(dayFolder)?.Name ?? string.Empty;
            if (!int.TryParse(monthName, NumberStyles.Integer, CultureInfo.InvariantCulture, out int month)
                || !int.TryParse(dayName, NumberStyles.Integer, CultureInfo.InvariantCulture, out int date)
                || month is < 1 or > 12 || date < 1 || date > DateTime.DaysInMonth(2000, month))
                return 0;

            return month * 100 + date;
        }

        private static bool ClearStationDatabasePaths(RecipeManager.RecipeStation station)
        {
            bool hadPaths = !string.IsNullOrWhiteSpace(station.AllDatabasePath)
                || !string.IsNullOrWhiteSpace(station.OkDatabasePath)
                || !string.IsNullOrWhiteSpace(station.NgDatabasePath);
            station.AllDatabasePath = null;
            station.OkDatabasePath = null;
            station.NgDatabasePath = null;
            return hadPaths;
        }

        /// <summary>設定檢測圖檔輸出根目錄，獨立存入目前 Recipe。</summary>
        public void ConfigureWorkDatabaseOutput(Window owner)
        {
            if (CurrentRecipe == null)
            {
                DatabaseSaveMessage = "請先載入配方，再設定圖檔輸出路徑。";
                return;
            }
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting)
            {
                DatabaseSaveMessage = "圖片仍在檢測中，請完成後再設定。";
                return;
            }

            string? folder = view.ExplorerFolderPicker.PickFolder(
                owner,
                "設定此 Recipe 的檢測圖檔輸出根目錄",
                CurrentRecipe.InspectionImageOutputFolder);
            if (folder == null) return;
            CurrentRecipe.InspectionImageOutputFolder = Path.GetFullPath(folder);
            recipeManager.SaveManifest(CurrentRecipe);
            InspectionImageOutputFolderDisplay = CurrentRecipe.InspectionImageOutputFolder;
            DatabaseSaveMessage = $"檢測圖檔輸出路徑已存入 Recipe：{CurrentRecipe.InspectionImageOutputFolder}";
        }

        // 供主畫面與明確 --import-trainingimage 命令列共同使用；不會改原廠母本。
        public void ImportTrainingImageRecipeFromPath(string sourceFolder)
        {
            if (IsAnalyzingTestImages) throw new InvalidOperationException("檢測進行中，不可匯入配方。");
            var recipe = recipeManager.ImportTrainingImageRecipe(sourceFolder, cacheSamples: true);
            var previous = Recipes.FirstOrDefault(item => item.Id == recipe.Id);
            if (previous != null) Recipes[Recipes.IndexOf(previous)] = recipe;
            else Recipes.Add(recipe);
            SelectedRecipe = recipe;
            LoadSelectedRecipe();
            RefreshStationDatabasePaths();
            DatabaseDayFolderDisplay = recipe.TrainingImageRoot ?? string.Empty;
            int available = recipe.Stations.Count(station =>
                station.Number <= 8 && (!string.IsNullOrEmpty(station.OkDatabasePath)
                    || !string.IsNullOrEmpty(station.NgDatabasePath)));
                SaveSettingsMessage = $"已建立配方 {recipe.Name}：{available}/8 站有離線快取；檢測參數須另外校準。";
        }

        [RelayCommand]
        private void LoadSelectedRecipe()
        {
            if (SelectedRecipe == null) return;
            if (IsAnalyzingTestImages || IsMultiStationTesting)
            {
                SaveSettingsMessage = "圖片仍在檢測中，請完成後再切換配方";
                return;
            }

            if (CurrentProfile != null && CurrentRecipe != null) SaveCurrentInspectionSettings(reanalyzeTestImages: false);
            _isLoadingRecipe = true;
            CurrentRecipe = SelectedRecipe;
            StationCount = CurrentRecipe.StationCount is 8 or 10 ? CurrentRecipe.StationCount : StationCount;
            _isLoadingRecipe = false;
            CurrentStationNumber = 1;
            _activeStationDatabase = null;
            ResetTestImageState();
            LoadRecipeStation();
            RefreshStationDatabasePaths();
            EnsureAoiAllDatabase();
            DatabaseDayFolderDisplay = CurrentRecipe.TrainingImageRoot
                ?? CurrentRecipe.DatabaseDayFolder ?? string.Empty;
            _preferences.LastRecipeId = CurrentRecipe.Id;
            _preferences.StationCount = StationCount;
            recipeManager.SavePreferences(_preferences);
            if (_preferences.AutoUpdateLatestSharedReportDay)
                _ = CheckLatestSharedReportDateAsync();
        }

        [RelayCommand]
        private void CloseRecipe()
        {
            if (CurrentProfile != null && CurrentRecipe != null) SaveCurrentInspectionSettings(reanalyzeTestImages: false);
            _activeStationDatabase = null;
            ResetTestImageState();
            CurrentRecipe = null;
            // Closing the active recipe is not the same as forgetting the last
            // recipe. Keep its ID so startup can reopen it next time.
            CurrentStationNumber = 1;
            RefreshStationSlots();
            LoadCurrentProfile(SelectedItem, isLibraryProfile: true);
            StationStatusMessage = "配方已關閉；請開啟配方與設定。";
        }

        [RelayCommand]
        private void DeleteRecipe()
        {
            if (SelectedRecipe == null)
            {
                SaveSettingsMessage = "請先選擇配方";
                return;
            }
            var result = MessageBox.Show($"確定要刪除配方「{SelectedRecipe.Name}」及其站別資料？\n設定檔庫不會受到影響。", "刪除配方", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (CurrentRecipe?.Id == SelectedRecipe.Id) CloseRecipe();
                recipeManager.DeleteRecipe(SelectedRecipe);
                if (string.Equals(_preferences.LastRecipeId, SelectedRecipe.Id, StringComparison.OrdinalIgnoreCase))
                {
                    _preferences.LastRecipeId = null;
                    recipeManager.SavePreferences(_preferences);
                }
                Recipes.Remove(SelectedRecipe);
                SelectedRecipe = null;
                SaveSettingsMessage = "配方已刪除";
            }
            catch (Exception ex)
            {
                SaveSettingsMessage = $"刪除配方失敗：{ex.Message}";
            }
        }

        [RelayCommand]
        private void SelectStation(int stationNumber)
        {
            if (CurrentRecipe == null || stationNumber < 1 || stationNumber > StationCount || stationNumber == CurrentStationNumber) return;
            if (IsAnalyzingTestImages)
            {
                SaveSettingsMessage = "圖片仍在檢測中，請完成後再切換站別";
                return;
            }
            if (CurrentProfile != null) SaveCurrentInspectionSettings(reanalyzeTestImages: false);
            CurrentStationNumber = stationNumber;
            ResetTestImageState();
            LoadRecipeStation();
            _activeStationDatabase = null;
            EnsureAoiAllDatabase();
            RestoreMultiStationResultsForCurrentStation();
        }

        [RelayCommand]
        private void ApplyLibraryProfileToStation()
        {
            if (CurrentRecipe == null || SelectedRecipeTemplate == null) return;
            var template = SelectedRecipeTemplate;
            bool alreadyConfigured = CurrentRecipe.Stations.Single(station => station.Number == CurrentStationNumber).IsConfigured;
            if (alreadyConfigured && MessageBox.Show(
                    $"要以「{template.Name}」取代「{CurrentRecipe.Name}」第 {CurrentStationNumber:00} 站的專屬設定嗎？",
                    "取代站別設定", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                CurrentProfile = recipeManager.AssignProfile(CurrentRecipe, CurrentStationNumber, template);
                LoadCurrentProfile(CurrentProfile, isLibraryProfile: false);
                RefreshStationSlots();
                StationStatusMessage = $"「{CurrentRecipe.Name}」第 {CurrentStationNumber:00} 站已建立「{template.Name}」的專屬副本。";
                SaveSettingsMessage = "站別設定已加入配方";
                OnPropertyChanged(nameof(CurrentInspectionName));
            }
            catch (Exception ex)
            {
                SaveSettingsMessage = $"套用設定檔失敗：{ex.Message}";
            }
        }

        private void LoadRecipeStation()
        {
            if (CurrentRecipe == null) return;
            // A shared template is only an input for applying a profile; it is
            // not the active Recipe station profile. Clear it on every switch.
            SelectedRecipeTemplate = null;
            try
            {
                var profile = recipeManager.LoadStation(CurrentRecipe, CurrentStationNumber);
                LoadCurrentProfile(profile, isLibraryProfile: false);
                string? sourceName = CurrentRecipe.Stations
                    .FirstOrDefault(station => station.Number == CurrentStationNumber)?.SourceProfileName;
                SelectedRecipeTemplate = string.IsNullOrWhiteSpace(sourceName)
                    ? null
                    : Items.FirstOrDefault(item => string.Equals(
                        item.Name, sourceName, StringComparison.OrdinalIgnoreCase));
                RefreshStationSlots();
                StationStatusMessage = profile == null
                    ? $"第 {CurrentStationNumber:00} 站尚未設定；按「新增參數」可直接用本站 OK 樣本建立，或從設定檔庫套用。"
                    : $"正在編輯 {CurrentRecipe.Name} 的第 {CurrentStationNumber:00} 站。"
                        + (!string.IsNullOrWhiteSpace(sourceName)
                            ? $" 來源：{sourceName}。"
                            : string.Empty);
                OnPropertyChanged(nameof(CurrentInspectionName));
            }
            catch (Exception ex)
            {
                CurrentProfile = null;
                StationStatusMessage = $"讀取第 {CurrentStationNumber:00} 站失敗：{ex.Message}";
            }
        }

        private void RefreshStationSlots()
        {
            var previousSlots = StationSlots.ToDictionary(slot => slot.Number);
            var slots = new ObservableCollection<StationSlotViewModel>();
            for (int number = 1; number <= StationCount; number++)
            {
                var station = CurrentRecipe?.Stations.FirstOrDefault(item => item.Number == number);
                bool configured = station?.IsConfigured == true;
                string? patternPath = null;
                string? previewOnlyPath = null;
                string allDatabasePath = recipeManager.ResolveDatabasePath(station?.AllDatabasePath);
                if (CurrentRecipe != null)
                {
                    string stationDir = Path.Combine(recipeManager.GetProfileBasePath(CurrentRecipe), RecipeManager.GetStationDirectoryName(number));
                    string profilePath = Path.Combine(stationDir, "profile.json");
                    patternPath = recipeManager.GetStationPatternPath(CurrentRecipe, number);
                    string previewOnlyDirectory = Path.Combine(stationDir, "preview-only");
                    if (Directory.Exists(previewOnlyDirectory))
                        previewOnlyPath = Directory.EnumerateFiles(previewOnlyDirectory)
                            .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".bmp")
                            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                            .FirstOrDefault();
                    if (configured) configured = File.Exists(profilePath) && patternPath != null;
                }
                Func<int, int>? saveRecentImageCount = null;
                Func<string, string>? saveRecentImageSource = null;
                if (station != null && CurrentRecipe != null)
                {
                    var recipeForSetting = CurrentRecipe;
                    var stationForSetting = station;
                    saveRecentImageCount = count => SaveStationRecentImageCount(
                        recipeForSetting, stationForSetting, count);
                    saveRecentImageSource = source => SaveStationRecentImageSource(
                        recipeForSetting, stationForSetting, source);
                }
                var slot = new StationSlotViewModel(number, configured,
                    CurrentRecipe != null && number == CurrentStationNumber,
                    patternPath,
                    recipeManager.ResolveDatabasePath(station?.OkDatabasePath),
                    recipeManager.ResolveDatabasePath(station?.NgDatabasePath),
                    station?.VendorAiModelFiles?.ToArray(),
                    station?.RecentImageCount ?? 1,
                    saveRecentImageCount,
                    station?.RecentImageSource ?? "ALL",
                    saveRecentImageSource,
                    previewOnlyPath,
                    allDatabasePath);
                if (previousSlots.TryGetValue(number, out var previous))
                {
                    slot.IncludeInBatch = previous.IncludeInBatch;
                    if (_multiStationResults.TryGetValue((CurrentRecipe?.Id ?? "", number), out var snapshot))
                        slot.ApplyBatchResult(snapshot.Ok, snapshot.Ng, snapshot.Status, snapshot.Details,
                            snapshot.Results.Count(result => result.IsRetest));
                }
                else if (_multiStationResults.TryGetValue((CurrentRecipe?.Id ?? "", number), out var saved))
                    slot.ApplyBatchResult(saved.Ok, saved.Ng, saved.Status, saved.Details,
                        saved.Results.Count(result => result.IsRetest));
                slots.Add(slot);
            }
            StationSlots = slots;
            OnPropertyChanged(nameof(RecipeConfigurationSummary));
            OnPropertyChanged(nameof(CurrentStationStatusText));
        }

        private int SaveStationRecentImageCount(
            RecipeManager.RecipeManifest recipe,
            RecipeManager.RecipeStation station,
            int recentImageCount)
        {
            int count = recentImageCount < 1 ? 1
                : recentImageCount > RecipeManager.RecipeStation.MaximumRecentImageCount
                    ? RecipeManager.RecipeStation.MaximumRecentImageCount
                    : recentImageCount;
            if (station.RecentImageCount == count) return count;

            int previous = station.RecentImageCount;
            station.RecentImageCount = count;
            try
            {
                recipeManager.SaveManifest(recipe);
                SaveSettingsMessage = $"已儲存第 {station.Number:00} 站的最新圖片測試張數：{count} 張。";
                return count;
            }
            catch (Exception ex)
            {
                station.RecentImageCount = previous;
                SaveSettingsMessage = $"儲存第 {station.Number:00} 站測試張數失敗：{ex.GetBaseException().Message}";
                return previous;
            }
        }

        private string SaveStationRecentImageSource(
            RecipeManager.RecipeManifest recipe,
            RecipeManager.RecipeStation station,
            string recentImageSource)
        {
            string source = recentImageSource is "ALL" or "OK" or "NG" ? recentImageSource : "ALL";
            if (station.RecentImageSource == source) return source;

            string previous = station.RecentImageSource;
            station.RecentImageSource = source;
            try
            {
                recipeManager.SaveManifest(recipe);
                SaveSettingsMessage = $"已儲存第 {station.Number:00} 站的最新圖片來源：{GetRecentImageSourceLabel(source)}。";
                return source;
            }
            catch (Exception ex)
            {
                station.RecentImageSource = previous;
                SaveSettingsMessage = $"儲存第 {station.Number:00} 站圖片來源失敗：{ex.GetBaseException().Message}";
                return previous;
            }
        }

        private static string GetRecentImageSourceLabel(string source) => source switch
        {
            "OK" => "OK",
            "NG" => "NG",
            _ => "全部"
        };

        [RelayCommand]
        private void ApplyGlobalRecentImageCount()
        {
            if (CurrentRecipe == null) return;

            int count = GlobalRecentImageCount < 1 ? 1
                : GlobalRecentImageCount > RecipeManager.RecipeStation.MaximumRecentImageCount
                    ? RecipeManager.RecipeStation.MaximumRecentImageCount
                    : GlobalRecentImageCount;
            GlobalRecentImageCount = count;

            var stations = CurrentRecipe.Stations
                .Where(station => station.Number <= CurrentRecipe.StationCount)
                .ToList();
            var previousCounts = stations.ToDictionary(station => station, station => station.RecentImageCount);
            foreach (var station in stations) station.RecentImageCount = count;
            try
            {
                recipeManager.SaveManifest(CurrentRecipe);
                foreach (var slot in StationSlots) slot.RecentImageCount = count;
                SaveSettingsMessage = $"已將目前 Recipe 全部 {stations.Count} 站的最新測試張數設為 {count} 張。";
            }
            catch (Exception ex)
            {
                foreach (var previous in previousCounts) previous.Key.RecentImageCount = previous.Value;
                SaveSettingsMessage = "套用全站測試張數失敗：" + ex.GetBaseException().Message;
            }
        }

        [RelayCommand]
        private void SelectDatabaseDayFolder()
        {
            if (CurrentRecipe == null)
            {
                SaveSettingsMessage = "請先載入配方，再設定各站 OK／NG 圖庫路徑。";
                return;
            }
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting)
            {
                SaveSettingsMessage = "圖片仍在檢測中，請完成後再設定圖庫路徑。";
                return;
            }

            try
            {
                // 批次設定視窗是 modal：Explorer 必須隸屬於目前對話窗，
                // 否則可能被 Recipe／圖庫視窗蓋在後面。
                Window owner = Application.Current.Windows.OfType<DatabaseSetupWindow>()
                    .FirstOrDefault(window => window.IsVisible) ?? Application.Current.MainWindow;
                string? folder = view.ExplorerFolderPicker.PickFolder(
                    owner,
                    "批次設定全部站別 · 選擇日期／站號／OK 或 NG 資料夾",
                    CurrentRecipe.DatabaseDayFolder ?? CurrentRecipe.TrainingImageRoot);
                if (folder == null) return;
                string? dayFolder = ResolveDatabaseDayFolder(Path.GetFullPath(folder));
                if (dayFolder == null)
                {
                    MessageBox.Show("找不到符合站號的上層資料夾，請選取包含 1～8（或 10）站資料夾的日期層。", "路徑結構不符",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                bool hasConfiguredPaths = CurrentRecipe.Stations.Any(station =>
                    station.Number <= CurrentRecipe.StationCount &&
                    (!string.IsNullOrWhiteSpace(station.OkDatabasePath) || !string.IsNullOrWhiteSpace(station.NgDatabasePath)));
                if (hasConfiguredPaths && MessageBox.Show(
                    $"批次設定會覆蓋目前 Recipe 全部 {CurrentRecipe.StationCount} 站的 OK／NG 路徑，包含已單獨設定的站別。\n\n新資料夾：{dayFolder}\n\n確定覆蓋嗎？",
                    "確認覆蓋所有站別路徑", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;
                ApplyDatabaseDayFolder(folder, true);
            }
            catch (Exception ex)
            {
                SaveSettingsMessage = $"圖庫設定失敗：{ex.Message}";
                MessageBox.Show(SaveSettingsMessage, "圖庫設定失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool ApplyDatabaseDayFolder(string folder, bool showMessages)
        {
            if (CurrentRecipe == null) return false;
            string selectedFolder = Path.GetFullPath(folder);
            string? dayFolder = ResolveDatabaseDayFolder(selectedFolder);
            if (dayFolder == null)
            {
                if (showMessages)
                    MessageBox.Show(
                        "此資料夾及其上層找不到站號資料夾。請選擇日期資料夾（例如 welding\\09\\03）、站號資料夾，或該站的 OK／NG 資料夾。",
                        "資料夾結構不符", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            CurrentRecipe.DatabaseDayFolder = dayFolder;
            DatabaseDayFolderDisplay = dayFolder;
            int availableStations = 0;
            int classifiedStations = 0;
            foreach (var station in CurrentRecipe.Stations.Where(item => item.Number <= CurrentRecipe.StationCount))
            {
                string stationFolder = Path.Combine(dayFolder, station.Number.ToString());
                station.OkDatabasePath = Path.Combine(stationFolder, "OK");
                station.NgDatabasePath = Path.Combine(stationFolder, "NG");
                station.AllDatabasePath = null;
                if (Directory.Exists(stationFolder)) availableStations++;
                if (HasDirectDatabaseFolders(stationFolder)) classifiedStations++;
            }

            recipeManager.SaveManifest(CurrentRecipe);
            RefreshStationDatabasePaths();
            RefreshStationSlots(); // 更新八站總覽的 OK／NG 圖庫狀態。
            DatabaseSaveMessage = $"已更新全部 {CurrentRecipe.StationCount} 站圖庫路徑。";
            if (_activeStationDatabase != null)
            {
                ResetTestImageState();
                LoadStationDatabase(_activeStationDatabase);
            }
            string resolutionNote = string.Equals(selectedFolder, dayFolder, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : $"（已從所選子資料夾辨識日期層：{dayFolder}）";
            SaveSettingsMessage = $"已為 {CurrentRecipe.Name} 的 {CurrentRecipe.StationCount} 站帶入路徑；{availableStations} 站的資料夾存在。{resolutionNote}可用主畫面 OK DB／NG DB 載入分類圖片，或按「標準件圖」載入站別設定中保存的參考圖。";
            if (showMessages)
                MessageBox.Show($"日期資料夾：{dayFolder}\n已一次設定 {CurrentRecipe.StationCount} 站路徑，其中 {availableStations} 站資料夾存在，{classifiedStations} 站有 OK 或 NG 子資料夾。\n主畫面「標準件圖」會載入站別設定時保存的參考圖片。",
                    "各站圖庫設定完成", MessageBoxButton.OK, MessageBoxImage.Information);
            return true;
        }

        private static string? ResolveDatabaseDayFolder(string selectedFolder)
        {
            if (HasStationFolders(selectedFolder)) return selectedFolder;

            var parent = Directory.GetParent(selectedFolder);
            string selectedName = Path.GetFileName(selectedFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            // The user may select <day>\\<station> instead of the <day> folder.
            if (int.TryParse(selectedName, out int stationNumber)
                && stationNumber is >= 1 and <= 10
                && parent != null
                && HasStationFolders(parent.FullName))
            {
                return parent.FullName;
            }

            // Also accept selecting the station's OK or NG folder itself.
            if ((string.Equals(selectedName, "OK", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(selectedName, "NG", StringComparison.OrdinalIgnoreCase))
                && parent != null
                && int.TryParse(Path.GetFileName(parent.FullName), out stationNumber)
                && stationNumber is >= 1 and <= 10
                && HasDirectDatabaseFolders(parent.FullName))
            {
                var dayParent = parent.Parent;
                if (dayParent != null && HasStationFolders(dayParent.FullName))
                    return dayParent.FullName;
            }

            return null;
        }

        private static bool HasStationFolders(string dayFolder)
            => Enumerable.Range(1, 10).Any(number => Directory.Exists(Path.Combine(dayFolder, number.ToString())));

        private static bool HasDirectDatabaseFolders(string stationFolder)
            => Directory.Exists(Path.Combine(stationFolder, "OK"))
                || Directory.Exists(Path.Combine(stationFolder, "NG"));

        private void RefreshStationDatabasePaths()
        {
            StationDatabasePaths = CurrentRecipe == null
                ? new ObservableCollection<StationDatabasePathViewModel>()
                : new ObservableCollection<StationDatabasePathViewModel>(CurrentRecipe.Stations
                    .Where(station => station.Number <= CurrentRecipe.StationCount)
                    .Select(station => new StationDatabasePathViewModel
                    {
                        Number = station.Number,
                        OkPath = recipeManager.ResolveDatabasePath(station.OkDatabasePath),
                        NgPath = recipeManager.ResolveDatabasePath(station.NgDatabasePath),
                        AllPath = recipeManager.ResolveDatabasePath(station.AllDatabasePath),
                        OkDirectoryExists = !string.IsNullOrWhiteSpace(station.OkDatabasePath)
                            && Directory.Exists(recipeManager.ResolveDatabasePath(station.OkDatabasePath)),
                        NgDirectoryExists = !string.IsNullOrWhiteSpace(station.NgDatabasePath)
                            && Directory.Exists(recipeManager.ResolveDatabasePath(station.NgDatabasePath)),
                        AllDirectoryExists = !string.IsNullOrWhiteSpace(station.AllDatabasePath)
                            && Directory.Exists(recipeManager.ResolveDatabasePath(station.AllDatabasePath))
                    }));
        }

        // 各站 OK／NG 可個別指定；只改對應站別與分類，不搬移或修改來源圖片。
        public void SetStationDatabaseFolder(int stationNumber, bool isOk, string selectedFolder, bool reloadActiveDatabase = true)
        {
            var recipe = CurrentRecipe ?? throw new InvalidOperationException("請先載入配方。");
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting)
                throw new InvalidOperationException("檢測進行中，請完成或停止後再調整圖庫路徑。");
            if (stationNumber < 1 || stationNumber > recipe.StationCount)
                throw new ArgumentOutOfRangeException(nameof(stationNumber), "站別不在目前 Recipe 範圍內。");
            string fullPath = Path.GetFullPath(selectedFolder);
            if (!Directory.Exists(fullPath))
                throw new DirectoryNotFoundException("資料夾不存在：" + fullPath);
            var station = recipe.Stations.Single(s => s.Number == stationNumber);
            string? previousPath = isOk ? station.OkDatabasePath : station.NgDatabasePath;
            string? previousAllPath = station.AllDatabasePath;
            string? oppositePath = isOk ? station.NgDatabasePath : station.OkDatabasePath;
            if (!string.IsNullOrWhiteSpace(oppositePath)
                && string.Equals(recipeManager.ResolveDatabasePath(oppositePath), fullPath,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("OK 與 NG 圖庫不能指向同一個資料夾。");
            string appRoot = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string savedPath = fullPath.StartsWith(appRoot, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(appRoot.Length) : fullPath;
            if (string.Equals(recipeManager.ResolveDatabasePath(previousPath), fullPath,
                StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(previousAllPath))
            {
                DatabaseSaveMessage = $"第 {stationNumber:00} 站 {(isOk ? "OK" : "NG")} 路徑未變更。";
                return;
            }
            if (isOk) station.OkDatabasePath = savedPath;
            else station.NgDatabasePath = savedPath;
            station.AllDatabasePath = null;
            try { recipeManager.SaveManifest(recipe); }
            catch
            {
                if (isOk) station.OkDatabasePath = previousPath;
                else station.NgDatabasePath = previousPath;
                station.AllDatabasePath = previousAllPath;
                throw;
            }
            RefreshStationDatabasePaths();
            RefreshStationSlots();
            if (reloadActiveDatabase && stationNumber == CurrentStationNumber && _activeStationDatabase != null &&
                (_activeStationDatabase == "ALL" ||
                 string.Equals(_activeStationDatabase, isOk ? "OK" : "NG", StringComparison.OrdinalIgnoreCase)))
            {
                ResetTestImageState();
                LoadStationDatabase(_activeStationDatabase);
            }
            DatabaseSaveMessage = $"已儲存第 {stationNumber:00} 站 {(isOk ? "OK" : "NG")} 圖庫；其餘站別及路徑保持不變。";
        }

        public async Task SetStationDatabaseFolderAsync(int stationNumber, bool isOk, string selectedFolder)
        {
            string? activeDatabase = stationNumber == CurrentStationNumber ? _activeStationDatabase : null;
            SetStationDatabaseFolder(stationNumber, isOk, selectedFolder, reloadActiveDatabase: false);
            if (activeDatabase != null && (activeDatabase == "ALL" ||
                string.Equals(activeDatabase, isOk ? "OK" : "NG", StringComparison.OrdinalIgnoreCase)))
            {
                ResetTestImageState();
                await LoadStationDatabaseAsync(activeDatabase);
            }
        }

        public void SetStationAllDatabaseFolder(int stationNumber, string selectedFolder, bool reloadActiveDatabase = true)
        {
            var recipe = CurrentRecipe ?? throw new InvalidOperationException("請先載入配方。");
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting)
                throw new InvalidOperationException("檢測進行中，請完成或停止後再調整圖庫路徑。");
            if (stationNumber < 1 || stationNumber > recipe.StationCount)
                throw new ArgumentOutOfRangeException(nameof(stationNumber), "站別不在目前 Recipe 範圍內。");

            string root = Path.GetFullPath(selectedFolder);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException("資料夾不存在：" + root);
            string leaf = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (leaf.Equals("OK", StringComparison.OrdinalIgnoreCase) || leaf.Equals("NG", StringComparison.OrdinalIgnoreCase))
            {
                string? parent = Directory.GetParent(root)?.FullName;
                string siblingCategory = leaf.Equals("OK", StringComparison.OrdinalIgnoreCase) ? "NG" : "OK";
                if (parent != null && Directory.Exists(Path.Combine(parent, siblingCategory)))
                    root = parent;
            }

            string? FindChild(string name)
            {
                if (!Directory.Exists(root)) return null;
                return Directory.EnumerateDirectories(root)
                    .FirstOrDefault(path => Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase));
            }

            string appRoot = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string SavePath(string path) => path.StartsWith(appRoot, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(appRoot.Length) : path;
            var station = recipe.Stations.Single(s => s.Number == stationNumber);
            string? previousAll = station.AllDatabasePath;
            string? previousOk = station.OkDatabasePath;
            string? previousNg = station.NgDatabasePath;
            station.AllDatabasePath = SavePath(root);
            station.OkDatabasePath = FindChild("OK") is { } ok ? SavePath(ok) : null;
            station.NgDatabasePath = FindChild("NG") is { } ng ? SavePath(ng) : null;
            try { recipeManager.SaveManifest(recipe); }
            catch
            {
                station.AllDatabasePath = previousAll;
                station.OkDatabasePath = previousOk;
                station.NgDatabasePath = previousNg;
                throw;
            }

            RefreshStationDatabasePaths();
            RefreshStationSlots();
            if (reloadActiveDatabase && stationNumber == CurrentStationNumber && _activeStationDatabase == "ALL")
            {
                ResetTestImageState();
                LoadStationDatabase("ALL");
            }
            string categories = station.OkDatabasePath != null && station.NgDatabasePath != null ? "OK／NG"
                : station.OkDatabasePath != null ? "OK"
                : station.NgDatabasePath != null ? "NG" : "無 OK／NG 子資料夾";
            DatabaseSaveMessage = $"已儲存第 {stationNumber:00} 站 ALL 圖庫：{root}；{categories}。";
        }

        public async Task SetStationAllDatabaseFolderAsync(int stationNumber, string selectedFolder)
        {
            string? activeDatabase = stationNumber == CurrentStationNumber ? _activeStationDatabase : null;
            SetStationAllDatabaseFolder(stationNumber, selectedFolder, reloadActiveDatabase: false);
            if (string.Equals(activeDatabase, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                ResetTestImageState();
                await LoadStationDatabaseAsync("ALL");
            }
        }

        private string GetProcessingProfileName() => CurrentRecipe == null
            ? CurrentProfile?.Name ?? string.Empty
            : RecipeManager.GetStationDirectoryName(CurrentStationNumber);

        private string GetProcessingProfileBasePath() => CurrentRecipe == null
            ? "profile"
            : recipeManager.GetProfileBasePath(CurrentRecipe);

        public string? GetVendorRecipeSnapshotPath()
        {
            if (CurrentRecipe == null) return null;
            string path = Path.Combine(recipeManager.GetProfileBasePath(CurrentRecipe), "vendor-source");
            return Directory.Exists(path) ? path : null;
        }

        public Task<System.Drawing.Rectangle> TestNativeLocationAsync(string imageFile)
        {
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting)
                throw new InvalidOperationException("請等待目前檢測完成。");
            string snapshot = GetVendorRecipeSnapshotPath() ?? throw new InvalidOperationException("目前配方沒有原廠定位參數。");
            return NativeLocationService.LocateAsync(snapshot, CurrentStationNumber, imageFile);
        }

        internal Task<NativeLocationService.Frame> LocateLightFrameAsync(string imageFile)
        {
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting) throw new InvalidOperationException("請等待目前檢測完成。");
            return PrepareLightFrameAsync(imageFile);
        }

        private string LightSettingsPath() => CurrentRecipe == null ? throw new InvalidOperationException("請先載入配方。")
            : LightSettingsPath(CurrentRecipe, CurrentStationNumber);

        private string LightSettingsPath(RecipeManager.RecipeManifest recipe, int stationNumber) =>
            Path.Combine(recipeManager.GetProfileBasePath(recipe), RecipeManager.GetStationDirectoryName(stationNumber), "light-inspection.json");

        public LightInspectionSettings LoadLightSettings()
            => CurrentRecipe == null ? throw new InvalidOperationException("請先載入配方。")
                : LoadLightSettings(CurrentRecipe, CurrentStationNumber);

        internal LightInspectionSettings LoadLightSettings(RecipeManager.RecipeManifest recipe, int stationNumber)
        {
            string path = LightSettingsPath(recipe, stationNumber);
            bool hasFile = File.Exists(path);
            string? json = hasFile ? File.ReadAllText(path) : null;
            var settings = hasFile ? System.Text.Json.JsonSerializer.Deserialize<LightInspectionSettings>(json!) ?? throw new InvalidDataException("光源設定無法讀取。") : new LightInspectionSettings();
            bool hasSavedLimitMode = false;
            if (json != null)
            {
                using var document = System.Text.Json.JsonDocument.Parse(json);
                hasSavedLimitMode = document.RootElement.TryGetProperty("LimitMode", out _);
            }

            if (hasFile && !hasSavedLimitMode)
            {
                // Older light settings stored only absolute limits. The new default is percentage-based;
                // recover its baseline and percentages from this station's recipe profile.
                var profile = CurrentRecipe?.Id == recipe.Id && CurrentStationNumber == stationNumber
                    ? CurrentProfile
                    : recipeManager.LoadStation(recipe, stationNumber);
                settings.LimitMode = "百分比偏差";
                if (settings.BaseValue <= 0 && profile != null)
                    settings.BaseValue = profile.BrightnessBaseValue ?? profile.BrightnessMean ?? GetMiddleValue(profile.BrightnessMin, profile.BrightnessMax);
                if (profile != null)
                {
                    settings.PercentLower = profile.BrightnessPercentLower == 0 ? 10 : profile.BrightnessPercentLower;
                    settings.PercentUpper = profile.BrightnessPercentUpper == 0 ? 10 : profile.BrightnessPercentUpper;
                }
            }
            if (!File.Exists(path) && CurrentRecipe?.Id == recipe.Id && CurrentStationNumber == stationNumber && CurrentProfile != null)
            {
                settings.BaseValue = BrightnessBaseValue;
                settings.LimitMode = "百分比偏差";
                settings.Minimum = Math.Max(0, BrightnessFixedMin); settings.Maximum = Math.Min(255, BrightnessFixedMax);
                settings.PercentLower = BrightnessPercentLower; settings.PercentUpper = BrightnessPercentUpper;
            }
            settings.Validate(); return settings;
        }

        public void SaveLightSettings(LightInspectionSettings settings)
        {
            settings.Validate();
            if (!settings.HasRoi) throw new InvalidOperationException("請先設定光源 ROI。");
            string path = LightSettingsPath(); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            LightSampleHistory.WriteAtomic(path, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(settings,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }

        public string LightSampleHistoryDirectory => CurrentRecipe == null
            ? throw new InvalidOperationException("請先載入配方。")
            : LightSampleHistory.Folder(recipeManager.GetProfileBasePath(CurrentRecipe), CurrentStationNumber);

        public string? GetCurrentStationPatternPath() => CurrentRecipe == null ? null
            : recipeManager.GetStationPatternPath(CurrentRecipe, CurrentStationNumber);

        public async Task<string> SetCurrentImageAsLightStandardAsync()
        {
            var recipe = CurrentRecipe ?? throw new InvalidOperationException("請先載入配方。");
            int station = CurrentStationNumber;
            string image = CurrentTestImagePath;
            if (string.IsNullOrWhiteSpace(image) || !File.Exists(image))
                throw new FileNotFoundException("請先選擇本站要設為標準件的圖片。", image);
            var settings = LoadLightSettings();
            var frame = await LocateLightFrameAsync(image);
            if (CurrentRecipe?.Id != recipe.Id || CurrentStationNumber != station || CurrentTestImagePath != image)
                throw new InvalidOperationException("配方、站別或圖片已變更，未設定標準件。");
            bool initialized = settings.EnsureEditableRoi(frame.Bounds);
            settings.BaseValue = LightBrightnessMeasurement.Measure(frame, settings);
            settings.EffectiveLimits();
            string saved = SaveLightStandardPart(image, settings);
            SaveSettingsMessage = $"「{recipe.Name}」第 {station} 站已設為標準件：{Path.GetFileName(image)}；基準亮度 {settings.BaseValue:F2}。"
                + (initialized ? "已依定位尺寸初始化 ROI。" : "") + saved;
            return SaveSettingsMessage;
        }

        public string SaveLightStandardPart(string imagePath, LightInspectionSettings settings)
        {
            if (CurrentRecipe == null) throw new InvalidOperationException("請先載入配方。");
            var sample = LightSampleHistory.Capture(CurrentRecipe.Id, CurrentStationNumber, imagePath, settings);
            bool created = false;
            ApplyLightSampleTransaction(sample, () => created = LightSampleHistory.Save(LightSampleHistoryDirectory, sample, ApplyLightSample));
            return (created ? "標準件與參數已儲存，已建立樣本還原點（每站保留最近 3 份）。" : "標準件與參數已儲存；與最近還原點相同，未重複建立。")
                + LightSampleHistory.Prune(LightSampleHistoryDirectory);
        }

        public LightInspectionSettings RestoreLightStandardPart(string archive)
        {
            if (CurrentRecipe == null) throw new InvalidOperationException("請先載入配方。");
            var sample = LightSampleHistory.Read(archive, CurrentRecipe.Id, CurrentStationNumber);
            ApplyLightSampleTransaction(sample, () => LightSampleHistory.Apply(sample, LightSampleHistoryDirectory, ApplyLightSample));
            return sample.Settings;
        }

        private void ApplyLightSample(LightSampleHistory.Sample sample, string imagePath)
        {
            // Decode before any write; corrupt image data cannot replace a valid standard.
            ProfileManager.LoadBitmapFromPath(imagePath);
            SaveStationStandardPartImage(imagePath);
            SaveLightSettings(sample.Settings);
        }

        private void ApplyLightSampleTransaction(LightSampleHistory.Sample sample, Action action)
        {
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting)
                throw new InvalidOperationException("檢測進行中不能更新標準件。");
            var recipe = CurrentRecipe!;
            var station = recipe.Stations.Single(s => s.Number == CurrentStationNumber);
            string root = recipeManager.GetProfileBasePath(recipe);
            string directory = Path.Combine(root, RecipeManager.GetStationDirectoryName(CurrentStationNumber));
            string? oldName = station.PatternFileName;
            bool oldConfigured = station.IsConfigured;
            string oldUpdatedAt = recipe.UpdatedAt;
            var previousProfile = CurrentProfile;
            var previousImagePath = previousProfile?.ImagePath;
            var previousBitmap = previousProfile?.Bitmap;
            var previousProfileName = previousProfile?.Name;
            var previousWidth = previousProfile?.ImageWidth;
            var previousHeight = previousProfile?.ImageHeight;
            string activeImage = Path.Combine(directory, "_light-standard" + Path.GetExtension(sample.OriginalName).ToLowerInvariant());
            try
            {
                LightSampleHistory.FileTransaction(new[] { activeImage, Path.Combine(directory, "profile.json"),
                    LightSettingsPath(), Path.Combine(root, "recipe.json") }, action);
            }
            catch
            {
                station.PatternFileName = oldName;
                station.IsConfigured = oldConfigured;
                recipe.UpdatedAt = oldUpdatedAt;
                if (previousProfile != null)
                {
                    previousProfile.ImagePath = previousImagePath!;
                    previousProfile.Bitmap = previousBitmap!;
                    previousProfile.Name = previousProfileName!;
                    previousProfile.ImageWidth = previousWidth!.Value;
                    previousProfile.ImageHeight = previousHeight!.Value;
                    LoadCurrentProfile(previousProfile, isLibraryProfile: false);
                }
                RefreshStationSlots();
                throw;
            }
            // Only remove images owned by this feature, never imported recipe/training files.
            foreach (string previous in Directory.GetFiles(directory, "_light-standard.*"))
                if (!string.Equals(previous, activeImage, StringComparison.OrdinalIgnoreCase) &&
                    new[] { ".png", ".bmp", ".jpg", ".jpeg", ".tif", ".tiff" }.Contains(Path.GetExtension(previous).ToLowerInvariant()))
                    try { File.Delete(previous); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        private ImageProcessor CreateImageProcessor() => new(GetProcessingProfileBasePath());

        [ObservableProperty]
        private double testImageBrightness = 0;
        [ObservableProperty]
        private double testImageBrightnessDeltaPercent = double.NaN;
        [ObservableProperty]
        private double testImageSharpness = 0;
        [ObservableProperty]
        private System.Drawing.Rectangle testImageROI;
        [ObservableProperty]
        private System.Drawing.Rectangle testImageBrightnessROI;
        [ObservableProperty]
        private System.Drawing.Rectangle testImageSharpnessROI;

        // 測試圖片資料夾相關屬性
        private readonly List<string> _allLoadedTestImageFiles = new();
        private readonly Dictionary<string, string> _imageReferenceClasses = new(StringComparer.OrdinalIgnoreCase);
        private string _resultListFilter = "ALL";
        [ObservableProperty]
        private ObservableCollection<string> testImageFiles = new();
        [ObservableProperty]
        private ObservableCollection<TestImageTreeNode> testImageTree = new();
        [ObservableProperty]
        private int currentTestImageIndex = -1;
        [ObservableProperty]
        private string currentTestImagePath = string.Empty;
        [ObservableProperty]
        private string testImageInfo = "圖片 0 / 0";
        [ObservableProperty]
        private bool canNavigatePrevious = false;
        [ObservableProperty]
        private bool canNavigateNext = false;
        [ObservableProperty]
        private BitmapImage currentTestImageBitmap;
        [ObservableProperty]
        private BitmapImage displayImage;
        [ObservableProperty]
        private string testImageMatchFailed = string.Empty;
        [ObservableProperty]
        private ObservableCollection<TestImageResultRow> testImageResults = new();
        [ObservableProperty]
        private int totalTestImageCount;
        [ObservableProperty]
        private int brightnessOkCount;
        [ObservableProperty]
        private int brightnessNgCount;
        [ObservableProperty]
        private int sharpnessOkCount;
        [ObservableProperty]
        private int sharpnessNgCount;
        [ObservableProperty]
        private int overallOkCount;
        [ObservableProperty]
        private int overallNgCount;
        [ObservableProperty]
        private int overallRetestCount;
        [ObservableProperty]
        private string overallOkRateText = "0.00%";
        [ObservableProperty]
        private string overallNgRateText = "0.00%";
        [ObservableProperty]
        private bool isAnalyzingTestImages;
        [ObservableProperty]
        private bool isContinuousTestRunning;
        [ObservableProperty]
        private bool isTestImageAnalysisComplete;
        [ObservableProperty]
        private int analysisProgressValue;
        [ObservableProperty]
        private int analysisProgressMaximum = 1;
        [ObservableProperty]
        private string analysisProgressText = string.Empty;

        [ObservableProperty]
        private string brightnessLimitMode = ProfileManager.LimitMode.Percent;
        [ObservableProperty]
        private double brightnessBaseValue;
        [ObservableProperty]
        private double brightnessPercentLower = 10;
        [ObservableProperty]
        private double brightnessPercentUpper = 10;
        [ObservableProperty]
        private double brightnessFixedMin;
        [ObservableProperty]
        private double brightnessFixedMax;
        [ObservableProperty]
        private string sharpnessLimitMode = ProfileManager.LimitMode.Percent;
        [ObservableProperty]
        private string detectionMode = ProfileManager.InspectionMode.Brightness;
        [ObservableProperty]
        private bool showRoiPanel = false;
        [ObservableProperty]
        private bool showOnlyNgThumbnails = false;
        [ObservableProperty]
        private string batchPreviewMode = BatchPreviewModes.NgOnly;
        [ObservableProperty]
        private double selectedBaselinePercentile = 2.5;
        [ObservableProperty]
        private double sharpnessBaseValue;
        [ObservableProperty]
        private double sharpnessPercentLower = 10;
        [ObservableProperty]
        private double sharpnessFixedMin;
        [ObservableProperty]
        private double effectiveBrightnessMin;
        [ObservableProperty]
        private double effectiveBrightnessMax;
        [ObservableProperty]
        private double effectiveSharpnessMin;
        [ObservableProperty]
        private string saveSettingsMessage = string.Empty;

        public string TestImageBrightnessDeltaPercentText => double.IsNaN(TestImageBrightnessDeltaPercent)
            ? "N/A"
            : $"{TestImageBrightnessDeltaPercent:+0.00;-0.00;0.00}%";

        partial void OnBrightnessLimitModeChanged(string value) => RecalculateThresholdPreview();
        partial void OnBrightnessBaseValueChanged(double value)
        {
            RecalculateThresholdPreview();
            if (!double.IsNaN(TestImageBrightnessDeltaPercent) || Math.Abs(TestImageBrightness) > double.Epsilon)
            {
                TestImageBrightnessDeltaPercent = CalculateBrightnessDeltaPercent(TestImageBrightness, value);
            }
        }
        partial void OnBrightnessPercentLowerChanged(double value) => RecalculateThresholdPreview();
        partial void OnBrightnessPercentUpperChanged(double value) => RecalculateThresholdPreview();
        partial void OnBrightnessFixedMinChanged(double value) => RecalculateThresholdPreview();
        partial void OnBrightnessFixedMaxChanged(double value) => RecalculateThresholdPreview();
        partial void OnTestImageBrightnessDeltaPercentChanged(double value) => OnPropertyChanged(nameof(TestImageBrightnessDeltaPercentText));
        partial void OnSharpnessLimitModeChanged(string value) => RecalculateThresholdPreview();
        partial void OnSharpnessBaseValueChanged(double value) => RecalculateThresholdPreview();
        partial void OnSharpnessPercentLowerChanged(double value) => RecalculateThresholdPreview();
        partial void OnSharpnessFixedMinChanged(double value) => RecalculateThresholdPreview();
        partial void OnDetectionModeChanged(string value)
        {
            OnPropertyChanged(nameof(IsSharpnessEnabled));
            OnPropertyChanged(nameof(IsBrightnessOnlyMode));
            OnPropertyChanged(nameof(IsSharpnessPercentMode));
            OnPropertyChanged(nameof(IsSharpnessFixedMode));
            NotifyBaselineSummaryChanged();
            if (IsTestImageAnalysisComplete && _allLoadedTestImageFiles.Count > 0)
            {
                _ = ReanalyzeLoadedTestImagesAsync();
            }
        }
        partial void OnShowRoiPanelChanged(bool value) => OnPropertyChanged(nameof(IsRoiPanelVisible));
        partial void OnBatchPreviewModeChanged(string value) => OnPropertyChanged(nameof(ResultsThumbnailMode));
        partial void OnSelectedBaselinePercentileChanged(double value)
        {
            OnPropertyChanged(nameof(BrightnessPercentileSummary));
            OnPropertyChanged(nameof(SharpnessPercentileSummary));
        }

        private void LoadInspectionSettings(ProfileData profile)
        {
            BrightnessLimitMode = string.IsNullOrWhiteSpace(profile.BrightnessLimitMode) ? ProfileManager.LimitMode.Percent : profile.BrightnessLimitMode;
            BrightnessBaseValue = profile.BrightnessBaseValue ?? profile.BrightnessMean ?? GetMiddleValue(profile.BrightnessMin, profile.BrightnessMax);
            BrightnessPercentLower = profile.BrightnessPercentLower == 0 ? 10 : profile.BrightnessPercentLower;
            BrightnessPercentUpper = profile.BrightnessPercentUpper == 0 ? 10 : profile.BrightnessPercentUpper;
            BrightnessFixedMin = profile.BrightnessFixedMin ?? profile.BrightnessMin ?? 0;
            BrightnessFixedMax = profile.BrightnessFixedMax ?? profile.BrightnessMax ?? 0;
            DetectionMode = string.IsNullOrWhiteSpace(profile.DetectionMode) ? ProfileManager.InspectionMode.Brightness : profile.DetectionMode;
            SharpnessLimitMode = string.IsNullOrWhiteSpace(profile.SharpnessLimitMode) ? ProfileManager.LimitMode.Percent : profile.SharpnessLimitMode;
            SharpnessBaseValue = profile.SharpnessBaseValue ?? profile.SharpnessMean ?? profile.SharpnessMin ?? 0;
            SharpnessPercentLower = profile.SharpnessPercentLower == 0 ? 10 : profile.SharpnessPercentLower;
            SharpnessFixedMin = profile.SharpnessFixedMin ?? profile.SharpnessMin ?? 0;
            SaveSettingsMessage = string.Empty;
            RecalculateThresholdPreview();
        }

        private static double GetMiddleValue(double? min, double? max)
        {
            if (min.HasValue && max.HasValue) return (min.Value + max.Value) / 2.0;
            return min ?? max ?? 0;
        }

        private void RecalculateThresholdPreview()
        {
            if (BrightnessLimitMode == ProfileManager.LimitMode.Fixed)
            {
                EffectiveBrightnessMin = BrightnessFixedMin;
                EffectiveBrightnessMax = BrightnessFixedMax;
            }
            else
            {
                EffectiveBrightnessMin = BrightnessBaseValue * (1 - BrightnessPercentLower / 100.0);
                EffectiveBrightnessMax = BrightnessBaseValue * (1 + BrightnessPercentUpper / 100.0);
            }

            EffectiveSharpnessMin = SharpnessLimitMode == ProfileManager.LimitMode.Fixed
                ? SharpnessFixedMin
                : SharpnessBaseValue * (1 - SharpnessPercentLower / 100.0);

            OnPropertyChanged(nameof(IsBrightnessPercentMode));
            OnPropertyChanged(nameof(IsBrightnessFixedMode));
            OnPropertyChanged(nameof(IsSharpnessPercentMode));
            OnPropertyChanged(nameof(IsSharpnessFixedMode));
            OnPropertyChanged(nameof(IsSharpnessEnabled));
            OnPropertyChanged(nameof(IsBrightnessOnlyMode));
        }

        public bool IsBrightnessPercentMode => BrightnessLimitMode == ProfileManager.LimitMode.Percent;
        public bool IsBrightnessFixedMode => BrightnessLimitMode == ProfileManager.LimitMode.Fixed;
        public bool IsSharpnessPercentMode => SharpnessLimitMode == ProfileManager.LimitMode.Percent;
        public bool IsSharpnessFixedMode => SharpnessLimitMode == ProfileManager.LimitMode.Fixed;
        public bool IsSharpnessEnabled => DetectionMode == ProfileManager.InspectionMode.BrightnessSharpness;
        public bool IsRecipeActive => CurrentRecipe != null;
        // 全域上方工具列直接讀取目前載入的 Recipe／站別，避免只顯示尚未套用的選單項目。
        public string RecipeConfigurationSummary
        {
            get
            {
                if (CurrentRecipe == null) return "未選擇";
                int configuredStations = StationSlots.Count(slot => slot.IsConfigured);
                string? dayFolder = CurrentRecipe.DatabaseDayFolder;
                return string.IsNullOrWhiteSpace(dayFolder)
                    ? $"已設定 {configuredStations}/{StationCount} 站"
                    : $"圖庫 {GetSharedReportDayLabel(dayFolder!)} · 已設定 {configuredStations}/{StationCount} 站";
            }
        }
        public string CurrentStationStatusText => CurrentRecipe == null
            ? "—" : $"{CurrentStationNumber} · {(StationSlots.FirstOrDefault(slot => slot.Number == CurrentStationNumber)?.IsConfigured == true ? "已設定" : "待校準")}";
        public string GlobalRunStatusText => CurrentRecipe == null
            ? "請先選擇 Recipe"
            : IsMultiStationTesting ? MultiStationProgress
            : IsAnalyzingTestImages || IsContinuousTestRunning
                ? (string.IsNullOrWhiteSpace(AnalysisProgressText) ? "單站檢測中" : AnalysisProgressText)
                : IsTestImageAnalysisComplete && TotalTestImageCount > 0
                    ? $"單站已判定 {TotalTestImageCount:N0} 張"
                    : MultiStationProgress;
        public System.Windows.Media.Brush GlobalStatusBrush => CurrentRecipe == null
            ? System.Windows.Media.Brushes.DarkOrange
            : IsMultiStationTesting || IsAnalyzingTestImages || IsContinuousTestRunning
                ? System.Windows.Media.Brushes.DeepSkyBlue
            : IsTestImageAnalysisComplete && TotalTestImageCount > 0
                ? System.Windows.Media.Brushes.MediumSeaGreen
            : MultiStationProgress.StartsWith("檢測完成", StringComparison.Ordinal)
                ? System.Windows.Media.Brushes.MediumSeaGreen
                : MultiStationProgress.StartsWith("多站測試中斷", StringComparison.Ordinal)
                    ? System.Windows.Media.Brushes.IndianRed
                    : System.Windows.Media.Brushes.SlateGray;
        public bool HasCurrentProfile => CurrentProfile != null;
        public bool HasSelectedRecipe => SelectedRecipe != null;
        public bool HasSelectedProfile => SelectedItem != null;
        public bool HasSelectedRecipeTemplate => SelectedRecipeTemplate != null;
        public bool CanApplyLibraryProfileToStation => CurrentRecipe != null && SelectedRecipeTemplate != null;
        public bool CanEditRoi => CurrentProfile != null && DisplayImage != null;
        public string CurrentInspectionName => CurrentRecipe == null
            ? CurrentProfile?.Name ?? "未選擇設定檔"
            : $"{CurrentRecipe.Name} / 第 {CurrentStationNumber:00} 站";
        public string CurrentStationSettingsLabel
        {
            get
            {
                if (CurrentRecipe == null) return "請先載入 Recipe";
                var station = CurrentRecipe.Stations.FirstOrDefault(item => item.Number == CurrentStationNumber);
                if (station?.IsConfigured != true) return "尚未設定";
                if (CurrentProfile == null) return "設定檔無法讀取";
                return string.IsNullOrWhiteSpace(station.SourceProfileName)
                    ? "Recipe 專屬設定"
                    : station.SourceProfileName;
            }
        }
        public bool IsBrightnessOnlyMode => !IsSharpnessEnabled;
        public bool IsRoiPanelVisible => ShowRoiPanel;
        public string ResultsThumbnailMode => BatchPreviewMode == BatchPreviewModes.None
            ? BatchPreviewModes.None
            : (ShowOnlyNgThumbnails || BatchPreviewMode == BatchPreviewModes.NgOnly ? BatchPreviewModes.NgOnly : BatchPreviewModes.All);
        public string ImageSizeSummary => CurrentProfile == null ? "圖片尺寸: -" : $"圖片尺寸: {CurrentProfile.ImageWidth:F0} x {CurrentProfile.ImageHeight:F0} px";
        public string BrightnessCvSummary => $"亮度變異係數: {FormatCv(CurrentProfile?.BrightnessMean, CurrentProfile?.BrightnessSigma)}";
        public string SharpnessCvSummary => $"銳利度變異係數: {FormatCv(CurrentProfile?.SharpnessMean, CurrentProfile?.SharpnessSigma)}";
        public string BrightnessPercentileSummary => $"亮度 P{SelectedBaselinePercentile:F1}~P{100 - SelectedBaselinePercentile:F1}: {FormatPercentileRange(CurrentProfile?.BrightnessValues, CurrentProfile?.BrightnessMean, CurrentProfile?.BrightnessSigma, CurrentProfile?.BrightnessPercentileLower, CurrentProfile?.BrightnessPercentileUpper, SelectedBaselinePercentile)}";
        public string SharpnessPercentileSummary => $"銳利度 P{SelectedBaselinePercentile:F1}~P{100 - SelectedBaselinePercentile:F1}: {FormatPercentileRange(CurrentProfile?.SharpnessValues, CurrentProfile?.SharpnessMean, CurrentProfile?.SharpnessSigma, CurrentProfile?.SharpnessPercentileLower, CurrentProfile?.SharpnessPercentileUpper, SelectedBaselinePercentile)}";
        public string BrightnessRoiAreaSummary => $"亮度 ROI 面積: {FormatArea(CurrentProfile?.EffectiveBrightnessRoiW, CurrentProfile?.EffectiveBrightnessRoiH)}";
        public string SharpnessRoiAreaSummary => $"銳利度 ROI 面積: {FormatArea(CurrentProfile?.EffectiveSharpnessRoiW, CurrentProfile?.EffectiveSharpnessRoiH)}";

        partial void OnCurrentProfileChanged(ProfileData? value)
        {
            NotifyBaselineSummaryChanged();
            OnPropertyChanged(nameof(HasCurrentProfile));
            OnPropertyChanged(nameof(CurrentStationSettingsLabel));
            OnPropertyChanged(nameof(CanEditRoi));
            if (value == null) IsRoiEditing = false;
        }

        partial void OnSelectedItemChanged(ProfileData? value)
        {
            OnPropertyChanged(nameof(HasSelectedProfile));
        }

        partial void OnSelectedRecipeTemplateChanged(ProfileData? value)
        {
            OnPropertyChanged(nameof(HasSelectedRecipeTemplate));
            OnPropertyChanged(nameof(CanApplyLibraryProfileToStation));
        }

        partial void OnDisplayImageChanged(BitmapImage value)
        {
            OnPropertyChanged(nameof(CanEditRoi));
            if (value == null) IsRoiEditing = false;
        }

        partial void OnSelectedRecipeChanged(RecipeManager.RecipeManifest? value) => OnPropertyChanged(nameof(HasSelectedRecipe));

        partial void OnCurrentRecipeChanged(RecipeManager.RecipeManifest? value)
        {
            MultiStationProgress = "尚未執行檢測";
            MultiStationTotal = "判定 OK —   NG —   Retest —   良率 —";
            DatabaseSaveMessage = "選取後自動儲存";
            OnPropertyChanged(nameof(IsRecipeActive));
            OnPropertyChanged(nameof(GlobalStatusBrush));
            OnPropertyChanged(nameof(GlobalRunStatusText));
            OnPropertyChanged(nameof(CurrentInspectionName));
            OnPropertyChanged(nameof(CurrentStationSettingsLabel));
            RefreshStationSlots();
            GlobalRecentImageCount = value?.Stations.FirstOrDefault(station => station.Number == 1)?.RecentImageCount ?? 1;
            RefreshStationDatabasePaths();
            DatabaseDayFolderDisplay = value?.DatabaseDayFolder ?? value?.TrainingImageRoot ?? string.Empty;
            InspectionImageOutputFolderDisplay = value?.InspectionImageOutputFolder ?? string.Empty;
        }

        partial void OnStationCountChanged(int value)
        {
            if (value is not (8 or 10)) return;
            if (!_isInitializing)
            {
                if (CurrentRecipe != null && !_isLoadingRecipe && CurrentProfile != null)
                    SaveCurrentInspectionSettings(reanalyzeTestImages: false);
                _preferences.StationCount = value;
                recipeManager.SavePreferences(_preferences);
                if (CurrentRecipe != null && !_isLoadingRecipe)
                {
                    CurrentRecipe.StationCount = value;
                    recipeManager.SaveManifest(CurrentRecipe);
                    if (CurrentStationNumber > value)
                    {
                        CurrentStationNumber = 1;
                        LoadRecipeStation();
                    }
                }
            }
            RefreshStationSlots();
            RefreshStationDatabasePaths();
            OnPropertyChanged(nameof(CurrentInspectionName));
        }

        partial void OnCurrentStationNumberChanged(int value)
        {
            OnPropertyChanged(nameof(CurrentInspectionName));
            OnPropertyChanged(nameof(CurrentStationSettingsLabel));
            OnPropertyChanged(nameof(CurrentStationStatusText));
        }

        partial void OnIsAnalyzingTestImagesChanged(bool value)
        {
            OnPropertyChanged(nameof(GlobalRunStatusText));
            OnPropertyChanged(nameof(GlobalStatusBrush));
        }
        partial void OnIsContinuousTestRunningChanged(bool value)
        {
            OnPropertyChanged(nameof(GlobalRunStatusText));
            OnPropertyChanged(nameof(GlobalStatusBrush));
        }
        partial void OnIsTestImageAnalysisCompleteChanged(bool value)
        {
            OnPropertyChanged(nameof(GlobalRunStatusText));
            OnPropertyChanged(nameof(GlobalStatusBrush));
        }
        partial void OnAnalysisProgressTextChanged(string value)
            => OnPropertyChanged(nameof(GlobalRunStatusText));
        partial void OnTotalTestImageCountChanged(int value)
            => OnPropertyChanged(nameof(GlobalRunStatusText));

        private void NotifyBaselineSummaryChanged()
        {
            OnPropertyChanged(nameof(ImageSizeSummary));
            OnPropertyChanged(nameof(BrightnessCvSummary));
            OnPropertyChanged(nameof(SharpnessCvSummary));
            OnPropertyChanged(nameof(BrightnessPercentileSummary));
            OnPropertyChanged(nameof(SharpnessPercentileSummary));
            OnPropertyChanged(nameof(BrightnessRoiAreaSummary));
            OnPropertyChanged(nameof(SharpnessRoiAreaSummary));
        }

        private static string FormatPercentileRange(IReadOnlyCollection<double>? values, double? mean, double? sigma, double? storedLower, double? storedUpper, double percentile)
        {
            if (values != null && values.Count > 0)
            {
                var sorted = values.OrderBy(value => value).ToArray();
                double lower = Percentile(sorted, percentile);
                double upper = Percentile(sorted, 100 - percentile);
                return $"{lower:F2} ~ {upper:F2}";
            }

            if (Math.Abs(percentile - 2.5) < 0.001 && storedLower.HasValue && storedUpper.HasValue)
            {
                return $"{storedLower.Value:F2} ~ {storedUpper.Value:F2}";
            }

            if (mean.HasValue && sigma.HasValue)
            {
                double z = percentile <= 1.0 ? 2.3263 : percentile <= 2.5 ? 1.9600 : 1.6449;
                return $"{mean.Value - z * sigma.Value:F2} ~ {mean.Value + z * sigma.Value:F2}";
            }

            return "-";
        }

        private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0)
            {
                return 0;
            }

            double index = (sortedValues.Count - 1) * percentile / 100.0;
            int lower = (int)Math.Floor(index);
            int upper = (int)Math.Ceiling(index);
            if (lower == upper)
            {
                return sortedValues[lower];
            }

            double fraction = index - lower;
            return sortedValues[lower] + fraction * (sortedValues[upper] - sortedValues[lower]);
        }

        private static string FormatCv(double? mean, double? sigma)
        {
            if (!mean.HasValue || Math.Abs(mean.Value) < double.Epsilon || !sigma.HasValue)
            {
                return "-";
            }

            return $"{sigma.Value / mean.Value * 100.0:F2}%";
        }

        private static double CalculateBrightnessDeltaPercent(double brightness, double brightnessBaseValue)
        {
            if (Math.Abs(brightnessBaseValue) < double.Epsilon)
            {
                return double.NaN;
            }

            return (brightness - brightnessBaseValue) / brightnessBaseValue * 100.0;
        }

        private static string FormatArea(int? width, int? height)
        {
            int area = width.GetValueOrDefault() * height.GetValueOrDefault();
            return area > 0 ? $"{area:N0} px" : "-";
        }

        [RelayCommand]
        private void SaveInspectionSettings()
        {
            if (IsMultiStationTesting)
            {
                SaveSettingsMessage = "多站測試進行中，請完成後再儲存參數。";
                return;
            }
            SaveCurrentInspectionSettings(reanalyzeTestImages: true);
        }

        [RelayCommand]
        private void OpenMultiAppearanceCalibrationPreview()
        {
            var samples = new List<string>();
            var station = CurrentRecipe?.Stations.FirstOrDefault(item => item.Number == CurrentStationNumber);
            if (station != null)
            {
                string folder = recipeManager.ResolveDatabasePath(station.OkDatabasePath);
                if (Directory.Exists(folder)) samples = EnumerateOkImages(folder).Take(100).ToList();
            }

            var window = new MultiAppearanceCalibrationPreviewWindow(Application.Current.MainWindow, samples);
            window.ShowDialog();
        }

        [RelayCommand]
        private void CreateStationParameters()
        {
            if (IsMultiStationTesting)
            {
                StationStatusMessage = "多站測試進行中，請完成後再新增參數。";
                return;
            }
            if (CurrentRecipe == null)
            {
                StationStatusMessage = "請先載入配方，再選擇站別新增參數。";
                return;
            }
            if (IsAnalyzingTestImages)
            {
                StationStatusMessage = "正在檢測圖片，請完成後再新增參數。";
                return;
            }
            int stationNumber = CurrentStationNumber;
            var recipe = CurrentRecipe;
            var station = recipe.Stations.Single(item => item.Number == stationNumber);
            string stationFolder = Path.Combine(recipeManager.GetProfileBasePath(recipe),
                RecipeManager.GetStationDirectoryName(stationNumber));
            if (CurrentProfile != null || station.IsConfigured
                || File.Exists(Path.Combine(stationFolder, "profile.json"))
                || recipeManager.GetStationPatternPath(recipe, stationNumber) != null)
            {
                MessageBox.Show($"第 {stationNumber:00} 站已有參數，請使用右側「重設參數」修改，避免覆蓋原設定。",
                    "本站已有設定", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 已設定且至少有一張 OK 圖片就直接使用，不額外要求選資料夾。
            List<string> files;
            try
            {
                files = ResolveStationOkSamples(stationNumber, station.OkDatabasePath,
                    isReset: false, out string selectedFolder);
                if (files.Count > 0) SaveChosenOkFolderIfChanged(recipe, stationNumber, selectedFolder);
            }
            catch (Exception ex)
            {
                StationStatusMessage = "讀取本站 OK 樣本失敗：" + ex.Message;
                MessageBox.Show(StationStatusMessage, "新增參數", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (files.Count == 0) return;

            // 延用現有的 ROI + 多樣本基準計算視窗。只有完成計算並確認結果才會寫入本站。
            SelectedFiles.Clear();
            foreach (string file in files) SelectedFiles.Add(file);
            StationStatusMessage = $"第 {stationNumber:00} 站使用 {files.Count} 張 OK 樣本建立參數；請在視窗畫定位與亮度 ROI，確認計算結果後儲存。";
            try
            {
                _windowService.ShowProfileAddWindow(files, DetectionMode, new ProfileData(),
                    (profile, patternImage) => SaveNewStationParameters(recipe.Id, stationNumber, profile, patternImage));
                if (!station.IsConfigured)
                    StationStatusMessage = $"第 {stationNumber:00} 站尚未建立參數；已取消，不變更原有圖庫。";
            }
            catch (Exception ex)
            {
                StationStatusMessage = "新增本站參數失敗：" + ex.Message;
                MessageBox.Show(StationStatusMessage, "新增參數", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveNewStationParameters(string expectedRecipeId, int expectedStation,
            ProfileData profile, string patternImage)
        {
            if (CurrentRecipe == null || CurrentRecipe.Id != expectedRecipeId
                || CurrentStationNumber != expectedStation)
                throw new InvalidOperationException("目前配方或站別已變更，取消儲存新增參數。");
            var station = CurrentRecipe.Stations.Single(item => item.Number == expectedStation);
            var stationFolder = Path.Combine(recipeManager.GetProfileBasePath(CurrentRecipe),
                RecipeManager.GetStationDirectoryName(expectedStation));
            if (CurrentProfile != null || station.IsConfigured
                || File.Exists(Path.Combine(stationFolder, "profile.json"))
                || recipeManager.GetStationPatternPath(CurrentRecipe, expectedStation) != null)
                throw new InvalidOperationException("本站已存在設定檔，不可用新增參數覆寫。");
            if (!File.Exists(patternImage) || profile.PatternMatchRoiW.GetValueOrDefault() <= 0
                || profile.PatternMatchRoiH.GetValueOrDefault() <= 0
                || profile.EffectiveBrightnessRoiW.GetValueOrDefault() <= 0
                || profile.EffectiveBrightnessRoiH.GetValueOrDefault() <= 0)
                throw new InvalidOperationException("基準圖或 ROI 不完整，請先畫好定位與亮度檢測框。");

            profile.ImagePath = patternImage;
            recipeManager.SaveStation(CurrentRecipe, expectedStation, profile);
            var saved = recipeManager.LoadStation(CurrentRecipe, expectedStation)
                ?? throw new InvalidOperationException("參數已寫入但無法重新讀取，請檢查本站檔案。");
            LoadCurrentProfile(saved, isLibraryProfile: false);
            RefreshStationSlots();
            StationStatusMessage = $"第 {expectedStation:00} 站已新增參數並保存到配方。";
            SaveSettingsMessage = $"已使用 {SelectedFiles.Count} 張 OK 樣本建立第 {expectedStation:00} 站檢測設定。";
            // 計算完成並保存後返回單站主頁；取消或計算失敗不會觸發此段。
            Application.Current.Windows.OfType<LumasharpChecker.view.ProfileAddWindows>()
                .FirstOrDefault(window => window.IsVisible)?.Close();
        }

        [RelayCommand]
        private void ResetCurrentParameters()
        {
            if (IsMultiStationTesting)
            {
                StationStatusMessage = "多站測試進行中，請完成後再重設參數。";
                return;
            }
            if (CurrentProfile == null)
            {
                StationStatusMessage = "請先選擇設定檔或載入已設定的站別，再重設參數。";
                return;
            }

            if (IsAnalyzingTestImages)
            {
                StationStatusMessage = "圖片仍在檢測中，請完成後再重設參數。";
                return;
            }

            var station = CurrentRecipe?.Stations.FirstOrDefault(item => item.Number == CurrentStationNumber);
            List<string> files;
            try
            {
                files = ResolveStationOkSamples(CurrentStationNumber, station?.OkDatabasePath,
                    isReset: true, out string selectedFolder);
                if (files.Count > 0 && CurrentRecipe != null)
                    SaveChosenOkFolderIfChanged(CurrentRecipe, CurrentStationNumber, selectedFolder);
            }
            catch (Exception ex)
            {
                StationStatusMessage = "讀取本站 OK 樣本失敗：" + ex.Message;
                MessageBox.Show(StationStatusMessage, "重設參數", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (files.Count == 0) return;

            SelectedFiles.Clear();
            foreach (var file in files) SelectedFiles.Add(file);
            IsRoiEditing = false;
            _windowService.ShowProfileAddWindow(
                SelectedFiles.ToList(), DetectionMode, CurrentProfile, SaveResetParameters);
        }

        private static readonly HashSet<string> BaselineImageExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

        private static List<string> EnumerateOkImages(string folder) =>
            Directory.EnumerateFiles(folder)
                .Where(path => BaselineImageExtensions.Contains(Path.GetExtension(path)))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

        private List<string> ResolveStationOkSamples(int stationNumber, string? configuredOkPath,
            bool isReset, out string selectedFolder)
        {
            selectedFolder = string.Empty;
            string configuredFolder = recipeManager.ResolveDatabasePath(configuredOkPath);
            if (Directory.Exists(configuredFolder))
            {
                var existingImages = EnumerateOkImages(configuredFolder);
                if (existingImages.Count >= 1)
                {
                    selectedFolder = configuredFolder;
                    return existingImages;
                }
                // 已設定路徑但沒有圖片才要求重選；單張可建立基準。
                MessageBox.Show(
                    $"第 {stationNumber:00} 站 OK 圖庫沒有可用圖片，請選擇至少包含 1 張圖片的資料夾。",
                    "OK 圖庫沒有圖片", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // .NET Framework 4.7.2 使用 Windows IFileOpenDialog：檔案總管式網址列／路徑貼上。
            string initialFolder = Directory.Exists(configuredFolder) ? configuredFolder
                : (CurrentRecipe?.TrainingImageRoot ?? Environment.CurrentDirectory);
            string? chosenFolder = ExplorerFolderPicker.PickFolder(
                Application.Current.MainWindow,
                $"第 {stationNumber:00} 站 · {(isReset ? "重設" : "新增")}參數 · 選取 OK 圖片資料夾",
                initialFolder);
            if (string.IsNullOrEmpty(chosenFolder)) return new List<string>();

            string folder = Path.GetFullPath(chosenFolder);
            if (Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                .Equals("NG", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("請選擇 OK 圖庫，不能以 NG 影像建立基準值。");
            // 若選到站號資料夾，直接進入 OK 子目錄，避免同時混入 NG。
            string okSubFolder = Path.Combine(folder, "OK");
            if (Directory.Exists(okSubFolder)) folder = okSubFolder;
            var images = EnumerateOkImages(folder);
            if (images.Count < 1)
            {
                MessageBox.Show("選取的資料夾沒有可用圖片。至少放入 1 張 OK 圖片即可新增參數。",
                    "OK 圖庫沒有圖片", MessageBoxButton.OK, MessageBoxImage.Warning);
                return new List<string>();
            }
            selectedFolder = folder;
            return images;
        }

        private void SaveChosenOkFolderIfChanged(RecipeManager.RecipeManifest recipe, int stationNumber, string selectedFolder)
        {
            var station = recipe.Stations.Single(item => item.Number == stationNumber);
            string previous = station.OkDatabasePath ?? string.Empty;
            if (string.Equals(recipeManager.ResolveDatabasePath(previous), selectedFolder,
                    StringComparison.OrdinalIgnoreCase)) return;
            // 若選的是程式包內的快取，以相對路徑保存，整包移到公司電腦仍能使用。
            string appRoot = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            station.OkDatabasePath = selectedFolder.StartsWith(appRoot, StringComparison.OrdinalIgnoreCase)
                ? selectedFolder.Substring(appRoot.Length)
                : selectedFolder;
            try { recipeManager.SaveManifest(recipe); }
            catch
            {
                station.OkDatabasePath = previous;
                throw;
            }
            RefreshStationDatabasePaths();
            RefreshStationSlots();
        }

        private void SaveResetParameters(ProfileData profile, string patternImagePath)
        {
            if (CurrentProfile == null || !ReferenceEquals(CurrentProfile, profile))
                throw new InvalidOperationException("目前站別已變更，無法保存重算參數。");
            if (string.IsNullOrWhiteSpace(CurrentProfile.ImagePath))
                throw new InvalidOperationException("目前設定沒有基準圖路徑，無法更新基準影像。");

            var patternPath = Path.GetFullPath(CurrentProfile.ImagePath);
            Directory.CreateDirectory(Path.GetDirectoryName(patternPath)!);
            if (!string.Equals(Path.GetFullPath(patternImagePath), patternPath, StringComparison.OrdinalIgnoreCase))
                File.Copy(patternImagePath, patternPath, true);
            CurrentProfile.Bitmap = ProfileManager.LoadBitmapFromPath(patternPath);
            CurrentProfile.ImageWidth = CurrentProfile.Bitmap.PixelWidth;
            CurrentProfile.ImageHeight = CurrentProfile.Bitmap.PixelHeight;

            LoadInspectionSettings(CurrentProfile);
            SaveCurrentInspectionSettings(reanalyzeTestImages: false);
            RefreshStationSlots();
            OnPropertyChanged(nameof(CurrentProfile));
            OnPropertyChanged(nameof(StationSettingsImage));
            OnPropertyChanged(nameof(ImageSizeSummary));
            OnPropertyChanged(nameof(BrightnessCvSummary));
            OnPropertyChanged(nameof(SharpnessCvSummary));
            OnPropertyChanged(nameof(BrightnessPercentileSummary));
            OnPropertyChanged(nameof(SharpnessPercentileSummary));
            SaveSettingsMessage = $"已用 {SelectedFiles.Count} 張樣本重算並保存目前設定，名稱維持「{CurrentProfile.Name}」。";
            if (IsTestImageAnalysisComplete && _allLoadedTestImageFiles.Count > 0)
                _ = ReanalyzeLoadedTestImagesAsync();
        }

        public void UpdateStationRoi(string roiType, int x, int y, int width, int height)
        {
            if (CurrentProfile == null)
                return;

            switch (roiType)
            {
                case "定位":
                    OriginalX = x;
                    OriginalY = y;
                    OriginalWidth = width;
                    OriginalHeight = height;
                    CurrentProfile.PatternMatchRoiX = x;
                    CurrentProfile.PatternMatchRoiY = y;
                    CurrentProfile.PatternMatchRoiW = width;
                    CurrentProfile.PatternMatchRoiH = height;
                    OnPropertyChanged(nameof(RectX));
                    OnPropertyChanged(nameof(RectY));
                    OnPropertyChanged(nameof(RectWidth));
                    OnPropertyChanged(nameof(RectHeight));
                    break;

                case "亮度":
                    OriginalX2 = x;
                    OriginalY2 = y;
                    OriginalWidth2 = width;
                    OriginalHeight2 = height;
                    CurrentProfile.BrightnessRoiX = x;
                    CurrentProfile.BrightnessRoiY = y;
                    CurrentProfile.BrightnessRoiW = width;
                    CurrentProfile.BrightnessRoiH = height;
                    CurrentProfile.MonitorRoiX = x;
                    CurrentProfile.MonitorRoiY = y;
                    CurrentProfile.MonitorRoiW = width;
                    CurrentProfile.MonitorRoiH = height;
                    OnPropertyChanged(nameof(RectX2));
                    OnPropertyChanged(nameof(RectY2));
                    OnPropertyChanged(nameof(RectWidth2));
                    OnPropertyChanged(nameof(RectHeight2));
                    break;

                case "銳利度" when IsSharpnessEnabled:
                    OriginalX3 = x;
                    OriginalY3 = y;
                    OriginalWidth3 = width;
                    OriginalHeight3 = height;
                    CurrentProfile.SharpnessRoiX = x;
                    CurrentProfile.SharpnessRoiY = y;
                    CurrentProfile.SharpnessRoiW = width;
                    CurrentProfile.SharpnessRoiH = height;
                    OnPropertyChanged(nameof(RectX3));
                    OnPropertyChanged(nameof(RectY3));
                    OnPropertyChanged(nameof(RectWidth3));
                    OnPropertyChanged(nameof(RectHeight3));
                    break;

                default:
                    return;
            }

            SaveCurrentInspectionSettings(reanalyzeTestImages: false);
            OnPropertyChanged(nameof(CurrentProfile));
            OnPropertyChanged(nameof(BrightnessRoiAreaSummary));
            OnPropertyChanged(nameof(SharpnessRoiAreaSummary));
        }

        private void SaveCurrentInspectionSettings(bool reanalyzeTestImages)
        {
            if (CurrentProfile == null) return;

            CurrentProfile.BrightnessLimitMode = BrightnessLimitMode;
            CurrentProfile.DetectionMode = DetectionMode;
            CurrentProfile.BrightnessBaseValue = BrightnessBaseValue;
            CurrentProfile.BrightnessPercentLower = BrightnessPercentLower;
            CurrentProfile.BrightnessPercentUpper = BrightnessPercentUpper;
            CurrentProfile.BrightnessFixedMin = BrightnessFixedMin;
            CurrentProfile.BrightnessFixedMax = BrightnessFixedMax;
            CurrentProfile.SharpnessLimitMode = SharpnessLimitMode;
            CurrentProfile.SharpnessBaseValue = SharpnessBaseValue;
            CurrentProfile.SharpnessPercentLower = SharpnessPercentLower;
            CurrentProfile.SharpnessFixedMin = SharpnessFixedMin;
            CurrentProfile.BrightnessMin = EffectiveBrightnessMin;
            CurrentProfile.BrightnessMax = EffectiveBrightnessMax;
            CurrentProfile.SharpnessMin = EffectiveSharpnessMin;
            CurrentProfile.ModifyDateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            if (CurrentRecipe != null)
            {
                recipeManager.SaveStation(CurrentRecipe, CurrentStationNumber, CurrentProfile);
                RefreshStationSlots();
                SaveSettingsMessage = $"{CurrentRecipe.Name} 第 {CurrentStationNumber:00} 站設定已儲存";
            }
            else
            {
                profileManager.SaveProfileSettings(CurrentProfile);
                SaveSettingsMessage = "檢測設定已儲存";
            }
            OnPropertyChanged(nameof(CurrentProfile));
            if (reanalyzeTestImages && IsTestImageAnalysisComplete && _allLoadedTestImageFiles.Count > 0)
            {
                _ = ReanalyzeLoadedTestImagesAsync();
            }
        }

        public async Task RetestFromAoiAsync()
        {
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting)
            {
                MessageBox.Show("檢測進行中，請先等待或停止。", "重新檢測", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (CurrentProfile == null || _allLoadedTestImageFiles.Count == 0)
            {
                MessageBox.Show("請先選擇已校準的站別並載入圖片。", "重新檢測", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            await ReanalyzeLoadedTestImagesAsync();
        }

        private async Task ReanalyzeLoadedTestImagesAsync()
        {
            await AnalyzeTestImageFolderAsync();
            ProcessCurrentTestImage();
        }

        [RelayCommand]
        private void DeleteSelectedProfile()
        {
            if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.Name))
            {
                SaveSettingsMessage = "請先選擇設定檔";
                return;
            }

            var result = MessageBox.Show(
                $"確定要刪除設定檔「{SelectedItem.Name}」？\n刪除後無法復原。",
                "刪除設定檔",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            string deletedName = SelectedItem.Name;
            try
            {
                profileManager.DeleteProfile(deletedName);
                RefreshProfiles();
                SaveSettingsMessage = $"已刪除設定檔: {deletedName}";
            }
            catch (Exception ex)
            {
                SaveSettingsMessage = $"刪除失敗: {ex.Message}";
            }
        }

        [RelayCommand]
        private void DeleteSelectedRecipeTemplate()
        {
            var template = SelectedRecipeTemplate;
            if (template == null || string.IsNullOrWhiteSpace(template.Name))
            {
                SaveSettingsMessage = "請先選擇共用設定檔範本";
                return;
            }

            var result = MessageBox.Show(
                $"確定要刪除共用設定檔範本「{template.Name}」？\n已套用到各 Recipe 的專屬副本不會受影響。",
                "刪除設定檔範本",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                profileManager.DeleteProfile(template.Name);
                SelectedRecipeTemplate = null;
                RefreshProfiles();
                SaveSettingsMessage = $"已刪除共用範本：{template.Name}；配方站別副本保留。";
            }
            catch (Exception ex)
            {
                SaveSettingsMessage = $"刪除設定檔範本失敗：{ex.Message}";
            }
        }

        private void RefreshProfiles()
        {
            var profiles = profileManager.LoadAllProfile();
            Items = new ObservableCollection<ProfileData>(profiles);
            SelectedItem = Items.FirstOrDefault();
            CurrentTestImageBitmap = null;
            TestImageFiles.Clear();
            _allLoadedTestImageFiles.Clear();
            _imageReferenceClasses.Clear();
            _resultListFilter = "ALL";
            TestImageTree.Clear();
            CurrentTestImageIndex = -1;
            CurrentTestImagePath = string.Empty;
            TestImageInfo = "圖片 0 / 0";
            TestImageBrightness = 0;
            TestImageBrightnessDeltaPercent = double.NaN;
            TestImageSharpness = 0;
            TestImageROI = new System.Drawing.Rectangle();
            TestImageBrightnessROI = new System.Drawing.Rectangle();
            TestImageSharpnessROI = new System.Drawing.Rectangle();
            TestImageMatchFailed = string.Empty;
            TestImageResults.Clear();
            IsTestImageAnalysisComplete = false;
            IsContinuousTestRunning = false;
            _continuousTestPaused = false;
            _continuousTestNextIndex = 0;
            ResetTestImageSummary();
            CanNavigatePrevious = false;
            CanNavigateNext = false;
            OnSelectionChanged();
            if (CurrentRecipe != null) LoadRecipeStation();
            UpdateDisplayImage();
        }

        [RelayCommand]
        private void ExportTestImages()
        {
            DateTime exportDate = DateTime.Now;
            string programName = CurrentRecipe?.Name ?? CurrentProfile?.Name ?? "未命名程式";
            string safeProgramName = MakeSafeFileNamePart(programName);
            string outputDirectory = Path.Combine(@"D:\光源點檢", exportDate.ToString("yyyy"));
            string outputPath = Path.Combine(outputDirectory, $"{exportDate:yyyyMMdd}_{safeProgramName}.xlsx");
            try
            {
                Directory.CreateDirectory(outputDirectory);
            }
            catch (Exception ex)
            {
                ShowExportNotice($"無法建立匯出資料夾：{outputDirectory}\n{ex.Message}", MessageBoxImage.Error);
                return;
            }

            var completedStations = CurrentRecipe == null
                ? new List<KeyValuePair<(string RecipeId, int Station), StationRunSummary>>()
                : _multiStationResults
                    .Where(item => item.Key.RecipeId == CurrentRecipe.Id && item.Value.Status == "完成" && item.Value.Results.Count > 0)
                    .OrderBy(item => item.Key.Station)
                    .ToList();
            bool hasStationResults = completedStations.Count > 0;
            bool isFlowResult = TestImageResults.Count > 0 &&
                TestImageResults.All(result => result.InspectionPipeline == "新版流程");
            bool isLegacyResult = !hasStationResults && !isFlowResult;
            if (!hasStationResults && TestImageResults.Count == 0)
            {
                ShowExportNotice("請先完成單站或多站連續檢測，再匯出報表。");
                return;
            }

            if ((hasStationResults || isFlowResult) && CurrentRecipe == null)
            {
                ShowExportNotice("請先載入 Recipe。");
                return;
            }

            if (isLegacyResult && CurrentProfile == null)
            {
                ShowExportNotice("請先選擇 Profile。");
                return;
            }

            if (!hasStationResults && _allLoadedTestImageFiles.Count == 0)
            {
                ShowExportNotice("請先載入測試圖片。");
                return;
            }

            if (isLegacyResult) SaveCurrentInspectionSettings(reanalyzeTestImages: false);
            if (!hasStationResults && !IsTestImageAnalysisComplete)
            {
                if (IsAnalyzingTestImages)
                {
                    ShowExportNotice("圖片仍在檢測中，請稍候再匯出。");
                    return;
                }

                ShowExportNotice("請先完成測試圖片檢測。");
                return;
            }

            try
            {
                if (hasStationResults)
                {
                    var stationReports = completedStations.Select(item => new StationExcelReportData(
                        item.Key.Station,
                        CurrentRecipe!.Name,
                        BuildFlowExportSpecRows(CurrentRecipe!, item.Key.Station),
                        item.Value.Results.ToList())).ToList();
                    PrepareExportTarget(outputPath);
                    ExcelReportWriter.WriteMultiStationTestResults(outputPath, stationReports);
                    SaveSettingsMessage = $"已匯出 {stationReports.Count} 站，共 {stationReports.Sum(station => station.Results.Count)} 筆：{outputPath}";
                    OpenExportFolder(outputDirectory);
                    return;
                }

                var specRows = isFlowResult
                    ? BuildFlowExportSpecRows(CurrentRecipe!, CurrentStationNumber)
                    : new List<string[]>
            {
                new[] { "項目", "值" },
                new[] { "Profile", CurrentInspectionName },
                new[] { "檢測模式", DetectionMode },
                new[] { "亮度判定方式", GetLimitModeDisplay(BrightnessLimitMode) },
                new[] { "亮度基準值", BrightnessBaseValue.ToString("F2", CultureInfo.InvariantCulture) },
                new[] { "亮度下限偏差%", BrightnessPercentLower.ToString("F2", CultureInfo.InvariantCulture) },
                new[] { "亮度上限偏差%", BrightnessPercentUpper.ToString("F2", CultureInfo.InvariantCulture) },
                new[] { "亮度固定下限", BrightnessFixedMin.ToString("F2", CultureInfo.InvariantCulture) },
                new[] { "亮度固定上限", BrightnessFixedMax.ToString("F2", CultureInfo.InvariantCulture) },
                new[] { "亮度有效下限", EffectiveBrightnessMin.ToString("F2", CultureInfo.InvariantCulture) },
                new[] { "亮度有效上限", EffectiveBrightnessMax.ToString("F2", CultureInfo.InvariantCulture) },
                new[] { "匯出時間", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
            };
                PrepareExportTarget(outputPath);

                ExcelReportWriter.WriteTestResults(
                    outputPath,
                    specRows,
                    TestImageResults,
                    isFlowResult ? TestImageResults.Min(result => result.BrightnessMin ?? 0) : EffectiveBrightnessMin,
                    isFlowResult ? TestImageResults.Max(result => result.BrightnessMax ?? 255) : EffectiveBrightnessMax);
                SaveSettingsMessage = $"已匯出 {TestImageResults.Count} 筆檢測結果：{outputPath}";
                OpenExportFolder(outputDirectory);
            }
            catch (Exception ex)
            {
                ShowExportNotice($"匯出失敗：{ex.Message}", MessageBoxImage.Error);
            }
        }

        private void ShowExportNotice(string message, MessageBoxImage icon = MessageBoxImage.Information)
        {
            SaveSettingsMessage = message;
            MessageBox.Show(message, "匯出報表", MessageBoxButton.OK, icon);
        }

        private static void PrepareExportTarget(string outputPath)
        {
            CloseOpenExcelWorkbook(outputPath);
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }

        private static void CloseOpenExcelWorkbook(string outputPath)
        {
            object? excel = null;
            object? workbooks = null;
            try
            {
                try { excel = Marshal.GetActiveObject("Excel.Application"); }
                catch (COMException) { return; }

                workbooks = excel.GetType().InvokeMember("Workbooks", BindingFlags.GetProperty, null, excel, null);
                int count = Convert.ToInt32(workbooks!.GetType().InvokeMember("Count", BindingFlags.GetProperty, null, workbooks, null));
                string target = Path.GetFullPath(outputPath);
                for (int index = count; index >= 1; index--)
                {
                    object? workbook = null;
                    try
                    {
                        workbook = workbooks.GetType().InvokeMember("Item", BindingFlags.GetProperty, null, workbooks, new object[] { index });
                        string fullName = Convert.ToString(workbook!.GetType().InvokeMember("FullName", BindingFlags.GetProperty, null, workbook, null)) ?? string.Empty;
                        if (string.Equals(Path.GetFullPath(fullName), target, StringComparison.OrdinalIgnoreCase))
                        {
                            // Save Excel edits before replacing the workbook with the new report.
                            workbook.GetType().InvokeMember("Close", BindingFlags.InvokeMethod, null, workbook, new object[] { true });
                            return;
                        }
                    }
                    finally
                    {
                        if (workbook != null && Marshal.IsComObject(workbook)) Marshal.ReleaseComObject(workbook);
                    }
                }
            }
            catch (COMException)
            {
                // If Excel is busy or the file is open outside the active Excel instance,
                // the normal file operation below will report the remaining lock clearly.
            }
            finally
            {
                if (workbooks != null && Marshal.IsComObject(workbooks)) Marshal.ReleaseComObject(workbooks);
                if (excel != null && Marshal.IsComObject(excel)) Marshal.ReleaseComObject(excel);
            }
        }

        private static string MakeSafeFileNamePart(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            string safe = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim().TrimEnd('.');
            return string.IsNullOrWhiteSpace(safe) ? "未命名程式" : safe;
        }

        private void OpenExportFolder(string folder)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{folder}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                SaveSettingsMessage += $"；資料夾開啟失敗：{ex.Message}";
            }
        }

        private List<string[]> BuildFlowExportSpecRows(RecipeManager.RecipeManifest recipe, int stationNumber)
        {
            var settings = LoadLightSettings(recipe, stationNumber);
            bool lightInspectionEnabled = recipeManager.LoadLightInspectionEnabled(recipe, stationNumber);
            var limits = lightInspectionEnabled ? settings.EffectiveLimits() : ((double Min, double Max)?)null;
            return new List<string[]>
            {
                new[] { "項目", "值" },
                new[] { "Recipe", recipe.Name },
                new[] { "Recipe ID", recipe.Id },
                new[] { "站別", stationNumber.ToString(CultureInfo.InvariantCulture) },
                new[] { "流程工具選取", recipeManager.LoadFlowSelection(recipe, stationNumber) },
                new[] { "光源檢測狀態", lightInspectionEnabled ? "啟用" : "略過（本站未啟用光源檢測）" },
                new[] { "光源檢測 ROI", !lightInspectionEnabled ? "不適用" : settings.HasRoi ? $"Left={settings.Left}, Top={settings.Top}, Right={settings.Right}, Bottom={settings.Bottom}" : "未設定" },
                new[] { "亮度判定方式", lightInspectionEnabled ? settings.LimitMode : "不適用" },
                new[] { "亮度基準值", lightInspectionEnabled ? settings.BaseValue.ToString("F2", CultureInfo.InvariantCulture) : "不適用" },
                new[] { "亮度有效下限", limits.HasValue ? limits.Value.Min.ToString("F2", CultureInfo.InvariantCulture) : "不適用" },
                new[] { "亮度有效上限", limits.HasValue ? limits.Value.Max.ToString("F2", CultureInfo.InvariantCulture) : "不適用" },
                new[] { "亮度下限偏差%", lightInspectionEnabled ? settings.PercentLower.ToString("F2", CultureInfo.InvariantCulture) : "不適用" },
                new[] { "亮度上限偏差%", lightInspectionEnabled ? settings.PercentUpper.ToString("F2", CultureInfo.InvariantCulture) : "不適用" },
                new[] { "匯出時間", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) }
            };
        }

        private static string Csv(string? value)
        {
            value ??= string.Empty;
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private static string GetLimitModeDisplay(string mode)
        {
            return mode == ProfileManager.LimitMode.Fixed ? "Fixed 固定值" : "Percent 百分比";
        }


        [RelayCommand]
        private void TestImage()
        {
            if (CurrentProfile == null) return;

            if (IsAnalyzingTestImages)
            {
                SaveSettingsMessage = "圖片仍在檢測中，請稍候再載入";
                return;
            }

            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "選擇測試圖片資料夾"
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            _activeStationDatabase = null;
            ResetTestImageState();
            LoadTestImageFolder(dialog.SelectedPath);
        }

        [RelayCommand]
        private async Task OpenStationDatabaseAsync(string kind)
        {
            if (IsAnalyzingTestImages)
            {
                SaveSettingsMessage = "圖片仍在檢測中，請完成後再切換圖庫。";
                return;
            }
            if (await LoadStationDatabaseAsync(kind, true)) _activeStationDatabase = kind;
        }

        [RelayCommand]
        private void OpenStandardPartImage()
        {
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting)
            {
                SaveSettingsMessage = "檢測進行中，請完成後再切換圖片。";
                return;
            }

            if (CurrentRecipe == null)
            {
                SaveSettingsMessage = "請先選擇 Recipe。";
                return;
            }

            string? imagePath = recipeManager.GetStationPatternPath(CurrentRecipe, CurrentStationNumber);
            if (imagePath == null)
            {
                SaveSettingsMessage = $"第 {CurrentStationNumber:00} 站尚未保存標準件圖。請先選擇圖片，再於「標準件圖」按右鍵選擇設定標準件。";
                MessageBox.Show(SaveSettingsMessage, "找不到標準件圖", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _activeStationDatabase = null;
            ResetTestImageState();
            TestImageFiles.Add(imagePath);
            _allLoadedTestImageFiles.Add(imagePath);
            _resultListFilter = "ALL";
            CurrentTestImageIndex = 0;
            ProcessCurrentTestImage();
            SaveSettingsMessage = $"第 {CurrentStationNumber:00} 站標準件圖：已載入設定標準件時保存的圖片。";
        }

        public void SaveStationStandardPartImage(string sourceImagePath)
        {
            if (CurrentRecipe == null)
                throw new InvalidOperationException("請先選擇 Recipe。" );
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting)
                throw new InvalidOperationException("檢測進行中不能更新標準件圖。" );
            if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
                throw new FileNotFoundException("目前圖片不存在，無法保存為標準件圖。", sourceImagePath);

            var recipe = CurrentRecipe;
            var profile = CurrentProfile;
            int stationNumber = CurrentStationNumber;
            string stationDirectory = Path.Combine(recipeManager.GetProfileBasePath(recipe),
                RecipeManager.GetStationDirectoryName(stationNumber));
            Directory.CreateDirectory(stationDirectory);
            string patternPath = Path.Combine(stationDirectory, Path.GetFileName(sourceImagePath));
            string tempPath = patternPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            string backupPath = patternPath + "." + Guid.NewGuid().ToString("N") + ".bak";
            bool hadPreviousImage = File.Exists(patternPath);
            var station = recipe.Stations.Single(item => item.Number == stationNumber);
            string? previousPatternFileName = station.PatternFileName;

            try
            {
                File.Copy(sourceImagePath, tempPath, false);

                if (hadPreviousImage) File.Replace(tempPath, patternPath, backupPath);
                else File.Move(tempPath, patternPath);

                try
                {
                    station.PatternFileName = Path.GetFileName(patternPath);
                    if (profile != null)
                    {
                        profile.ImagePath = patternPath;
                        profile.Bitmap = ProfileManager.LoadBitmapFromPath(patternPath);
                        profile.ImageWidth = profile.Bitmap.PixelWidth;
                        profile.ImageHeight = profile.Bitmap.PixelHeight;
                        recipeManager.SaveStation(recipe, stationNumber, profile);
                    }
                    else
                    {
                        recipeManager.SaveManifest(recipe);
                    }
                }
                catch
                {
                    station.PatternFileName = previousPatternFileName;
                    if (hadPreviousImage && File.Exists(backupPath)) File.Replace(backupPath, patternPath, null);
                    else if (!hadPreviousImage && File.Exists(patternPath)) File.Delete(patternPath);
                    throw;
                }
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
                if (File.Exists(backupPath)) File.Delete(backupPath);
            }

            _multiStationResults.Remove((recipe.Id, stationNumber));
            InvalidatePatternMatchResults();
            RefreshStationSlots();
            OnPropertyChanged(nameof(CurrentProfile));
            OnPropertyChanged(nameof(StationSettingsImage));
            OnPropertyChanged(nameof(ImageSizeSummary));
            SaveSettingsMessage = $"第 {stationNumber:00} 站標準件圖已更新；已清除舊判定，請重新測試。";
        }

        /// <summary>
        /// 進入站別時預設載入 ALL DB；已選擇本站圖庫時保留目前清單。
        /// 找不到圖庫僅更新狀態，需提示時由操作者明確觸發。
        /// </summary>
        public bool EnsureAoiAllDatabase(bool showError = false)
        {
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting)
                return false;
            if (_activeStationDatabase == "ALL" && TestImageFiles.Count > 0 &&
                !string.IsNullOrWhiteSpace(CurrentTestImagePath) && File.Exists(CurrentTestImagePath))
                return true;
            if (!LoadStationDatabase("ALL", showError)) return false;
            _activeStationDatabase = "ALL";
            return TestImageFiles.Count > 0;
        }

        private bool LoadStationDatabase(string kind, bool showError = false)
        {
            _databaseLoadGeneration++;
            var station = CurrentRecipe?.Stations.FirstOrDefault(item => item.Number == CurrentStationNumber);
            if (kind == "ALL")
            {
                if (station == null) return false;
                string allFolder = recipeManager.ResolveDatabasePath(station.AllDatabasePath);
                string okFolder = recipeManager.ResolveDatabasePath(station.OkDatabasePath);
                string ngFolder = recipeManager.ResolveDatabasePath(station.NgDatabasePath);
                List<string> okFiles = EnumerateDatabaseImages(okFolder);
                List<string> ngFiles = EnumerateDatabaseImages(ngFolder);
                var okFileSet = new HashSet<string>(okFiles, StringComparer.OrdinalIgnoreCase);
                var ngFileSet = new HashSet<string>(ngFiles, StringComparer.OrdinalIgnoreCase);
                List<string> allFiles = EnumerateDatabaseImages(allFolder, recursive: true);
                string[] files = (Directory.Exists(allFolder)
                        ? allFiles
                        : okFiles.Concat(ngFiles))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (files.Length == 0)
                {
                    SaveSettingsMessage = $"第 {CurrentStationNumber:00} 站 OK／NG DB 都沒有可用圖片。";
                    if (showError)
                        MessageBox.Show(SaveSettingsMessage, "ALL DB 無圖片", MessageBoxButton.OK, MessageBoxImage.Information);
                    return false;
                }

                ResetTestImageState();
                foreach (string file in files)
                {
                    TestImageFiles.Add(file);
                    _allLoadedTestImageFiles.Add(file);
                    if (okFileSet.Contains(file))
                        _imageReferenceClasses[file] = "OK";
                    else if (ngFileSet.Contains(file))
                        _imageReferenceClasses[file] = "NG";
                }
                TestImageTree.Clear();
                _resultListFilter = "ALL";
                CurrentTestImageIndex = 0;
                ProcessCurrentTestImage();
                SaveSettingsMessage = Directory.Exists(allFolder)
                    ? $"第 {CurrentStationNumber:00} 站 ALL DB：已載入資料夾內 {files.Length} 張圖片；結果顯示流程判定。"
                    : $"第 {CurrentStationNumber:00} 站 ALL DB：來源 OK {okFiles.Count} 張、NG {ngFiles.Count} 張，共 {files.Length} 張；結果顯示流程判定。";
                return true;
            }

            string? folder = kind switch
            {
                "OK" => station?.OkDatabasePath,
                "NG" => station?.NgDatabasePath,
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(folder)) folder = recipeManager.ResolveDatabasePath(folder);
            string label = kind + " DB";
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                if (showError)
                {
                    SaveSettingsMessage = $"第 {CurrentStationNumber:00} 站的 {label} 資料夾尚未設定或不存在。";
                    MessageBox.Show(SaveSettingsMessage + (string.IsNullOrWhiteSpace(folder) ? "\n請先使用圖庫設定選擇日期資料夾。" : $"\n{folder}"),
                        "無法載入圖庫", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return false;
            }

            ResetTestImageState();
            if (!LoadTestImageFolder(folder, quietEmpty: !showError)) return false;
            string? referenceClass = kind switch { "OK" => "OK", "NG" => "NG", _ => null };
            if (referenceClass != null)
                foreach (string file in _allLoadedTestImageFiles) _imageReferenceClasses[file] = referenceClass;
            if (TestImageFiles.Count > 0)
                SaveSettingsMessage = $"第 {CurrentStationNumber:00} 站 {label}：已載入 {TestImageFiles.Count} 張；按連續測試開始檢測，結果清單會區分圖庫標記與流程判定。";
            return true;
        }

        private async Task<bool> LoadStationDatabaseAsync(string kind, bool showError = false)
        {
            int generation = ++_databaseLoadGeneration;
            var recipe = CurrentRecipe;
            int stationNumber = CurrentStationNumber;
            try
            {
                var station = recipe?.Stations.FirstOrDefault(item => item.Number == stationNumber);
                if (station == null) return false;

                if (kind == "ALL")
                {
                    string allFolder = recipeManager.ResolveDatabasePath(station.AllDatabasePath);
                    string okFolder = recipeManager.ResolveDatabasePath(station.OkDatabasePath);
                    string ngFolder = recipeManager.ResolveDatabasePath(station.NgDatabasePath);
                    SaveSettingsMessage = $"正在掃描第 {CurrentStationNumber:00} 站 ALL 圖庫…";
                    var scan = await Task.Run(() =>
                    {
                        List<string> ok = EnumerateDatabaseImages(okFolder);
                        List<string> ng = EnumerateDatabaseImages(ngFolder);
                        List<string> all = EnumerateDatabaseImages(allFolder, recursive: true);
                        string[] files = (Directory.Exists(allFolder) ? all : ok.Concat(ng))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
                        return (Ok: ok, Ng: ng, Files: files, HasAllFolder: Directory.Exists(allFolder));
                    });
                    if (generation != _databaseLoadGeneration || stationNumber != CurrentStationNumber ||
                        !ReferenceEquals(recipe, CurrentRecipe)) return false;
                    if (scan.Files.Length == 0)
                    {
                        SaveSettingsMessage = $"第 {stationNumber:00} 站 OK／NG DB 都沒有可用圖片。";
                        if (showError) MessageBox.Show(SaveSettingsMessage, "ALL DB 無圖片", MessageBoxButton.OK, MessageBoxImage.Information);
                        return false;
                    }

                    ResetTestImageState();
                    ApplyTestImageFiles(scan.Files);
                    var okSet = new HashSet<string>(scan.Ok, StringComparer.OrdinalIgnoreCase);
                    var ngSet = new HashSet<string>(scan.Ng, StringComparer.OrdinalIgnoreCase);
                    foreach (string file in scan.Files)
                    {
                        if (okSet.Contains(file)) _imageReferenceClasses[file] = "OK";
                        else if (ngSet.Contains(file)) _imageReferenceClasses[file] = "NG";
                    }
                    _resultListFilter = "ALL";
                    CurrentTestImageIndex = 0;
                    ProcessCurrentTestImage();
                    SaveSettingsMessage = scan.HasAllFolder
                        ? $"第 {CurrentStationNumber:00} 站 ALL DB：已載入資料夾內 {scan.Files.Length} 張圖片；結果顯示流程判定。"
                        : $"第 {CurrentStationNumber:00} 站 ALL DB：來源 OK {scan.Ok.Count} 張、NG {scan.Ng.Count} 張，共 {scan.Files.Length} 張；結果顯示流程判定。";
                    return true;
                }

                string? folder = kind switch
                {
                    "OK" => station.OkDatabasePath,
                    "NG" => station.NgDatabasePath,
                    _ => null
                };
                if (!string.IsNullOrWhiteSpace(folder)) folder = recipeManager.ResolveDatabasePath(folder);
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                {
                    if (showError)
                    {
                        SaveSettingsMessage = $"第 {CurrentStationNumber:00} 站的 {kind} DB 資料夾尚未設定或不存在。";
                        MessageBox.Show(SaveSettingsMessage, "無法載入圖庫", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return false;
                }

                SaveSettingsMessage = $"正在掃描第 {CurrentStationNumber:00} 站 {kind} 圖庫…";
                List<string> imageFiles = await Task.Run(() => EnumerateDatabaseImages(folder));
                if (generation != _databaseLoadGeneration || stationNumber != CurrentStationNumber ||
                    !ReferenceEquals(recipe, CurrentRecipe)) return false;
                ResetTestImageState();
                ApplyTestImageFiles(imageFiles);
                string? referenceClass = kind is "OK" or "NG" ? kind : null;
                if (referenceClass != null)
                    foreach (string file in imageFiles) _imageReferenceClasses[file] = referenceClass;
                if (imageFiles.Count == 0)
                {
                    if (!showError) return true;
                    SaveSettingsMessage = $"第 {CurrentStationNumber:00} 站的 {kind} DB 沒有可用圖片。";
                    MessageBox.Show(SaveSettingsMessage, "無法載入圖庫", MessageBoxButton.OK, MessageBoxImage.Information);
                    return false;
                }
                CurrentTestImageIndex = 0;
                ProcessCurrentTestImage();
                SaveSettingsMessage = $"第 {CurrentStationNumber:00} 站 {kind} DB：已載入 {imageFiles.Count} 張；按連續測試開始檢測。";
                return true;
            }
            catch (Exception ex)
            {
                if (generation != _databaseLoadGeneration || stationNumber != CurrentStationNumber ||
                    !ReferenceEquals(recipe, CurrentRecipe)) return false;
                SaveSettingsMessage = $"掃描第 {stationNumber:00} 站圖庫失敗：{ex.GetBaseException().Message}";
                if (showError) MessageBox.Show(SaveSettingsMessage, "無法載入圖庫", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private void ApplyTestImageFiles(IEnumerable<string> files)
        {
            string[] materialized = files as string[] ?? files.ToArray();
            TestImageFiles = new ObservableCollection<string>(materialized);
            _allLoadedTestImageFiles.Clear();
            _allLoadedTestImageFiles.AddRange(materialized);
            _imageReferenceClasses.Clear();
            TestImageTree.Clear(); // 建立選圖樹延後到使用者真的開啟選圖視窗時。
            TestImageResults.Clear();
            IsTestImageAnalysisComplete = false;
            ResetTestImageSummary();
        }

        private static List<string> EnumerateDatabaseImages(string folder, bool recursive = false) =>
            Directory.Exists(folder)
                ? Directory.EnumerateFiles(folder, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                    .Where(path => BaselineImageExtensions.Contains(Path.GetExtension(path)))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : new List<string>();

        internal string GetReferenceClassForImage(string imagePath)
        {
            if (_imageReferenceClasses.TryGetValue(imagePath, out string? savedClass)) return savedClass;
            string parent = Path.GetFileName(Path.GetDirectoryName(imagePath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return parent.Equals("OK", StringComparison.OrdinalIgnoreCase) ? "OK"
                : parent.Equals("NG", StringComparison.OrdinalIgnoreCase) ? "NG" : "未標記";
        }

        [RelayCommand]
        private void SelectSingleImage()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "圖片檔案|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有檔案|*.*",
                Multiselect = false,
                Title = "選擇圖片"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            if (IsAnalyzingTestImages)
            {
                SaveSettingsMessage = "圖片仍在檢測中，請稍候再載入";
                return;
            }

            _activeStationDatabase = null;
            ResetTestImageState();
            TestImageFiles.Add(dialog.FileName);
            _allLoadedTestImageFiles.Add(dialog.FileName);
            CurrentTestImageIndex = 0;
            ProcessCurrentTestImage();
            SaveSettingsMessage = "已選擇單張圖片；按「連續測試」才會開始檢測";
        }

        private void ResetTestImageState()
        {
            _databaseLoadGeneration++;
            IsContinuousTestRunning = false;
            _continuousTestPaused = false;
            _continuousTestNextIndex = 0;
            TestImageFiles.Clear();
            _allLoadedTestImageFiles.Clear();
            _imageReferenceClasses.Clear();
            _resultListFilter = "ALL";
            TestImageTree.Clear();
            CurrentTestImageIndex = -1;
            CurrentTestImagePath = string.Empty;
            TestImageInfo = "圖片 0 / 0";
            CurrentTestImageBitmap = null;
            CanNavigatePrevious = false;
            CanNavigateNext = false;
            TestImageBrightness = 0;
            TestImageBrightnessDeltaPercent = double.NaN;
            TestImageSharpness = 0;
            TestImageROI = new System.Drawing.Rectangle();
            TestImageBrightnessROI = new System.Drawing.Rectangle();
            TestImageSharpnessROI = new System.Drawing.Rectangle();
            TestImageResults.Clear();
            IsTestImageAnalysisComplete = false;
            ResetTestImageSummary();
            UpdateDisplayImage();
        }

        /// <summary>定位樣板異動後清空舊判定，保留原圖清單供重新測試。</summary>
        public void InvalidatePatternMatchResults()
        {
            if (IsAnalyzingTestImages || IsMultiStationTesting || IsContinuousTestRunning)
                throw new InvalidOperationException("檢測進行中不能修改定位樣板。");
            TestImageResults.Clear();
            IsTestImageAnalysisComplete = false;
            TestImageMatchFailed = string.Empty;
            TestImageBrightness = 0;
            TestImageBrightnessDeltaPercent = double.NaN;
            TestImageSharpness = 0;
            TestImageROI = System.Drawing.Rectangle.Empty;
            TestImageBrightnessROI = System.Drawing.Rectangle.Empty;
            TestImageSharpnessROI = System.Drawing.Rectangle.Empty;
            ResetTestImageSummary();
            SaveSettingsMessage = "定位樣板已更新，請重新執行測試以取得新的判定結果。";
        }

        private bool LoadTestImageFolder(string folderPath, bool quietEmpty = false)
        {
            try
            {
                // 取得資料夾內所有圖片檔案
                var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
                var files = Directory.GetFiles(folderPath)
                    .Where(file => imageExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .OrderBy(file => file)
                    .ToArray();

                TestImageFiles.Clear();
                _allLoadedTestImageFiles.Clear();
                _imageReferenceClasses.Clear();
                foreach (var file in files)
                {
                    TestImageFiles.Add(file);
                    _allLoadedTestImageFiles.Add(file);
                }
                _resultListFilter = "ALL";

                TestImageTree.Clear();
                if (files.Length == 0)
                {
                    if (!quietEmpty) SaveSettingsMessage = "資料夾內沒有支援的圖片";
                    return true;
                }

                // 載入資料夾只建立清單並顯示第一張預覽，不執行影像檢測。
                CurrentTestImageIndex = 0;
                ProcessCurrentTestImage();
                SaveSettingsMessage = $"已載入 {files.Length} 張圖片；按「連續測試」開始檢測";
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"載入測試圖片資料夾失敗: {ex.Message}");
                SaveSettingsMessage = $"載入圖片失敗：{ex.Message}";
                return false;
            }
        }

        internal ObservableCollection<TestImageTreeNode> CreateTestImagePickerTree() => BuildTestImageTree(TestImageFiles);

        private static ObservableCollection<TestImageTreeNode> BuildTestImageTree(IEnumerable<string> files)
        {
            var paths = files.ToList();
            var root = new TestImageTreeNode
            {
                Name = paths.Count == 0 ? "圖片" : Path.GetFileName(Path.GetDirectoryName(paths[0]) ?? "圖片"),
                IsFolder = true
            };
            foreach (string path in paths)
            {
                root.Children.Add(new TestImageTreeNode
                {
                    Name = Path.GetFileName(path),
                    FilePath = path,
                    IsFolder = false
                });
            }

            return paths.Count == 0
                ? new ObservableCollection<TestImageTreeNode>()
                : new ObservableCollection<TestImageTreeNode> { root };
        }

        [RelayCommand]
        private void StartContinuousTest()
        {
            if (IsMultiStationTesting)
            {
                SaveSettingsMessage = "多站測試進行中，請先完成或停止";
                return;
            }
            if (IsContinuousTestRunning)
            {
                IsContinuousTestRunning = false;
                _continuousTestPaused = true;
                SaveSettingsMessage = $"連續測試已暫停（{_continuousTestNextIndex} / {_allLoadedTestImageFiles.Count}）";
                return;
            }

            if (IsAnalyzingTestImages)
            {
                if (_continuousTestLoopActive && _continuousTestPaused)
                {
                    _continuousTestPaused = false;
                    IsContinuousTestRunning = true;
                    SaveSettingsMessage = "連續測試繼續執行";
                }
                else
                {
                    SaveSettingsMessage = "圖片仍在檢測中，請稍候";
                }
                return;
            }

            if (_allLoadedTestImageFiles.Count == 0)
            {
                SaveSettingsMessage = "請先按 OK DB、NG DB 或選擇資料夾載入測試圖片，再開始連續測試。";
                MessageBox.Show(SaveSettingsMessage, "連續測試", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (CurrentProfile == null)
            {
                SaveSettingsMessage = "目前站別尚未設定檢測參數，請先選擇已設定的站別或新增參數。";
                MessageBox.Show(SaveSettingsMessage, "連續測試", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!_continuousTestPaused)
            {
                TestImageResults.Clear();
                IsTestImageAnalysisComplete = false;
                ResetTestImageSummary();
                _continuousTestNextIndex = 0;
                _resultListFilter = "ALL";
            }

            _continuousTestPaused = false;
            IsContinuousTestRunning = true;
            AnalysisProgressText = $"準備檢測 {_continuousTestNextIndex} / {_allLoadedTestImageFiles.Count}";
            _ = RunContinuousTestAsync();
        }

        private async Task RunContinuousTestAsync()
        {
            var imageFiles = _allLoadedTestImageFiles.ToList();
            if (imageFiles.Count == 0)
            {
                IsContinuousTestRunning = false;
                return;
            }

            _continuousTestLoopActive = true;
            IsAnalyzingTestImages = true;
            AnalysisProgressMaximum = imageFiles.Count;
            AnalysisProgressValue = _continuousTestNextIndex;
            string profileName = GetProcessingProfileName();
            string profileBasePath = GetProcessingProfileBasePath();
            bool includeSharpness = IsSharpnessEnabled;
            double brightnessBaseValue = BrightnessBaseValue;

            try
            {
                var processor = new ImageProcessor(profileBasePath);
                while (IsContinuousTestRunning && _continuousTestNextIndex < imageFiles.Count)
                {
                    int index = _continuousTestNextIndex;
                    string imagePath = imageFiles[index];
                    CurrentTestImageIndex = index;
                    CurrentTestImagePath = imagePath;
                    TestImageInfo = $"圖片 {index + 1} / {imageFiles.Count}";
                    CanNavigatePrevious = index > 0;
                    CanNavigateNext = index < imageFiles.Count - 1;

                    AnalysisProgressText = $"檢測中 {index + 1} / {imageFiles.Count}: {Path.GetFileName(imagePath)}";

                    var result = await Task.Run(() => AnalyzeSingleTestImage(processor, imagePath, profileName, includeSharpness, brightnessBaseValue));

                    UpsertTestImageResult(result);

                    _continuousTestNextIndex = index + 1;
                    AnalysisProgressValue = _continuousTestNextIndex;

                    // 每張圖處理完即更新結果框與預覽，讓 UI 即時顯示最新定位/ROI
                    if (IsContinuousTestRunning)
                    {
                        ApplyTestImageResult(result);
                        UpdateDisplayRoiProperties();
                        RefreshContinuousPreview(imagePath);
                    }
                    else
                    {
                        ApplyTestImageResult(result);
                        RefreshContinuousPreview(imagePath);
                    }
                }

                if (_continuousTestNextIndex >= imageFiles.Count)
                {
                    IsTestImageAnalysisComplete = true;
                    AnalysisProgressText = $"檢測完成 {TestImageResults.Count} 張";
                    _continuousTestPaused = false;
                    _continuousTestNextIndex = 0;
                    ApplyTestImageResultFilter(_resultListFilter, showMessage: false);
                }
                else
                {
                    _continuousTestPaused = true;
                    AnalysisProgressText = $"已暫停 {_continuousTestNextIndex} / {imageFiles.Count}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"連續測試失敗: {ex.Message}");
                SaveSettingsMessage = $"連續測試失敗：{ex.Message}";
                AnalysisProgressText = SaveSettingsMessage;
                MessageBox.Show(SaveSettingsMessage, "連續測試失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
                _continuousTestPaused = _continuousTestNextIndex < imageFiles.Count;
            }
            finally
            {
                _continuousTestLoopActive = false;
                IsContinuousTestRunning = false;
                IsAnalyzingTestImages = false;
            }
        }

        public Task NavigateTestImageByWheelAsync(int delta) =>
            delta > 0 ? PreviousTestImage() : NextTestImage();

        private void RefreshContinuousPreview(string imagePath)
        {
            try
            {
                CurrentTestImageBitmap = LoadBitmapFromPath(imagePath);
                UpdateDisplayImage();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"連續測試預覽載入失敗: {ex.Message}");
                CurrentTestImageBitmap = null;
                UpdateDisplayImage();
                TestImageMatchFailed = "圖片預覽載入失敗";
            }
        }

        public void SelectTestImage(string filePath)
        {
            if (IsContinuousTestRunning) return;
            int index = TestImageFiles.IndexOf(filePath);
            if (index < 0) return;
            CurrentTestImageIndex = index;
            ProcessCurrentTestImage();
        }

        private async void AnalyzeTestImageFolder()
        {
            await AnalyzeTestImageFolderAsync();
        }

        private async Task AnalyzeTestImageFolderAsync()
        {
            if (IsAnalyzingTestImages || IsMultiStationTesting)
            {
                return;
            }

            TestImageResults.Clear();
            IsTestImageAnalysisComplete = false;
            ResetTestImageSummary();

            if (CurrentProfile == null || _allLoadedTestImageFiles.Count == 0)
            {
                return;
            }

            var imageFiles = _allLoadedTestImageFiles.ToList();
            string profileName = GetProcessingProfileName();
            string profileBasePath = GetProcessingProfileBasePath();
            bool includeSharpness = IsSharpnessEnabled;
            double brightnessBaseValue = BrightnessBaseValue;

            IsAnalyzingTestImages = true;
            AnalysisProgressValue = 0;
            AnalysisProgressMaximum = Math.Max(1, imageFiles.Count);
            AnalysisProgressText = $"準備檢測 0 / {imageFiles.Count}";

            try
            {
                var analyzedResults = await Task.Run(() =>
                {
                    ImageProcessor processor = new ImageProcessor(profileBasePath);
                    var results = new List<TestImageResultRow>();

                    for (int i = 0; i < imageFiles.Count; i++)
                    {
                        string imageFile = imageFiles[i];
                        results.Add(AnalyzeSingleTestImage(processor, imageFile, profileName, includeSharpness, brightnessBaseValue));

                        int processed = i + 1;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AnalysisProgressValue = processed;
                            AnalysisProgressText = $"檢測中 {processed} / {imageFiles.Count}: {Path.GetFileName(imageFile)}";
                        });
                    }

                    return results
                        .OrderBy(result => result.IsOverallPass)
                        .ThenBy(result => result.FileName)
                        .ToList();
                });

                foreach (var result in analyzedResults)
                {
                    TestImageResults.Add(result);
                }

                UpdateTestImageSummary();
                IsTestImageAnalysisComplete = true;
                AnalysisProgressText = $"檢測完成 {TestImageResults.Count} 張";
                ApplyTestImageResultFilter(_resultListFilter, showMessage: false);
            }
            finally
            {
                IsAnalyzingTestImages = false;
            }
        }

        private TestImageResultRow AnalyzeSingleTestImage(ImageProcessor processor, string imageFile)
        {
            return AnalyzeSingleTestImage(processor, imageFile, GetProcessingProfileName(), IsSharpnessEnabled, BrightnessBaseValue);
        }

        public Task<TestImageResultRow> TestLightImageAsync(string imageFile)
        {
            if (CurrentProfile == null || CurrentRecipe == null)
                throw new InvalidOperationException("請先載入已校準的配方與站別。");
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting)
                throw new InvalidOperationException("目前正在檢測，請完成或停止後再測試光源。");
            if (string.IsNullOrWhiteSpace(imageFile) || !File.Exists(imageFile))
                throw new FileNotFoundException("請先選擇目前站別的圖片。");
            string profileName = GetProcessingProfileName();
            string profileBasePath = GetProcessingProfileBasePath();
            double baseValue = BrightnessBaseValue;
            return Task.Run(() => AnalyzeSingleTestImage(new ImageProcessor(profileBasePath), imageFile, profileName, false, baseValue));
        }

        private TestImageResultRow AnalyzeSingleTestImage(ImageProcessor processor, string imageFile, string profileName, bool includeSharpness, double brightnessBaseValue)
        {
            try
            {
                using Mat mat = new Mat(imageFile);
                var result = processor.ProcessImage2(mat, profileName, includeSharpness);

                string message = result.IsMatched ? string.Empty : "圖片匹配失敗";
                return new TestImageResultRow
                {
                    FileName = Path.GetFileName(imageFile),
                    FilePath = imageFile,
                    Brightness = result.Brightness,
                    BrightnessMin = result.BrightnessMin,
                    BrightnessMax = result.BrightnessMax,
                    BrightnessDeltaPercent = CalculateBrightnessDeltaPercent(result.Brightness, brightnessBaseValue),
                    Sharpness = result.Sharpness,
                    BrightnessSafe = result.BrightnessSafe,
                    SharpnessSafe = result.SharpnessSafe,
                    IncludeSharpness = includeSharpness,
                    IsMatched = result.IsMatched,
                    ReferenceClass = GetReferenceClassForImage(imageFile),
                    RoiX = result.RoiCoords.X,
                    RoiY = result.RoiCoords.Y,
                    RoiW = result.RoiCoords.Width,
                    RoiH = result.RoiCoords.Height,
                    BrightnessRoiX = result.BrightnessRoiCoords.X,
                    BrightnessRoiY = result.BrightnessRoiCoords.Y,
                    BrightnessRoiW = result.BrightnessRoiCoords.Width,
                    BrightnessRoiH = result.BrightnessRoiCoords.Height,
                    SharpnessRoiX = result.SharpnessRoiCoords.X,
                    SharpnessRoiY = result.SharpnessRoiCoords.Y,
                    SharpnessRoiW = result.SharpnessRoiCoords.Width,
                    SharpnessRoiH = result.SharpnessRoiCoords.Height,
                    Message = message
                };
            }
            catch (Exception ex)
            {
                return new TestImageResultRow
                {
                    FileName = Path.GetFileName(imageFile),
                    FilePath = imageFile,
                    BrightnessDeltaPercent = double.NaN,
                    BrightnessSafe = false,
                    SharpnessSafe = !includeSharpness,
                    IncludeSharpness = includeSharpness,
                    IsMatched = false,
                    ReferenceClass = GetReferenceClassForImage(imageFile),
                    Message = $"處理失敗: {ex.Message}"
                };
            }
        }

        private void ResetTestImageSummary()
        {
            _testImageResultIndexes.Clear();
            _brightnessOkResultCount = 0;
            _brightnessNgResultCount = 0;
            _sharpnessOkResultCount = 0;
            _overallOkResultCount = 0;
            TotalTestImageCount = 0;
            BrightnessOkCount = 0;
            BrightnessNgCount = 0;
            SharpnessOkCount = 0;
            SharpnessNgCount = 0;
            OverallOkCount = 0;
            OverallNgCount = 0;
            OverallRetestCount = 0;
            OverallOkRateText = "0.00%";
            OverallNgRateText = "0.00%";
        }

        private void UpdateTestImageSummary()
        {
            _testImageResultIndexes.Clear();
            _brightnessOkResultCount = 0;
            _brightnessNgResultCount = 0;
            _sharpnessOkResultCount = 0;
            _overallOkResultCount = 0;

            for (int i = 0; i < TestImageResults.Count; i++)
            {
                TestImageResultRow result = TestImageResults[i];
                _testImageResultIndexes[result.FilePath] = i;
                AdjustTestImageResultCounts(result, 1);
            }

            UpdateTestImageSummaryProperties();
        }

        private void UpsertTestImageResult(TestImageResultRow result)
        {
            if (_testImageResultIndexes.TryGetValue(result.FilePath, out int existingIndex) &&
                existingIndex >= 0 && existingIndex < TestImageResults.Count)
            {
                AdjustTestImageResultCounts(TestImageResults[existingIndex], -1);
                TestImageResults[existingIndex] = result;
                AdjustTestImageResultCounts(result, 1);
            }
            else
            {
                _testImageResultIndexes[result.FilePath] = TestImageResults.Count;
                TestImageResults.Add(result);
                AdjustTestImageResultCounts(result, 1);
            }

            UpdateTestImageSummaryProperties();
        }

        private void AdjustTestImageResultCounts(TestImageResultRow result, int direction)
        {
            if (result.BrightnessMeasured)
            {
                if (result.BrightnessSafe) _brightnessOkResultCount += direction;
                else _brightnessNgResultCount += direction;
            }
            if (result.SharpnessSafe) _sharpnessOkResultCount += direction;
            if (result.IsOverallPass) _overallOkResultCount += direction;
        }

        private void UpdateTestImageSummaryProperties()
        {
            TotalTestImageCount = TestImageResults.Count;
            BrightnessOkCount = _brightnessOkResultCount;
            BrightnessNgCount = _brightnessNgResultCount;
            SharpnessOkCount = IsSharpnessEnabled ? _sharpnessOkResultCount : 0;
            SharpnessNgCount = IsSharpnessEnabled ? TotalTestImageCount - SharpnessOkCount : 0;
            OverallOkCount = _overallOkResultCount;
            OverallNgCount = TotalTestImageCount - OverallOkCount;
            OverallRetestCount = TestImageResults.Count(result => result.IsRetest);
            OverallOkRateText = TotalTestImageCount == 0
                ? "0.00%"
                : $"{OverallOkCount * 100.0 / TotalTestImageCount:F2}%";
            OverallNgRateText = TotalTestImageCount == 0
                ? "0.00%"
                : $"{OverallNgCount * 100.0 / TotalTestImageCount:F2}%";
        }

        private TestImageResultRow? FindTestImageResult(string imagePath)
        {
            return _testImageResultIndexes.TryGetValue(imagePath, out int index) &&
                   index >= 0 && index < TestImageResults.Count
                ? TestImageResults[index]
                : null;
        }

        private bool AreAllTestImagesAnalyzed()
        {
            return _allLoadedTestImageFiles.Count > 0 && _testImageResultIndexes.Count >= _allLoadedTestImageFiles.Count;
        }

        private void ProcessCurrentTestImage(bool showSourcePreview = true)
        {
            if (CurrentTestImageIndex < 0 || CurrentTestImageIndex >= TestImageFiles.Count)
                return;

            LastFlowPreviewPath = null;

            CurrentTestImagePath = TestImageFiles[CurrentTestImageIndex];
            TestImageInfo = $"{_resultListFilter} {CurrentTestImageIndex + 1} / {TestImageFiles.Count}";

            // 更新導航按鈕狀態
            CanNavigatePrevious = CurrentTestImageIndex > 0;
            CanNavigateNext = CurrentTestImageIndex < TestImageFiles.Count - 1;

            var cachedResult = FindTestImageResult(CurrentTestImagePath);
            if (cachedResult != null)
            {
                ApplyTestImageResult(cachedResult);
                UpdateDisplayRoiProperties();
                LoadCurrentPreviewIfNeeded(cachedResult);
                return;
            }

            try
            {
                // 清單和選圖只切換預覽；方向鍵切換圖片時會補做單張分析。
                TestImageBrightness = 0;
                TestImageBrightnessDeltaPercent = double.NaN;
                TestImageSharpness = 0;
                TestImageROI = new System.Drawing.Rectangle();
                TestImageBrightnessROI = new System.Drawing.Rectangle();
                TestImageSharpnessROI = new System.Drawing.Rectangle();
                BrightnessColor = Brushes.Black;
                SharpnessColor = Brushes.Black;
                TestImageMatchFailed = string.Empty;
                if (showSourcePreview)
                {
                    CurrentTestImageBitmap = LoadBitmapFromPath(CurrentTestImagePath);
                    UpdateDisplayImage();
                }
                UpdateDisplayRoiProperties();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"處理測試圖片失敗: {ex.Message}");
                TestImageBrightness = 0;
                TestImageBrightnessDeltaPercent = double.NaN;
                TestImageSharpness = 0;
                TestImageROI = new System.Drawing.Rectangle();
                TestImageBrightnessROI = new System.Drawing.Rectangle();
                TestImageSharpnessROI = new System.Drawing.Rectangle();
                CurrentTestImageBitmap = null;
                UpdateDisplayImage();
                TestImageMatchFailed = "圖片匹配失敗";
                UpdateDisplayRoiProperties();
            }
        }

        private void ApplyTestImageResult(TestImageResultRow result)
        {
            bool showProcessedPreview = UsesProcessedPreview(result);
            int originX = showProcessedPreview ? result.FrameOriginX : 0;
            int originY = showProcessedPreview ? result.FrameOriginY : 0;
            TestImageBrightness = result.Brightness;
            TestImageBrightnessDeltaPercent = result.BrightnessDeltaPercent;
            TestImageSharpness = result.Sharpness;
            TestImageROI = new System.Drawing.Rectangle(result.RoiX - originX, result.RoiY - originY, result.RoiW, result.RoiH);
            TestImageBrightnessROI = new System.Drawing.Rectangle(result.BrightnessRoiX - originX, result.BrightnessRoiY - originY, result.BrightnessRoiW, result.BrightnessRoiH);
            TestImageSharpnessROI = new System.Drawing.Rectangle(result.SharpnessRoiX - originX, result.SharpnessRoiY - originY, result.SharpnessRoiW, result.SharpnessRoiH);
            BrightnessColor = !result.BrightnessMeasured ? Brushes.Gray : result.BrightnessSafe ? Brushes.Green : Brushes.Red;
            SharpnessColor = !result.IncludeSharpness || result.SharpnessSafe ? Brushes.Green : Brushes.Red;
            TestImageMatchFailed = result.IsMatched ? string.Empty : result.Message;
        }

        private void LoadCurrentPreviewIfNeeded(TestImageResultRow result)
        {
            try
            {
                string previewPath = UsesProcessedPreview(result) ? result.ProcessedImagePath : CurrentTestImagePath;
                CurrentTestImageBitmap = LoadBitmapFromPath(previewPath);
                UpdateDisplayImage();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"載入測試圖片顯示失敗: {ex.Message}");
                CurrentTestImageBitmap = null;
                UpdateDisplayImage();
            }
        }

        private static bool UsesProcessedPreview(TestImageResultRow result) =>
            result.ShowProcessedPreview && !string.IsNullOrWhiteSpace(result.ProcessedImagePath) && File.Exists(result.ProcessedImagePath);

        internal string? LastFlowPreviewPath { get; set; }

        private bool CanDeferFlowSourcePreview()
        {
            if (CurrentRecipe == null || InspectCurrentFlow == null) return false;
            try
            {
                var settings = LoadLightSettings();
                if (!settings.HasRoi) return false;
                settings.EffectiveLimits();
                return true;
            }
            catch { return false; }
        }

        private void ShowCurrentSourcePreview()
        {
            if (string.IsNullOrWhiteSpace(CurrentTestImagePath) || !File.Exists(CurrentTestImagePath)) return;
            CurrentTestImageBitmap = LoadBitmapFromPath(CurrentTestImagePath);
            TestImageROI = System.Drawing.Rectangle.Empty;
            TestImageBrightnessROI = System.Drawing.Rectangle.Empty;
            TestImageSharpnessROI = System.Drawing.Rectangle.Empty;
            UpdateDisplayImage();
            UpdateDisplayRoiProperties();
        }

        [RelayCommand]
        private void ShowTestImageResults()
        {
            var results = new List<(int StationNumber, TestImageResultRow Result)>();
            if (CurrentRecipe != null)
            {
                results.AddRange(_multiStationResults
                    .Where(pair => pair.Key.RecipeId == CurrentRecipe.Id)
                    .SelectMany(pair => pair.Value.Results.Select(result =>
                        (StationNumber: pair.Key.Station, Result: result))));
                results.AddRange(_singleFlowDetails
                    .Where(pair => pair.Key.RecipeId == CurrentRecipe.Id)
                    .Select(pair => (StationNumber: pair.Key.Station, Result: pair.Value)));
            }
            results.AddRange(TestImageResults.Select(result =>
                (StationNumber: CurrentStationNumber, Result: result)));

            var distinctResults = results
                .GroupBy(item => item.StationNumber)
                .SelectMany(station => station.GroupBy(item => item.Result.FilePath,
                    StringComparer.OrdinalIgnoreCase).Select(image => image.Last()))
                .OrderBy(item => item.StationNumber)
                .ThenBy(item => item.Result.FileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (distinctResults.Length == 0)
            {
                SaveSettingsMessage = "目前配方各站還沒有可檢視的檢測結果";
                return;
            }

            var window = new TestImageResultsWindow(distinctResults, ResultsThumbnailMode,
                allStations: true);
            window.Owner = Application.Current.MainWindow;
            window.Show();
        }

        [RelayCommand]
        private void ShowTestImageResultsByStatus(string statusFilter)
        {
            ShowTestImageResults(statusFilter);
        }

        [RelayCommand]
        private void FilterTestImageFilesByStatus(string statusFilter)
        {
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting)
            {
                SaveSettingsMessage = "檢測進行中，完成後再切換圖片篩選。";
                return;
            }
            ApplyTestImageResultFilter(statusFilter, showMessage: true);
        }

        private void ApplyTestImageResultFilter(string statusFilter, bool showMessage)
        {
            string filter = string.Equals(statusFilter, "OK", StringComparison.OrdinalIgnoreCase) ? "OK"
                : string.Equals(statusFilter, "NG", StringComparison.OrdinalIgnoreCase) ? "NG" : "ALL";
            _resultListFilter = filter;
            var files = _allLoadedTestImageFiles.Where(path =>
            {
                var result = FindTestImageResult(path);
                if (result == null) return filter == "ALL" && !IsTestImageAnalysisComplete;
                return filter == "ALL" || (filter == "OK" ? result.IsOverallPass : !result.IsOverallPass);
            }).ToList();

            TestImageFiles.Clear();
            foreach (var path in files) TestImageFiles.Add(path);
            TestImageTree.Clear();
            if (files.Count == 0)
            {
                CurrentTestImageIndex = -1;
                CurrentTestImagePath = string.Empty;
                CurrentTestImageBitmap = null;
                TestImageInfo = $"{filter} 圖片 0 / 0";
                CanNavigatePrevious = false;
                CanNavigateNext = false;
                UpdateDisplayImage();
            }
            else
            {
                CurrentTestImageIndex = 0;
                ProcessCurrentTestImage();
            }

            int testedCount = files.Count(path => FindTestImageResult(path) != null);
            if (showMessage)
                SaveSettingsMessage = filter == "ALL"
                    ? $"已顯示全部圖片：{files.Count} 張（已判定 {testedCount} 張）"
                    : $"已篩選 {filter}：{testedCount} 張已判定圖片";
        }

        private void ShowTestImageResults(string statusFilter)
        {
            var singleResults = CurrentRecipe == null
                ? Enumerable.Empty<(int StationNumber, TestImageResultRow Result)>()
                : _singleFlowDetails
                    .Where(pair => pair.Key.RecipeId == CurrentRecipe.Id && pair.Key.Station == CurrentStationNumber)
                    .Select(pair => (StationNumber: pair.Key.Station, Result: pair.Value));
            var results = TestImageResults
                .Select(result => (StationNumber: CurrentStationNumber, Result: result))
                .Concat(singleResults)
                .GroupBy(item => item.Result.FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last()).ToArray();
            if (results.Length == 0)
            {
                SaveSettingsMessage = "目前還沒有可檢視的檢測結果";
                return;
            }

            var window = new TestImageResultsWindow(results, ResultsThumbnailMode, statusFilter);
            window.Owner = Application.Current.MainWindow;
            window.Show();
        }

        private BitmapImage LoadBitmapFromPath(string imagePath)
        {
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        [RelayCommand]
        private async Task PreviousTestImage()
        {
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting || IsNativeOperationBusy?.Invoke() == true)
            {
                SaveSettingsMessage = "圖片仍在檢測中，請稍候";
                return;
            }

            if (CanNavigatePrevious && CurrentTestImageIndex > 0)
            {
                string nextImage = TestImageFiles[CurrentTestImageIndex - 1];
                bool deferSourcePreview = CanDeferFlowSourcePreview() && FindTestImageResult(nextImage) == null;
                CurrentTestImageIndex--;
                ProcessCurrentTestImage(showSourcePreview: !deferSourcePreview);
                if (CurrentRecipe != null && InspectCurrentFlow != null)
                {
                    await InspectCurrentFlow();
                    if (deferSourcePreview && !string.Equals(LastFlowPreviewPath, CurrentTestImagePath, StringComparison.OrdinalIgnoreCase))
                        ShowCurrentSourcePreview();
                }
                else await AnalyzeCurrentTestImageAsync();
            }
        }

        [RelayCommand]
        private async Task NextTestImage()
        {
            if (IsAnalyzingTestImages || IsContinuousTestRunning || IsMultiStationTesting || IsNativeOperationBusy?.Invoke() == true)
            {
                SaveSettingsMessage = "圖片仍在檢測中，請稍候";
                return;
            }

            if (CanNavigateNext && CurrentTestImageIndex < TestImageFiles.Count - 1)
            {
                string nextImage = TestImageFiles[CurrentTestImageIndex + 1];
                bool deferSourcePreview = CanDeferFlowSourcePreview() && FindTestImageResult(nextImage) == null;
                CurrentTestImageIndex++;
                ProcessCurrentTestImage(showSourcePreview: !deferSourcePreview);
                if (CurrentRecipe != null && InspectCurrentFlow != null)
                {
                    await InspectCurrentFlow();
                    if (deferSourcePreview && !string.Equals(LastFlowPreviewPath, CurrentTestImagePath, StringComparison.OrdinalIgnoreCase))
                        ShowCurrentSourcePreview();
                }
                else await AnalyzeCurrentTestImageAsync();
            }
        }

        private async Task AnalyzeCurrentTestImageAsync()
        {
            if (CurrentProfile == null || CurrentTestImageIndex < 0 || CurrentTestImageIndex >= TestImageFiles.Count)
            {
                return;
            }

            string imagePath = CurrentTestImagePath;
            string profileName = GetProcessingProfileName();
            string profileBasePath = GetProcessingProfileBasePath();
            bool includeSharpness = IsSharpnessEnabled;
            double brightnessBaseValue = BrightnessBaseValue;

            IsAnalyzingTestImages = true;
            AnalysisProgressValue = 0;
            AnalysisProgressMaximum = 1;
            AnalysisProgressText = $"檢測中 1 / 1: {Path.GetFileName(imagePath)}";

            try
            {
                var result = await Task.Run(() =>
                {
                    var processor = new ImageProcessor(profileBasePath);
                    return AnalyzeSingleTestImage(processor, imagePath, profileName, includeSharpness, brightnessBaseValue);
                });

                AnalysisProgressValue = 1;
                AnalysisProgressText = $"已完成 {Path.GetFileName(imagePath)}";
                ApplyTestImageResult(result);
                LoadCurrentPreviewIfNeeded(result);
                UpdateDisplayRoiProperties();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"檢測目前圖片失敗: {ex.Message}");
                SaveSettingsMessage = $"檢測圖片失敗：{ex.Message}";
            }
            finally
            {
                IsAnalyzingTestImages = false;
            }
        }

        [RelayCommand]
        private void AddNewProfile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "圖片檔案|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有檔案|*.*",
                Multiselect = true  // 啟用多選
            };
            if ((bool)!dialog.ShowDialog())
            {
                return;
            }
            else
            {
                var files = dialog.FileNames;
                SelectedFiles.Clear();
                foreach (var file in files)
                {
                    SelectedFiles.Add(file);
                }
            }
            _windowService.ShowProfileAddWindow(SelectedFiles.ToList(), DetectionMode);
            RefreshProfiles();
        }

        public interface IWindowService
        {
            bool? ShowProfileDetailWindow(ProfileData profile, List<string> imagesFiles);  // 抽象視窗顯示行為
            bool? ShowProfileAddWindow(List<string> imagesFiles, string detectionMode, ProfileData? profileToReset = null, Action<ProfileData, string>? resetCompleted = null);  // 抽象視窗顯示行為
        }
        public class WindowService : IWindowService
        {
            public bool? ShowProfileDetailWindow(ProfileData profile, List<string> imagesFiles)
            {
                throw new NotImplementedException();

                var window = new ProfileDetailWindows();
                window.DataContext = new ProfileDetailViewModel(profile, imagesFiles);  // 傳遞資料
                return window.ShowDialog();  // 回傳 DialogResult
            }
            public bool? ShowProfileAddWindow(List<string> imagesFiles, string detectionMode, ProfileData? profileToReset = null, Action<ProfileData, string>? resetCompleted = null)
            {
                var window = new ProfileAddWindows();
                var services = new ServiceCollection();
                services.AddSingleton<ProfileAddViewModel.IWindowService, ProfileAddViewModel.WindowService>();
                services.AddTransient<ProfileAddViewModel>();
                
                var provider = services.BuildServiceProvider();
                // 設定主視窗 DataContext
                var vm = provider.GetRequiredService<ProfileAddViewModel>();
                if (profileToReset != null && resetCompleted != null)
                    vm.InitializeForReset(imagesFiles, detectionMode, profileToReset, resetCompleted);
                else
                    vm.Initialize(imagesFiles, detectionMode);
                window.DataContext = vm;
                return window.ShowDialog();


                //window.DataContext = new ProfileAddViewModel(imagesFiles);  // 傳遞資料
                //return window.ShowDialog();  // 回傳 DialogResult
            }
        }
    }
    public class ProfileMultiValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // 檢查是否有空值
            if (values == null || values.Length < 2)
                return "數據不完整";

            // 提取第一個值（Name）
            double? brightnessmin = values[0] as double?;  // 使用索引  和 as 轉換
            if (brightnessmin == null)
                brightnessmin = 0;

            // 提取第二個值（BrightnessMin）
            double? brightnessmax = values[1] as double?; // 使用索引  和 as 轉換
            if (brightnessmax == null)
                return $"{brightnessmax}: 亮度未設定";

            // 格式化輸出
            return $"亮度: {brightnessmin:F2} ~ {brightnessmax:F2}";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class PathToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || !(value is string path) || string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;  // 避免鎖定文件
                bitmap.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                bitmap.EndInit();
                bitmap.Freeze();  // 跨線程安全
                return bitmap;
            }
            catch
            {
                return null;  // 路徑無效時返回空
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public interface IFileDialogService
    {
        IEnumerable<string>? OpenImageFiles();
    }
    public class FileDialogService : IFileDialogService
    {
        public IEnumerable<string>? OpenImageFiles()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "圖片檔案|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有檔案|*.*",
                Multiselect = true  // 啟用多選
            };

            return dialog.ShowDialog() == true ? dialog.FileNames : null;
        }
    }
    

}
