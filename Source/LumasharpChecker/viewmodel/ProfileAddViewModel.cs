using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cyntec.Logger;
using LumasharpChecker.model;
using LumasharpChecker.view;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static LumasharpChecker.viewmodel.mainViewModel;
using static ProfileManager;

namespace LumasharpChecker.viewmodel
{
    public partial class ProfileAddViewModel : ObservableObject
    {
        [ObservableProperty]
        private string targetImageFilePath;

        private List<string> _imagesFiles;
        private ProfileData? _profileToReset;
        private Action<ProfileData, string>? _resetCompleted;
        public ProfileAddViewModel()
        {
        }

        public void Initialize(List<string> imagesFiles, string detectionMode = ProfileManager.InspectionMode.Brightness)
        {
            TargetImageFilePath = imagesFiles[0];
            _imagesFiles = imagesFiles;
            DetectionMode = string.IsNullOrWhiteSpace(detectionMode) ? ProfileManager.InspectionMode.Brightness : detectionMode;
            OnPropertyChanged(nameof(SampleCountText));
        }

        public string SampleCountText => $"已載入 {_imagesFiles?.Count ?? 0} 張基準圖片；計算將使用所有樣本，畫面僅預覽其中一張。";

        public void InitializeForReset(List<string> imagesFiles, string detectionMode, ProfileData profile, Action<ProfileData, string> resetCompleted)
        {
            Initialize(imagesFiles, detectionMode);
            _profileToReset = profile;
            _resetCompleted = resetCompleted;
            PatternMatchRoi = ToRect(profile.PatternMatchRoiX, profile.PatternMatchRoiY, profile.PatternMatchRoiW, profile.PatternMatchRoiH);
            BrightnessRoi = ToRect(profile.EffectiveBrightnessRoiX, profile.EffectiveBrightnessRoiY, profile.EffectiveBrightnessRoiW, profile.EffectiveBrightnessRoiH);
            MonitorRoi = BrightnessRoi;
            SharpnessRoi = ToRect(profile.EffectiveSharpnessRoiX, profile.EffectiveSharpnessRoiY, profile.EffectiveSharpnessRoiW, profile.EffectiveSharpnessRoiH);
        }

        private static System.Windows.Rect ToRect(int? x, int? y, int? width, int? height) =>
            new(x.GetValueOrDefault(), y.GetValueOrDefault(), width.GetValueOrDefault(), height.GetValueOrDefault());

        [ObservableProperty]
        private System.Windows.Rect _patternMatchRoi;

        [ObservableProperty]
        private System.Windows.Rect _monitorRoi;
        [ObservableProperty]
        private System.Windows.Rect _brightnessRoi;
        [ObservableProperty]
        private System.Windows.Rect _sharpnessRoi;

        [ObservableProperty]
        private string detectionMode = ProfileManager.InspectionMode.Brightness;

        partial void OnDetectionModeChanged(string value)
        {
            OnPropertyChanged(nameof(IsSharpnessEnabled));
        }

        public bool IsSharpnessEnabled => DetectionMode == ProfileManager.InspectionMode.BrightnessSharpness;

        [ObservableProperty]
        private double brightnessLowLimit;

        [ObservableProperty]
        private double brightnessHighLimit;

        [ObservableProperty]
        private double sharpnessLowLimit;

        [ObservableProperty]
        private double sharpnessHighLimit;

        [RelayCommand]
        private void ClearRoi()
        {

        }
//        [RelayCommand]
//        private void CalculateImages()
//        {
//            ImageProcessor ip = new ImageProcessor();
//            ip.roi = new System.Drawing.Rectangle((int)Roi.X, (int)Roi.Y, (int)Roi.Width, (int)Roi.Height);
//            var ar = ip.CalculateReferenceValues(_imagesFiles);

//            //_windowService.ShowStatisticResultWindow(ar);

//            SavingProfileDialog savingProfileDialog = new SavingProfileDialog();
//            if (savingProfileDialog.ShowDialog() ?? false)
//            {
//#warning MVVM pattern should br use
//                ProfileManager profileManager = new ProfileManager("profile");
//                profileManager.SaveProfile(savingProfileDialog.newPorfileName, ip.roi ?? new System.Drawing.Rectangle(), ar, _imagesFiles[0]);
                
//            }
//        }

