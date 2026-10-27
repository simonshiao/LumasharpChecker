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
        public ProfileAddViewModel()
        {
        }

        public void Initialize(List<string> imagesFiles, string detectionMode = ProfileManager.InspectionMode.Brightness)
        {
            TargetImageFilePath = imagesFiles[0];
            _imagesFiles = imagesFiles;
            DetectionMode = string.IsNullOrWhiteSpace(detectionMode) ? ProfileManager.InspectionMode.Brightness : detectionMode;
        }

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
            }
            finally
            {
                //IsProcessing = false; // 恢复按钮状态
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
