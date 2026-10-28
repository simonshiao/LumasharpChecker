using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
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
namespace LumasharpChecker.viewmodel
{
    public class TestImageResultRow
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public double Brightness { get; set; }
        public double BrightnessDeltaPercent { get; set; }
        public double Sharpness { get; set; }
        public bool BrightnessSafe { get; set; }
        public bool SharpnessSafe { get; set; }
        public bool IncludeSharpness { get; set; } = true;
        public bool IsMatched { get; set; }
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
        public bool IsOverallPass => BrightnessSafe && (!IncludeSharpness || SharpnessSafe) && IsMatched;
        public string BrightnessResult => BrightnessSafe ? "OK" : "NG";
        public string BrightnessDeltaPercentText => double.IsNaN(BrightnessDeltaPercent) ? "N/A" : $"{BrightnessDeltaPercent:+0.00;-0.00;0.00}%";
        public string SharpnessResult => IncludeSharpness ? (SharpnessSafe ? "OK" : "NG") : "N/A";
        public string OverallResult => IsOverallPass ? "OK" : "NG";
        public string MatchResult => IsMatched ? "OK" : "NG";
        public string RoiDisplay => IncludeSharpness
            ? $"亮度 X:{BrightnessRoiX} Y:{BrightnessRoiY} W:{BrightnessRoiW} H:{BrightnessRoiH} / 銳利度 X:{SharpnessRoiX} Y:{SharpnessRoiY} W:{SharpnessRoiW} H:{SharpnessRoiH}"
            : $"亮度 X:{BrightnessRoiX} Y:{BrightnessRoiY} W:{BrightnessRoiW} H:{BrightnessRoiH}";
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
        private bool _isInitializing;
        private bool _isLoadingRecipe;

        // 對外暴露可綁定集合（無需手動觸發通知，ObservableCollection 自帶集合變化事件）
        public ObservableCollection<processInfo> ProcessList { get; } = new();

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
        private int _currentStationNumber = 1;