        [RelayCommand] // 绑定按钮可用性
        private async Task CalculateImagesAsync(CancellationToken token)
        {
            if (PatternMatchRoi.Width == 0)
            {
                return;
            }
            if (BrightnessRoi.Width == 0)
            {
                return;
            }
            if (IsSharpnessEnabled && SharpnessRoi.Width == 0)
            {
                return;
            }

            try
            {
                ImageProcessor ip = new ImageProcessor();
                ip.patternmatchroi = new System.Drawing.Rectangle((int)PatternMatchRoi.X, (int)PatternMatchRoi.Y, (int)PatternMatchRoi.Width, (int)PatternMatchRoi.Height);
                ip.brightnessroi = new System.Drawing.Rectangle((int)BrightnessRoi.X, (int)BrightnessRoi.Y, (int)BrightnessRoi.Width, (int)BrightnessRoi.Height);
                ip.sharpnessroi = IsSharpnessEnabled
                    ? new System.Drawing.Rectangle((int)SharpnessRoi.X, (int)SharpnessRoi.Y, (int)SharpnessRoi.Width, (int)SharpnessRoi.Height)
                    : ip.brightnessroi;
                ip.monitorroi = ip.brightnessroi;
                var ar = await ip.CalculateReferenceValues(_imagesFiles);
                if (ar == null) return;
                int successCount = ar.BrightnessValues?.Count ?? 0;
                if (successCount < 2)
                {
                    System.Windows.MessageBox.Show($"只有 {successCount} 張圖片成功完成定位與亮度計算，至少需要 2 張。請調整 ROI 或重選圖片資料夾。",
                        "有效樣本不足", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                if (successCount < _imagesFiles.Count)
                {
                    if (System.Windows.MessageBox.Show(
                        $"選取 {_imagesFiles.Count} 張，其中只有 {successCount} 張完成計算，其餘圖片可能定位失敗。\n要以 {successCount} 張成功樣本的統計值繼續嗎？",
                        "有圖片未成功計算", System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes)
                        return;
                }

//                _windowService.ShowStatisticResultWindow(ar);

//                SavingProfileDialog savingProfileDialog = new SavingProfileDialog();
//                if (savingProfileDialog.ShowDialog() ?? false)
//                {
//#warning MVVM pattern should be use
//                    ProfileManager profileManager = new ProfileManager("profile");
//                    profileManager.SaveProfile(savingProfileDialog.newPorfileName, ip.patternmatchroi ?? new System.Drawing.Rectangle(),
//                        ip.monitorroi ?? new System.Drawing.Rectangle(), ar, _imagesFiles[0], ar.EnableDynamicThreshold, ar.DynamicThresholdCount);
//                    //ProfileManager.Instance.SaveProfile(savingProfileDialog.newPorfileName, ip.patternmatchroi ?? new System.Drawing.Rectangle(),
//                    //    ip.monitorroi ?? new System.Drawing.Rectangle(), ar, _imagesFiles[0]);

//                }
                var vm = _windowService.ShowStatisticResultWindow(ar);
                if (vm == null) return;

                if (_profileToReset != null && _resetCompleted != null)
                {
                    ApplyAnalysisResult(_profileToReset, ar);
                    _resetCompleted(_profileToReset, _imagesFiles[0]);
                    return;
                }

                SavingProfileDialog savingProfileDialog = new SavingProfileDialog();
                if (savingProfileDialog.ShowDialog() ?? false)
                {
                    ProfileManager profileManager = new ProfileManager("profile");
                    profileManager.SaveProfile(savingProfileDialog.newPorfileName,
                        ip.patternmatchroi ?? new System.Drawing.Rectangle(),
                        ip.brightnessroi ?? new System.Drawing.Rectangle(),
                        ip.sharpnessroi ?? new System.Drawing.Rectangle(),
                        ar, _imagesFiles[0], false, 0, DetectionMode);
                }
            }
            catch (OperationCanceledException)
            {
                Logmanager.loger.Warn("用户取消操作");
            }
            catch (Exception ex)
            {
                Logmanager.loger.Error(ex, "图片计算失败");
                if (_resetCompleted != null)
                    System.Windows.MessageBox.Show("參數計算或儲存失敗：" + ex.Message,
                        "設定檔未完成", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally
            {
                //IsProcessing = false; // 恢复按钮状态
            }
        }

        private void ApplyAnalysisResult(ProfileData profile, AnalysisResult result)
        {
            profile.BrightnessMax = result.Brightness.ThreeSigmaUpper;
            profile.BrightnessMin = result.Brightness.ThreeSigmaLower;
            profile.BrightnessMean = result.Brightness.Mean;
            profile.BrightnessSigma = result.Brightness.Sigma;
            profile.BrightnessValues = result.BrightnessValues;
            profile.BrightnessPercentileLower = result.Brightness.Percentile?.Lower;
            profile.BrightnessPercentileUpper = result.Brightness.Percentile?.Upper;
            profile.BrightnessBaseValue = result.Brightness.Mean;
            profile.DetectionMode = DetectionMode;
            profile.SharpnessMin = result.Sharpness?.Percentile?.Lower;
            profile.SharpnessMax = result.Sharpness?.Percentile?.Upper;
            profile.SharpnessMean = result.Sharpness?.Mean;
            profile.SharpnessSigma = result.Sharpness?.Sigma;
            profile.SharpnessValues = result.SharpnessValues;
            profile.SharpnessPercentileLower = result.Sharpness?.Percentile?.Lower;
            profile.SharpnessPercentileUpper = result.Sharpness?.Percentile?.Upper;
            profile.SharpnessBaseValue = result.Sharpness?.Mean;
            profile.BrightnessLimitMode = ProfileManager.LimitMode.Percent;
            profile.BrightnessPercentLower = 10;
            profile.BrightnessPercentUpper = 10;
            profile.BrightnessFixedMin = profile.BrightnessMin;
            profile.BrightnessFixedMax = profile.BrightnessMax;
            profile.SharpnessLimitMode = ProfileManager.LimitMode.Percent;
            profile.SharpnessPercentLower = 10;
            profile.SharpnessFixedMin = profile.SharpnessMin;
            profile.PatternMatchRoiX = (int)PatternMatchRoi.X;
            profile.PatternMatchRoiY = (int)PatternMatchRoi.Y;
            profile.PatternMatchRoiW = (int)PatternMatchRoi.Width;
            profile.PatternMatchRoiH = (int)PatternMatchRoi.Height;
            profile.BrightnessRoiX = (int)BrightnessRoi.X;
            profile.BrightnessRoiY = (int)BrightnessRoi.Y;
            profile.BrightnessRoiW = (int)BrightnessRoi.Width;
            profile.BrightnessRoiH = (int)BrightnessRoi.Height;
            profile.MonitorRoiX = profile.BrightnessRoiX;
            profile.MonitorRoiY = profile.BrightnessRoiY;
            profile.MonitorRoiW = profile.BrightnessRoiW;
            profile.MonitorRoiH = profile.BrightnessRoiH;
            profile.SharpnessRoiX = (int)SharpnessRoi.X;
            profile.SharpnessRoiY = (int)SharpnessRoi.Y;
            profile.SharpnessRoiW = (int)SharpnessRoi.Width;
            profile.SharpnessRoiH = (int)SharpnessRoi.Height;
            profile.EnableDynamicThreshold = false;
            profile.DynamicThresholdCount = 0;
            profile.ModifyDateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            if (result.Brightness.Sigma < 1.0)
            {
                profile.BrightnessMin = result.Brightness.Mean - 3;
                profile.BrightnessMax = result.Brightness.Mean + 3;
            }
        }

        private readonly IWindowService _windowService;
        public ProfileAddViewModel(IWindowService windowService)
        {
            _windowService = windowService;
        }
        public interface IWindowService
        {
            //bool? ShowStatisticResultWindow(model.AnalysisResult ar);  // 抽象視窗顯示行為
            StatisticResultModel ShowStatisticResultWindow(AnalysisResult ar);
        }
        public class WindowService : IWindowService
        {
            public StatisticResultModel ShowStatisticResultWindow(AnalysisResult ar)
            {
                var vm = new StatisticResultModel();
                vm.Initialize(ar);

                var window = new StatisticResultWindow(vm);
                window.DataContext = vm;
                var result = window.ShowDialog();

                return result == true ? vm : null;
            }
            //public bool? ShowStatisticResultWindow(model.AnalysisResult ar)
            //{
            //    //var services = new ServiceCollection();
            //    //var provider = services.BuildServiceProvider();

            //    //services.AddSingleton<StatisticResultModel.IWindowService, StatisticResultModel.WindowService>();
            //    //services.AddTransient<StatisticResultModel>();

            //    // 設定主視窗 DataContext
            //    //var vm = provider.GetRequiredService<StatisticResultModel>();
            //    //vm.Initialize(imagesFiles);

            //    var vm = new StatisticResultModel();
            //    vm.Initialize(ar);
            //    var window = new StatisticResultWindow();
            //    window.DataContext = vm;
            //    return window.ShowDialog();

            //}
        }
    }
}