        [ObservableProperty]
        private string _stationStatusMessage = "請先開啟 Recipe 與設定，選擇或新增 Recipe。";

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
            // 如果有測試圖片，顯示測試圖片；否則顯示 Profile 範本圖片
            if (CurrentTestImageBitmap != null)
            {
                DisplayImage = CurrentTestImageBitmap;
            }
            else if (CurrentProfile?.Bitmap != null)
            {
                DisplayImage = CurrentProfile.Bitmap;
            }
            else
            {
                DisplayImage = null;
            }
        }


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
            _isInitializing = true;
            StationCount = recipeManager.LoadPreferences().StationCount;
            _isInitializing = false;

            var t = profileManager.LoadAllProfile();
            _items = new ObservableCollection<ProfileData>(t);
            Recipes = new ObservableCollection<RecipeManager.RecipeManifest>(recipeManager.LoadAllRecipes());
            if(_items.Count!=0)
                _selectedItem = _items[0];
            OnSelectionChanged();
            RefreshStationSlots();
        }

        [RelayCommand]
        private void CreateRecipe()
        {
            string name = Interaction.InputBox("輸入 Recipe 名稱。此 Recipe 會建立完整的站別骨架。", "建立 Recipe", "新 Recipe").Trim();
            if (string.IsNullOrWhiteSpace(name)) return;
            try
            {
                var recipe = recipeManager.CreateRecipe(name, StationCount);
                Recipes.Add(recipe);
                SelectedRecipe = recipe;
                LoadSelectedRecipe();
                SaveSettingsMessage = $"已建立 Recipe：{name}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "建立 Recipe 失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private void LoadSelectedRecipe()
        {
            if (SelectedRecipe == null) return;
            if (IsAnalyzingTestImages)
            {
                SaveSettingsMessage = "圖片仍在檢測中，請完成後再切換 Recipe";
                return;
            }

            if (CurrentProfile != null && CurrentRecipe != null) SaveCurrentInspectionSettings(reanalyzeTestImages: false);
            _isLoadingRecipe = true;
            CurrentRecipe = SelectedRecipe;
            StationCount = CurrentRecipe.StationCount is 8 or 10 ? CurrentRecipe.StationCount : StationCount;
            _isLoadingRecipe = false;
            CurrentStationNumber = 1;
            ResetTestImageState();
            LoadRecipeStation();
        }

        [RelayCommand]
        private void CloseRecipe()
        {
            if (CurrentProfile != null && CurrentRecipe != null) SaveCurrentInspectionSettings(reanalyzeTestImages: false);
            ResetTestImageState();
            CurrentRecipe = null;
            CurrentStationNumber = 1;
            RefreshStationSlots();
            LoadCurrentProfile(SelectedItem, isLibraryProfile: true);
            StationStatusMessage = "Recipe 已關閉；請開啟 Recipe 與設定。";
        }

        [RelayCommand]
        private void DeleteRecipe()
        {
            if (SelectedRecipe == null)
            {
                SaveSettingsMessage = "請先選擇 Recipe";
                return;
            }
            var result = MessageBox.Show($"確定要刪除 Recipe「{SelectedRecipe.Name}」及其站別資料？\n設定檔庫不會受到影響。", "刪除 Recipe", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (CurrentRecipe?.Id == SelectedRecipe.Id) CloseRecipe();
                recipeManager.DeleteRecipe(SelectedRecipe);
                Recipes.Remove(SelectedRecipe);
                SelectedRecipe = null;
                SaveSettingsMessage = "Recipe 已刪除";
            }
            catch (Exception ex)
            {
                SaveSettingsMessage = $"刪除 Recipe 失敗：{ex.Message}";
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
        }

        [RelayCommand]
        private void ApplyLibraryProfileToStation()
        {
            if (CurrentRecipe == null || SelectedItem == null) return;
            bool alreadyConfigured = CurrentRecipe.Stations.Single(station => station.Number == CurrentStationNumber).IsConfigured;
            if (alreadyConfigured && MessageBox.Show(
                    $"要以「{SelectedItem.Name}」取代目前第 {CurrentStationNumber:00} 站的 Recipe 設定嗎？",
                    "取代站別設定", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                CurrentProfile = recipeManager.AssignProfile(CurrentRecipe, CurrentStationNumber, SelectedItem);
                LoadCurrentProfile(CurrentProfile, isLibraryProfile: false);
                RefreshStationSlots();
                StationStatusMessage = $"第 {CurrentStationNumber:00} 站已套用「{SelectedItem.Name}」的獨立副本。";
                SaveSettingsMessage = "站別設定已加入 Recipe";
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
            try
            {
                var profile = recipeManager.LoadStation(CurrentRecipe, CurrentStationNumber);
                LoadCurrentProfile(profile, isLibraryProfile: false);
                RefreshStationSlots();
                StationStatusMessage = profile == null
                    ? $"第 {CurrentStationNumber:00} 站尚未設定；從設定檔庫選擇設定檔，再按「套用至本站」。"
                    : $"正在編輯 {CurrentRecipe.Name} 的第 {CurrentStationNumber:00} 站。"
                        + (CurrentRecipe.Stations.Single(station => station.Number == CurrentStationNumber).SourceProfileName is string sourceName
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
            var slots = new ObservableCollection<StationSlotViewModel>();
            for (int number = 1; number <= StationCount; number++)
            {
                bool configured = CurrentRecipe?.Stations.FirstOrDefault(station => station.Number == number)?.IsConfigured == true;
                if (configured && CurrentRecipe != null)
                {
                    string profilePath = Path.Combine(recipeManager.GetProfileBasePath(CurrentRecipe), RecipeManager.GetStationDirectoryName(number), "profile.json");
                    configured = File.Exists(profilePath);
                }
                slots.Add(new StationSlotViewModel(number, configured, CurrentRecipe != null && number == CurrentStationNumber));
            }
            StationSlots = slots;
        }

        private string GetProcessingProfileName() => CurrentRecipe == null
            ? CurrentProfile?.Name ?? string.Empty
            : RecipeManager.GetStationDirectoryName(CurrentStationNumber);

        private string GetProcessingProfileBasePath() => CurrentRecipe == null
            ? "profile"
            : recipeManager.GetProfileBasePath(CurrentRecipe);

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
        [ObservableProperty]
        private ObservableCollection<string> testImageFiles = new();
        [ObservableProperty]
        private int currentTestImageIndex = -1;
        [ObservableProperty]
        private string currentTestImagePath = string.Empty;
        [ObservableProperty]
        private string testImageInfo = string.Empty;
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
        private bool isAnalyzingTestImages;
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
        private bool isSettingsPanelOpen = false;
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
            if (TestImageFiles.Count > 0)
            {
                _ = AnalyzeTestImageFolderAsync();
                ProcessCurrentTestImage();
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
        public bool HasSelectedRecipe => SelectedRecipe != null;
        public bool CanApplyLibraryProfileToStation => CurrentRecipe != null && SelectedItem != null;
        public string CurrentInspectionName => CurrentRecipe == null
            ? CurrentProfile?.Name ?? "未選擇設定檔"
            : $"{CurrentRecipe.Name} / 第 {CurrentStationNumber:00} 站";
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
        }

        partial void OnSelectedRecipeChanged(RecipeManager.RecipeManifest? value) => OnPropertyChanged(nameof(HasSelectedRecipe));

        partial void OnCurrentRecipeChanged(RecipeManager.RecipeManifest? value)
        {
            OnPropertyChanged(nameof(IsRecipeActive));
            OnPropertyChanged(nameof(CurrentInspectionName));
            OnPropertyChanged(nameof(CanApplyLibraryProfileToStation));
            RefreshStationSlots();
        }

        partial void OnStationCountChanged(int value)
        {
            if (value is not (8 or 10)) return;
            if (!_isInitializing)
            {
                if (CurrentRecipe != null && !_isLoadingRecipe && CurrentProfile != null)
                    SaveCurrentInspectionSettings(reanalyzeTestImages: false);
                recipeManager.SavePreferences(new RecipeManager.AppPreferences { StationCount = value });
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
            OnPropertyChanged(nameof(CurrentInspectionName));
        }

        partial void OnCurrentStationNumberChanged(int value) => OnPropertyChanged(nameof(CurrentInspectionName));

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
            SaveCurrentInspectionSettings(reanalyzeTestImages: true);
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
            if (reanalyzeTestImages && TestImageFiles.Count > 0)
            {
                _ = AnalyzeTestImageFolderAsync();
                ProcessCurrentTestImage();
            }
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

        private void RefreshProfiles()
        {
            var profiles = profileManager.LoadAllProfile();
            Items = new ObservableCollection<ProfileData>(profiles);
            SelectedItem = Items.FirstOrDefault();
            CurrentTestImageBitmap = null;
            TestImageFiles.Clear();
            CurrentTestImageIndex = -1;
            CurrentTestImagePath = string.Empty;
            TestImageInfo = string.Empty;
            TestImageBrightness = 0;
            TestImageBrightnessDeltaPercent = double.NaN;
            TestImageSharpness = 0;
            TestImageROI = new System.Drawing.Rectangle();
            TestImageBrightnessROI = new System.Drawing.Rectangle();
            TestImageSharpnessROI = new System.Drawing.Rectangle();
            TestImageMatchFailed = string.Empty;
            TestImageResults.Clear();
            IsTestImageAnalysisComplete = false;
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
            if (CurrentProfile == null)
            {
                SaveSettingsMessage = "請先選擇 Profile";
                return;
            }

            if (TestImageFiles.Count == 0)
            {
                SaveSettingsMessage = "請先載入測試圖片";
                return;
            }

            SaveCurrentInspectionSettings(reanalyzeTestImages: false);
            if (!IsTestImageAnalysisComplete)
            {
                if (IsAnalyzingTestImages)
                {
                    SaveSettingsMessage = "圖片仍在檢測中，請稍候再匯出";
                    return;
                }

                SaveSettingsMessage = "請先完成測試圖片檢測";
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel 活頁簿|*.xlsx",
                FileName = $"{CurrentInspectionName.Replace('/', '_')}_檢測結果.xlsx",
                InitialDirectory = Path.GetDirectoryName(CurrentTestImagePath)
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var specRows = new List<string[]>
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
                new[] { "銳利度判定方式", GetLimitModeDisplay(SharpnessLimitMode) },
                new[] { "銳利度基準值", SharpnessBaseValue.ToString("F2", CultureInfo.InvariantCulture) },
                new[] { "銳利度下限偏差%", SharpnessPercentLower.ToString("F2", CultureInfo.InvariantCulture) },
                new[] { "銳利度固定下限", SharpnessFixedMin.ToString("F2", CultureInfo.InvariantCulture) },
                new[] { "銳利度有效下限", EffectiveSharpnessMin.ToString("F2", CultureInfo.InvariantCulture) },
                new[] { "匯出時間", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
            };

            try
            {
                if (File.Exists(dialog.FileName))
                {
                    File.Delete(dialog.FileName);
                }

                ExcelReportWriter.WriteTestResults(
                    dialog.FileName,
                    specRows,
                    TestImageResults,
                    IsSharpnessEnabled,
                    EffectiveBrightnessMin,
                    EffectiveBrightnessMax,
                    EffectiveSharpnessMin);
                SaveSettingsMessage = $"已匯出 {TestImageResults.Count} 筆檢測結果";
            }
            catch (Exception ex)
            {
                SaveSettingsMessage = $"匯出失敗: {ex.Message}";
            }
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
        private async Task TestImage()
        {
            if (CurrentProfile == null) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "圖片檔案|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有檔案|*.*",
                Multiselect = false
            };
            
            if ((bool)!dialog.ShowDialog())
            {
                return;
            }

            ResetTestImageState();

            // 取得選取的檔案路徑
            string selectedFilePath = dialog.FileName;
            string folderPath = Path.GetDirectoryName(selectedFilePath) ?? string.Empty;
            
            // 載入資料夾內所有圖片檔案
            await LoadTestImageFolder(folderPath, selectedFilePath);
        }

        [RelayCommand]
        private async Task TestSingleImage()
        {
            if (CurrentProfile == null) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "圖片檔案|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有檔案|*.*",
                Multiselect = false,
                Title = "選擇要單獨測試的圖片"
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

            ResetTestImageState();
            TestImageFiles.Add(dialog.FileName);
            CurrentTestImageIndex = 0;

            await AnalyzeTestImageFolderAsync();
            ProcessCurrentTestImage();
        }

        private void ResetTestImageState()
        {
            TestImageFiles.Clear();
            CurrentTestImageIndex = -1;
            CurrentTestImagePath = string.Empty;
            TestImageInfo = string.Empty;
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

        private async Task LoadTestImageFolder(string folderPath, string selectedFilePath)
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
                foreach (var file in files)
                {
                    TestImageFiles.Add(file);
                }

                // 設定當前選取的圖片索引
                CurrentTestImageIndex = Array.IndexOf(files, selectedFilePath);
                await AnalyzeTestImageFolderAsync();
                
                if (CurrentTestImageIndex >= 0)
                {
                    ProcessCurrentTestImage();
                }
            }
            catch (Exception ex)
            {
                // 處理錯誤，可以記錄到日誌
                System.Diagnostics.Debug.WriteLine($"載入測試圖片資料夾失敗: {ex.Message}");
            }
        }

        private async void AnalyzeTestImageFolder()
        {
            await AnalyzeTestImageFolderAsync();
        }

        private async Task AnalyzeTestImageFolderAsync()
        {
            if (IsAnalyzingTestImages)
            {
                return;
            }

            TestImageResults.Clear();
            IsTestImageAnalysisComplete = false;
            ResetTestImageSummary();

            if (CurrentProfile == null || TestImageFiles.Count == 0)
            {
                return;
            }

            var imageFiles = TestImageFiles.ToList();
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
                    BrightnessDeltaPercent = CalculateBrightnessDeltaPercent(result.Brightness, brightnessBaseValue),
                    Sharpness = result.Sharpness,
                    BrightnessSafe = result.BrightnessSafe,
                    SharpnessSafe = result.SharpnessSafe,
                    IncludeSharpness = includeSharpness,
                    IsMatched = result.IsMatched,
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
                    Message = $"處理失敗: {ex.Message}"
                };
            }
        }

        private void ResetTestImageSummary()
        {
            TotalTestImageCount = 0;
            BrightnessOkCount = 0;
            BrightnessNgCount = 0;
            SharpnessOkCount = 0;
            SharpnessNgCount = 0;
            OverallOkCount = 0;
            OverallNgCount = 0;
        }

        private void UpdateTestImageSummary()
        {
            TotalTestImageCount = TestImageResults.Count;
            BrightnessOkCount = TestImageResults.Count(result => result.BrightnessSafe);
            BrightnessNgCount = TotalTestImageCount - BrightnessOkCount;
            SharpnessOkCount = IsSharpnessEnabled ? TestImageResults.Count(result => result.SharpnessSafe) : 0;
            SharpnessNgCount = IsSharpnessEnabled ? TotalTestImageCount - SharpnessOkCount : 0;
            OverallOkCount = TestImageResults.Count(result => result.IsOverallPass);
            OverallNgCount = TotalTestImageCount - OverallOkCount;
        }

        private void ProcessCurrentTestImage()
        {
            if (CurrentTestImageIndex < 0 || CurrentTestImageIndex >= TestImageFiles.Count)
                return;

            CurrentTestImagePath = TestImageFiles[CurrentTestImageIndex];
            TestImageInfo = $"圖片 {CurrentTestImageIndex + 1} / {TestImageFiles.Count}";
            
            // 更新導航按鈕狀態
            CanNavigatePrevious = CurrentTestImageIndex > 0;
            CanNavigateNext = CurrentTestImageIndex < TestImageFiles.Count - 1;

            var cachedResult = TestImageResults.FirstOrDefault(result => string.Equals(result.FilePath, CurrentTestImagePath, StringComparison.OrdinalIgnoreCase));
            if (cachedResult != null)
            {
                ApplyTestImageResult(cachedResult);
                LoadCurrentPreviewIfNeeded(cachedResult);
                return;
            }

            try
            {
                ImageProcessor processor = CreateImageProcessor();
                var result = AnalyzeSingleTestImage(processor, CurrentTestImagePath);
                ApplyTestImageResult(result);
                LoadCurrentPreviewIfNeeded(result);
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
            }
        }

        private void ApplyTestImageResult(TestImageResultRow result)
        {
            TestImageBrightness = result.Brightness;
            TestImageBrightnessDeltaPercent = result.BrightnessDeltaPercent;
            TestImageSharpness = result.Sharpness;
            TestImageROI = new System.Drawing.Rectangle(result.RoiX, result.RoiY, result.RoiW, result.RoiH);
            TestImageBrightnessROI = new System.Drawing.Rectangle(result.BrightnessRoiX, result.BrightnessRoiY, result.BrightnessRoiW, result.BrightnessRoiH);
            TestImageSharpnessROI = new System.Drawing.Rectangle(result.SharpnessRoiX, result.SharpnessRoiY, result.SharpnessRoiW, result.SharpnessRoiH);
            BrightnessColor = result.BrightnessSafe ? Brushes.Green : Brushes.Red;
            SharpnessColor = !result.IncludeSharpness || result.SharpnessSafe ? Brushes.Green : Brushes.Red;
            TestImageMatchFailed = result.IsMatched ? string.Empty : "圖片匹配失敗 - 無法找到範本";
        }

        private void LoadCurrentPreviewIfNeeded(TestImageResultRow result)
        {
            try
            {
                CurrentTestImageBitmap = LoadBitmapFromPath(CurrentTestImagePath);
                UpdateDisplayImage();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"載入測試圖片顯示失敗: {ex.Message}");
                CurrentTestImageBitmap = null;
                UpdateDisplayImage();
            }
        }

        [RelayCommand]
        private void ShowTestImageResults()
        {
            if (TestImageFiles.Count == 0)
            {
                SaveSettingsMessage = "請先載入測試圖片";
                return;
            }

            if (!IsTestImageAnalysisComplete)
            {
                if (IsAnalyzingTestImages)
                {
                    SaveSettingsMessage = "圖片仍在檢測中，請稍候再檢視結果";
                    return;
                }

                SaveSettingsMessage = "請先完成測試圖片檢測";
                return;
            }

            var window = new TestImageResultsWindow(TestImageResults, ResultsThumbnailMode);
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
        private void PreviousTestImage()
        {
            if (CanNavigatePrevious && CurrentTestImageIndex > 0)
            {
                CurrentTestImageIndex--;
                ProcessCurrentTestImage();
            }
        }

        [RelayCommand]
        private void NextTestImage()
        {
            if (CanNavigateNext && CurrentTestImageIndex < TestImageFiles.Count - 1)
            {
                CurrentTestImageIndex++;
                ProcessCurrentTestImage();
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
            bool? ShowProfileAddWindow(List<string> imagesFiles, string detectionMode);  // 抽象視窗顯示行為
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
            public bool? ShowProfileAddWindow(List<string> imagesFiles, string detectionMode)
            {
                var window = new ProfileAddWindows();
                var services = new ServiceCollection();
                services.AddSingleton<ProfileAddViewModel.IWindowService, ProfileAddViewModel.WindowService>();
                services.AddTransient<ProfileAddViewModel>();
                
                var provider = services.BuildServiceProvider();
                // 設定主視窗 DataContext
                var vm = provider.GetRequiredService<ProfileAddViewModel>();
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
