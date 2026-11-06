using LumasharpChecker.viewmodel;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using LumasharpChecker.model;
using LumasharpChecker.view;

namespace LumasharpChecker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _sidebarCollapsed;
        private bool _nativeFlowBusy;
        private bool _nativeEditorOpen;
        private bool _stopFlow;
        private bool _restoringFlowSelection;

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_nativeEditorOpen || _nativeFlowBusy || (DataContext is mainViewModel closingVm && closingVm.IsContinuousTestRunning))
            {
                e.Cancel = true;
                MessageBox.Show("請先關閉原生參數視窗，並等待配方儲存完成。", "參數儲存中",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            base.OnClosing(e);
        }
        private string _lastSettingsMessage = "";
        private System.ComponentModel.INotifyPropertyChanged? _messageSource;

        public MainWindow()
        {
            InitializeComponent();
            AoiWorkflow.PipelineStatusChanged += message => AppendAoiMessage("流程", message);
            DataContextChanged += (_, _) => SubscribeAoiMessages();
            SubscribeAoiMessages();
            AppendAoiMessage("系統", "AOI 訊息紀錄已啟動");

            AoiWorkflow.LocatorRequested += OpenAoiLocator_Click;
            AoiWorkflow.SelectWayRequested += (_, _) => OpenAoiNativeEditor("選向");
            AoiWorkflow.SelectFaceRequested += (_, _) => OpenAoiNativeEditor("選面");
            AoiWorkflow.LocatorEnabledChanged += (_, _) => SaveFlowSelectionFromPanel();
            AoiWorkflow.SelectWayEnabledChanged += (_, _) => SaveFlowSelectionFromPanel();
            AoiWorkflow.SelectFaceEnabledChanged += (_, _) => SaveFlowSelectionFromPanel();
            AoiWorkflow.LightEnabledChanged += (_, _) => SaveFlowSelectionFromPanel();
            AoiWorkflow.LightRequested += OpenAoiLight_Click;
            AoiWorkflow.ContinuousRequested += async (_, _) => await RunContinuousFlowAsync();
            AoiWorkflow.ResultsRequested += (_, _) =>
            {
                if (DataContext is mainViewModel vm) vm.ShowTestImageResultsCommand.Execute(null);
            };
            AoiWorkflow.CollapseRequested += (_, _) => SetAoiPanelExpanded(false);

            Version? version = Assembly.GetEntryAssembly().GetName().Version;
            this.Title += $" {version?.ToString()}";
            // 首頁預設為 Chao8Side 風格的極簡看板，設定工具收在左側導覽。
            ToggleSidebar_Click(this, new RoutedEventArgs());
            ShowMainPage(true);
        }

        private void SubscribeAoiMessages()
        {
            if (_messageSource is mainViewModel previousVm)
            {
                previousVm.InspectCurrentFlow = null;
                previousVm.IsNativeOperationBusy = null;
            }
            if (_messageSource != null) _messageSource.PropertyChanged -= OnAoiSettingsChanged;
            _messageSource = DataContext as System.ComponentModel.INotifyPropertyChanged;
            if (_messageSource != null) _messageSource.PropertyChanged += OnAoiSettingsChanged;
            if (_messageSource is mainViewModel vm)
            {
                vm.InspectCurrentFlow = () => RunNativeRetestAsync(quiet: true);
                vm.IsNativeOperationBusy = () => _nativeFlowBusy || _nativeEditorOpen;
                RestoreFlowSelection(vm);
            }
        }

        private void RestoreFlowSelection(mainViewModel vm)
        {
            if (vm.CurrentRecipe == null) return;
            _restoringFlowSelection = true;
            try
            {
                string selection = vm.LoadFlowSelection();
                AoiWorkflow.SetToolSelection(selection, vm.LoadLightInspectionEnabled());
                vm.LightFlowSelection = selection;
            }
            catch (Exception ex)
            {
                AoiWorkflow.SetPipelineStatus("無法載入流程勾選設定：" + ex.GetBaseException().Message);
            }
            finally { _restoringFlowSelection = false; }
        }

        private void SaveFlowSelectionFromPanel()
        {
            if (_restoringFlowSelection) return;
            if (DataContext is not mainViewModel vm || vm.CurrentRecipe == null) return;
            if (_nativeFlowBusy || _nativeEditorOpen || vm.IsContinuousTestRunning ||
                vm.IsAnalyzingTestImages || vm.IsMultiStationTesting)
            {
                RestoreFlowSelection(vm);
                return;
            }

            string selection = (AoiWorkflow.IsLocatorEnabled ? "1" : "0") +
                (AoiWorkflow.IsSelectWayEnabled ? "1" : "0") +
                (AoiWorkflow.IsSelectFaceEnabled ? "1" : "0");
            try
            {
                vm.SaveFlowSelection(selection);
                vm.SaveLightInspectionEnabled(AoiWorkflow.IsLightInspectionEnabled);
                vm.LightFlowSelection = selection;
                AoiWorkflow.ClearNativeResults();
                AoiWorkflow.SetPipelineStatus("流程工具設定已儲存至目前 Recipe／站別。");
            }
            catch (Exception ex)
            {
                RestoreFlowSelection(vm);
                AoiWorkflow.SetPipelineStatus("流程工具設定儲存失敗：" + ex.GetBaseException().Message);
            }
        }

        private void OnAoiSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if ((e.PropertyName == nameof(mainViewModel.CurrentStationNumber) ||
                 e.PropertyName == nameof(mainViewModel.CurrentRecipe)) &&
                DataContext is mainViewModel selectionVm)
                RestoreFlowSelection(selectionVm);

            if (e.PropertyName == nameof(mainViewModel.CurrentTestImagePath) ||
                e.PropertyName == nameof(mainViewModel.CurrentStationNumber) ||
                e.PropertyName == nameof(mainViewModel.CurrentRecipe))
            {
                if (e.PropertyName == nameof(mainViewModel.CurrentTestImagePath) && DataContext is mainViewModel batchVm && batchVm.IsContinuousTestRunning) return;
                if (Dispatcher.CheckAccess()) AoiWorkflow.ClearNativeResults();
                else Dispatcher.BeginInvoke(new Action(AoiWorkflow.ClearNativeResults));
            }
            if (e.PropertyName != "SaveSettingsMessage") return;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => OnAoiSettingsChanged(sender, e)));
                return;
            }
            if (DataContext is mainViewModel vm)
            {
                string message = vm.SaveSettingsMessage;
                if (message == _lastSettingsMessage) return;
                _lastSettingsMessage = message;
                AppendAoiMessage("Luma", message);
            }
        }

        // 原生 Host 的 FAIL／定位失敗等結果可由同一入口追加。
        private void AppendAoiMessage(string source, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            string entry = "[" + DateTime.Now.ToString("HH:mm:ss") + "][" + source + "] " + message.Trim();
            AoiMessageLog.AppendText(entry + Environment.NewLine);
            if (AoiMessageLog.Text.Length > 24000)
                AoiMessageLog.Text = AoiMessageLog.Text.Substring(AoiMessageLog.Text.Length - 16000);
            AoiMessageLog.ScrollToEnd();
        }

        private void CopyAoiMessages_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(AoiMessageLog.Text))
                Clipboard.SetText(AoiMessageLog.Text);
        }

        private void ShowOverview_Click(object sender, RoutedEventArgs e) => ShowMainPage(true);

        private void ShowAoiTools_Click(object sender, RoutedEventArgs e)
        {
            ShowMainPage(false);
            SetAoiPanelExpanded(true);
            AoiToolsNavButton.Tag = "Active";
            AoiToolsNavButton.Background = (System.Windows.Media.Brush)FindResource("BlueSoft");
            AoiToolsNavButton.BorderBrush = (System.Windows.Media.Brush)FindResource("Blue");
            // AOI 預設沿用目前站別 OK DB，不用每次開檔案選擇視窗。
            if (DataContext is mainViewModel vm && vm.CurrentRecipe != null)
                vm.EnsureAoiAllDatabase();
        }

        private void OpenAoiPanel_Click(object sender, RoutedEventArgs e) => SetAoiPanelExpanded(true);

        private void SetAoiPanelExpanded(bool expanded)
        {
            InspectionSideColumn.Width = new GridLength(expanded ? 322 : 44);
            AoiWorkflow.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            AoiPanelReopen.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OpenAoiLocator_Click(object? sender, EventArgs e)
        {
            if (!AoiWorkflow.IsLocatorEnabled)
            {
                MessageBox.Show("請先勾選定位。", "定位", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            OpenAoiNativeEditor("定位");
        }

        // AOI 圖示負責參數編輯；原生執行器仍由光源流程呼叫。
        // 使用已驗證的 R3 隔離 Recipe，避免直接寫入原廠來源。
        private async void OpenAoiNativeEditor(string label)
        {
            if (_nativeFlowBusy) return;
            if (DataContext is not mainViewModel vm || vm.CurrentRecipe == null)
            {
                MessageBox.Show("請先載入配方。", label, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (vm.IsAnalyzingTestImages || vm.IsContinuousTestRunning || vm.IsMultiStationTesting)
            {
                MessageBox.Show("檢測進行中，請先完成再調整工具。", label,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string recipeId = vm.CurrentRecipe.Id;
            int editedStation = vm.CurrentStationNumber;
            string editedImage = vm.CurrentTestImagePath;
            _nativeFlowBusy = true;
            _nativeEditorOpen = true;
            try
            {
                // 新配方的初始定位 ROI 以 t1 第一站為基準；每站第一次進入定位編輯器時，
                // 依本站目前選取的圖片尺寸縮放並保存，再建立隔離 Recipe 副本。
                if (label == "定位") vm.ApplyInitialRoiScaleForCurrentImage();

                NativeRecipeBridge.PreparedRecipe ready;
                try
                {
                    ready = NativeRecipeBridge.Prepare(vm.CurrentRecipe, AppDomain.CurrentDomain.BaseDirectory);
                }
                catch (InvalidDataException ex) when (ex.Message.Contains("與 Luma 快照不一致"))
                {
                    NativeRecipeBridge.RefreshNativeCopy(vm.CurrentRecipe, AppDomain.CurrentDomain.BaseDirectory);
                    AoiWorkflow.SetPipelineStatus("原生 Recipe 已自動更新至最新匯入版本；副本校驗完成。");
                    ready = NativeRecipeBridge.Prepare(vm.CurrentRecipe, AppDomain.CurrentDomain.BaseDirectory);
                }
                await NativeRecipeBridge.OpenSandbox(ready, vm.CurrentStationNumber, label == "定位",
                    vm.TestImageFiles, vm.CurrentTestImagePath, label == "選向" ? "way" :
                    label == "選面" ? "face" : null);
                AoiWorkflow.SetPipelineStatus("原生「" + label + "」參數已儲存至本機配方。");
                await RetestSavedImageAsync(vm, recipeId, editedStation, editedImage);
            }
            catch (Exception ex)
            {
                AoiWorkflow.SetPipelineStatus("無法開啟原生 " + label + " 設定：" + ex.GetBaseException().Message);
                MessageBox.Show(ex.GetBaseException().Message, label + "設定",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { _nativeEditorOpen = false; _nativeFlowBusy = false; }
        }

        private async System.Threading.Tasks.Task InspectNativeStageAsync(string step, string label)
        {
            if (_nativeFlowBusy) return;
            if (DataContext is not mainViewModel vm || vm.CurrentRecipe == null)
            {
                MessageBox.Show("請先載入配方。", label, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (vm.IsAnalyzingTestImages || vm.IsContinuousTestRunning || vm.IsMultiStationTesting)
            {
                MessageBox.Show("檢測進行中，請先完成再使用工具。", label,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (string.IsNullOrWhiteSpace(vm.CurrentTestImagePath) || !File.Exists(vm.CurrentTestImagePath))
                vm.EnsureAoiAllDatabase(showError: true);
            if (string.IsNullOrWhiteSpace(vm.CurrentTestImagePath) || !File.Exists(vm.CurrentTestImagePath))
            {
                MessageBox.Show("本站 ALL DB（或 OK／NG 圖庫）沒有可用圖片，無法執行原生工具。", label,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var recipe = vm.CurrentRecipe;
            int station = vm.CurrentStationNumber;
            string imagePath = vm.CurrentTestImagePath;
            _nativeFlowBusy = true;
            try
            {
                AoiWorkflow.SetPipelineStatus("正在執行原生非 AI " + label + "…");
                var native = await NativeRecipeBridge.RunPreLightAsync(
                    recipe, station, imagePath, AppDomain.CurrentDomain.BaseDirectory, step);
                if (vm.CurrentRecipe?.Id != recipe.Id ||
                    vm.CurrentStationNumber != station || vm.CurrentTestImagePath != imagePath)
                    throw new InvalidOperationException("站別、配方或圖片已變更，本次結果作廢。");
                AoiWorkflow.SetPipelineStatus(native.Summary);
                string state = step == "way" ? native.SelectWay
                    : step == "face" ? native.SelectFace : native.Locate;
                if (state == "SKIPPED_RECIPE_OFF")
                {
                    MessageBox.Show("第 " + station + " 站原廠配方未啟用「" + label +
                        "」，已依原廠設定略過，未執行也未變更參數。\n" + native.Summary,
                        label, MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                AoiWorkflow.SetPipelineStatus("原生 " + label + " 未通過：" + ex.GetBaseException().Message);
                MessageBox.Show("原生 " + label + " 未通過：\n" + ex.GetBaseException().Message,
                    label, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { _nativeFlowBusy = false; }
        }

        private async System.Threading.Tasks.Task RetestSavedImageAsync(mainViewModel vm, string recipeId, int station, string image)
        {
            if (vm.CurrentRecipe?.Id != recipeId || vm.CurrentStationNumber != station || vm.CurrentTestImagePath != image)
            {
                AoiWorkflow.SetPipelineStatus("設定已儲存；配方、站別或圖片已切換，未將舊圖結果套用至目前畫面。");
                return;
            }
            // Saving has finished. Transfer the busy state to the existing single-image runner
            // synchronously, before its first await; it updates preview, tools and detail results.
            _nativeEditorOpen = false;
            _nativeFlowBusy = false;
            AoiWorkflow.SetPipelineStatus("設定已儲存，正在複測目前圖片…");
            await RunNativeRetestAsync(quiet: true);
        }

        private async System.Threading.Tasks.Task RunNativeRetestAsync(bool continuous = false, bool quiet = false)
        {
            if (_nativeFlowBusy || _nativeEditorOpen) return;
            void Unavailable(string message)
            {
                AoiWorkflow.SetPipelineStatus(message);
                if (!quiet) MessageBox.Show(message, "重新檢測", MessageBoxButton.OK, MessageBoxImage.Information);
                if (continuous) _stopFlow = true;
            }
            if (DataContext is not mainViewModel vm || vm.CurrentRecipe == null)
            {
                Unavailable("請先載入配方。");
                return;
            }
            if (vm.IsAnalyzingTestImages || (!continuous && vm.IsContinuousTestRunning) || vm.IsMultiStationTesting)
            {
                Unavailable("檢測進行中，請先完成或停止。");
                return;
            }
            string image = vm.CurrentTestImagePath;
            if (string.IsNullOrWhiteSpace(image) || !File.Exists(image))
            {
                Unavailable("請先載入要檢測的圖片。");
                return;
            }
            var recipe = vm.CurrentRecipe;
            int station = vm.CurrentStationNumber;
            bool locatorEnabled = AoiWorkflow.IsLocatorEnabled;
            bool lightInspectionEnabled = AoiWorkflow.IsLightInspectionEnabled;
            if (!locatorEnabled && !lightInspectionEnabled)
            {
                Unavailable("請至少勾選「定位」或「光源檢測」。");
                return;
            }
            string selection = locatorEnabled
                ? "1" + (AoiWorkflow.IsSelectWayEnabled ? "1" : "0") + (AoiWorkflow.IsSelectFaceEnabled ? "1" : "0")
                : "000";
            _nativeFlowBusy = true;
            AoiWorkflow.ClearNativeResults();
            try
            {
                AoiWorkflow.SetPipelineStatus(!locatorEnabled
                    ? "定位未勾選：使用整張圖的光源 ROI 檢測…"
                    : lightInspectionEnabled
                        ? "正在檢測目前圖片：定位 → 選向 → 選面 → 光源…"
                        : "正在檢測目前圖片：定位 → 選向 → 選面…（光源略過）");
                var report = await vm.InspectFlowAsync(image, selection, accumulate: continuous,
                    lightInspectionEnabled: lightInspectionEnabled);
                if (vm.CurrentRecipe?.Id != recipe.Id || vm.CurrentStationNumber != station || vm.CurrentTestImagePath != image)
                    throw new InvalidOperationException("站別、配方或圖片已變更，本次結果作廢。");
                AoiWorkflow.ShowNativeResults(report);
                AoiWorkflow.SetPipelineStatus(report.Summary +
                    (string.IsNullOrWhiteSpace(report.Error) ? "" : "；" + report.Error));
                if (!string.IsNullOrWhiteSpace(report.SelectWayDetail))
                    AoiWorkflow.SetPipelineStatus("選向資訊：" + report.SelectWayDetail.Trim());
                if (report.Exit != 0 && !continuous && !quiet)
                    MessageBox.Show(report.Summary + (string.IsNullOrWhiteSpace(report.Error) ? "" : "\n" + report.Error),
                        "原生流程未通過", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AoiWorkflow.SetPipelineStatus("原生流程無法完成：" + ex.GetBaseException().Message);
                if (!continuous && !quiet) MessageBox.Show(ex.GetBaseException().Message, "原生流程失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (continuous) _stopFlow = true;
            }
            finally { _nativeFlowBusy = false; }
        }

        private async void StartFlowContinuous_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not mainViewModel vm) return;
            if (vm.CurrentRecipe != null) await RunContinuousFlowAsync();
            else vm.StartContinuousTestCommand.Execute(null);
        }

        private async System.Threading.Tasks.Task RunContinuousFlowAsync()
        {
            if (DataContext is not mainViewModel vm) return;
            if (vm.IsContinuousTestRunning) { _stopFlow = true; return; }
            if (_nativeFlowBusy || _nativeEditorOpen || vm.IsAnalyzingTestImages || vm.IsMultiStationTesting) return;
            string? batchRecipe = null; int station = 0, done = 0, count = 0;
            NativeRecipeBridge.StageReport? last = null;
            var batch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (vm.CurrentRecipe == null || vm.TestImageFiles.Count == 0) throw new InvalidOperationException("請先載入配方與圖庫。");
                bool lightInspectionEnabled = AoiWorkflow.IsLightInspectionEnabled;
                bool locatorEnabled = AoiWorkflow.IsLocatorEnabled;
                if (!locatorEnabled && !lightInspectionEnabled)
                    throw new InvalidOperationException("請至少勾選「定位」或「光源檢測」。");
                if (lightInspectionEnabled)
                {
                    var settings = vm.LoadLightSettings();
                    bool hasRoi = locatorEnabled ? settings.HasRoi : settings.HasFullImageRoi;
                    if (!hasRoi) throw new InvalidOperationException(locatorEnabled
                        ? "請先在「光源檢測」設定定位後 ROI 並儲存。"
                        : "請先在「光源檢測」以整張圖模式設定 ROI 並儲存。");
                    settings.EffectiveLimits(fullImage: !locatorEnabled);
                }
                var files = vm.TestImageFiles.ToArray(); count = files.Length;
                batchRecipe = vm.CurrentRecipe.Id; station = vm.CurrentStationNumber;
                string selection = locatorEnabled
                    ? "1" + (AoiWorkflow.IsSelectWayEnabled ? "1" : "0") + (AoiWorkflow.IsSelectFaceEnabled ? "1" : "0")
                    : "000";
                vm.BeginContinuousFlow();
                _stopFlow = false; vm.IsContinuousTestRunning = true; AoiWorkflow.SetContinuousRunning(true);
                AoiWorkflow.ClearNativeResults();
                AoiWorkflow.SetPipelineStatus(locatorEnabled
                    ? "連續檢測開始：過程保留目前影像，結束後顯示最後一張。"
                    : "連續光源檢測開始：依整張圖 ROI 檢測，結束後顯示最後一張。");
                foreach (string file in files)
                {
                    if (_stopFlow || vm.CurrentRecipe?.Id != batchRecipe || vm.CurrentStationNumber != station) break;
                    // Do not switch/preview the input image or update the result list per frame.
                    _nativeFlowBusy = true;
                    try { last = await vm.InspectFlowAsync(file, selection, accumulate: true,
                        deferPresentation: true, lightInspectionEnabled: lightInspectionEnabled); }
                    finally { _nativeFlowBusy = false; }
                    done++;
                    AoiWorkflow.SetContinuousProgress(done, files.Length);
                }
            }
            catch (Exception ex)
            {
                _stopFlow = true;
                AoiWorkflow.SetPipelineStatus("連續檢測中止：" + ex.GetBaseException().Message);
                MessageBox.Show(ex.GetBaseException().Message, "連續檢測", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                bool sameContext = batchRecipe != null && vm.CurrentRecipe?.Id == batchRecipe && vm.CurrentStationNumber == station;
                try
                {
                    double displayMs = vm.FinishContinuousFlow(sameContext);
                    if (sameContext && last != null) AoiWorkflow.ShowNativeResults(last);
                    if (batchRecipe != null)
                    {
                        vm.IsTestImageAnalysisComplete = sameContext && !_stopFlow && done == count;
                        batch.Stop();
                        AoiWorkflow.SetPipelineStatus($"連續流程{(_stopFlow || !sameContext ? "已停止" : "結束")}：{done}/{count} 張，共 {batch.Elapsed.TotalMilliseconds:F0} ms；最後畫面更新 {displayMs:F1} ms。");
                    }
                }
                finally { vm.IsContinuousTestRunning = false; AoiWorkflow.SetContinuousRunning(false); }
            }
        }

        private async void ConfigureAllStandards_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not mainViewModel vm || vm.CurrentRecipe == null) return;
            if (_nativeFlowBusy || _nativeEditorOpen || vm.IsAnalyzingTestImages ||
                vm.IsContinuousTestRunning || vm.IsMultiStationTesting)
            {
                MessageBox.Show("請先完成檢測或關閉參數工具。", "批次標準件", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string recipeId = vm.CurrentRecipe.Id;
            string recipeName = vm.CurrentRecipe.Name;
            int stationCount = vm.CurrentRecipe.StationCount;
            int originalStation = vm.CurrentStationNumber;
            string originalImage = vm.CurrentTestImagePath;
            string originalSelection = vm.LightFlowSelection;
            // 背景讀取各站 Recipe 圖庫，避免大量檔案掃描讓主視窗卡住。
            var stationLists = await System.Threading.Tasks.Task.Run(() =>
                Enumerable.Range(1, stationCount).ToDictionary(
                    station => station, station => vm.GetStationStandardImageList(station)));
            if (vm.CurrentRecipe?.Id != recipeId) return;
            // 自動檢查並修復「正式資料及還原點都完整」的中斷殘留。
            // 無法確認安全時只停用受影響站別，不再鎖住整份配方。
            var recovery = await System.Threading.Tasks.Task.Run(() => vm.RepairIncompleteLightStandardFiles());
            if (vm.CurrentRecipe?.Id != recipeId) return;
            var images = new System.Collections.Generic.Dictionary<int, string>();
            var stationChoices = new System.Collections.Generic.Dictionary<int, CheckBox>();
            var interruptedTransactions = Enumerable.Range(1, stationCount)
                .SelectMany(station => vm.GetIncompleteLightStandardFiles(station)
                    .Select(path => (Station: station, File: path))).ToList();
            var blockedStations = new System.Collections.Generic.HashSet<int>(
                interruptedTransactions.Select(item => item.Station));
            var panel = new StackPanel { Margin = new Thickness(22, 8, 22, 16) };
            var title = new TextBlock
            {
                Text = "全站快速設定標準件", FontSize = 23,
                FontWeight = FontWeights.SemiBold, Foreground = StandardImagePickerPopup.BrushFrom("#20232B"),
                Margin = new Thickness(0, 0, 0, 6)
            };
            panel.Children.Add(title);
            panel.Children.Add(new TextBlock
            {
                Text = $"配方：{recipeName}　·　各站獨立選圖，預設第一張",
                FontSize = 13, Foreground = StandardImagePickerPopup.BrushFrom("#606975"),
                Margin = new Thickness(0, 0, 0, 6)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "點圖片或「圖庫」展開縮圖選單；目前採安全單站寫入，一次只能設定一站。",
                FontSize = 12, Foreground = StandardImagePickerPopup.BrushFrom("#7B8493"),
                Margin = new Thickness(0, 0, 0, 18)
            });
            if (recovery.Recovered > 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"已自動完成儲存復原，保留 {recovery.Recovered} 筆中斷紀錄備份。正式標準件未被覆寫。",
                    FontSize = 12, Foreground = StandardImagePickerPopup.BrushFrom("#14734B"),
                    Margin = new Thickness(0, 0, 0, 10)
                });
            }
            if (interruptedTransactions.Count > 0)
            {
                panel.Children.Add(new Border
                {
                    Background = StandardImagePickerPopup.BrushFrom("#FFF2E8"),
                    BorderBrush = StandardImagePickerPopup.BrushFrom("#E7A57F"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 14),
                    Child = new TextBlock
                    {
                        Text = $"有 {blockedStations.Count} 站的標準件尚無法安全自動恢復，該站已暫停寫入；其他站可正常設定。" +
                            " 原檔和還原點均保留，請由工程支援檢查，無須自行處理暫存檔。",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = StandardImagePickerPopup.BrushFrom("#913E20")
                    }
                });
            }
            var selectedSummary = new TextBlock
            {
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = StandardImagePickerPopup.BrushFrom("#525B68"),
                VerticalAlignment = VerticalAlignment.Center
            };
            void UpdateSelectedCount() => selectedSummary.Text = $"已選取 {images.Count} / {stationCount} 站";
            for (int station = 1; station <= stationCount; station++)
            {
                int stationNumber = station;
                var entry = stationLists[stationNumber];
                var files = entry.Files;
                string? snapshot = vm.GetStationCurrentImageSnapshot(stationNumber);
                string source = entry.Source;
                if (files.Count == 0 && snapshot != null)
                {
                    files = new System.Collections.Generic.List<string> { snapshot };
                    source = "當下圖片";
                }
                int selectedIndex = snapshot == null ? -1 :
                    files.FindIndex(path => string.Equals(path, snapshot, StringComparison.OrdinalIgnoreCase));
                if (selectedIndex < 0) selectedIndex = 0;

                var row = new Grid { MinHeight = 84 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(116) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(238) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(66) });
                var stationLabel = new TextBlock
                {
                    Text = $"第 {station:00} 站", FontWeight = FontWeights.SemiBold,
                    FontSize = 13, Foreground = StandardImagePickerPopup.BrushFrom("#293240"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(stationLabel, 0);
                row.Children.Add(stationLabel);
                var preview = new System.Windows.Controls.Image
                {
                    Width = 102, Height = 66, Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var previewBorder = new Border
                {
                    Child = preview, BorderBrush = StandardImagePickerPopup.BrushFrom("#E3E8EF"),
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                    Background = StandardImagePickerPopup.BrushFrom("#F5F6F9"),
                    ToolTip = "點擊縮圖展開本站圖片選單"
                };
                Grid.SetColumn(previewBorder, 1);
                row.Children.Add(previewBorder);

                var fileInfo = new StackPanel { Margin = new Thickness(13, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
                var name = new TextBlock
                {
                    FontSize = 12.5, FontWeight = FontWeights.Medium,
                    Foreground = StandardImagePickerPopup.BrushFrom("#27303A"),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                var detail = new TextBlock
                {
                    Foreground = StandardImagePickerPopup.BrushFrom("#778191"),
                    FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 5, 0, 0)
                };
                fileInfo.Children.Add(name);
                fileInfo.Children.Add(detail);
                Grid.SetColumn(fileInfo, 2);
                row.Children.Add(fileInfo);
                var navigation = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var previous = StandardImagePickerPopup.MakeButton("‹", StandardImagePickerPopup.BrushFrom("#F1F3F7"),
                    StandardImagePickerPopup.BrushFrom("#28303B"), 34, 34);
                var gallery = StandardImagePickerPopup.MakeButton("圖庫選單 ▾", StandardImagePickerPopup.BrushFrom("#E8F2FF"),
                    StandardImagePickerPopup.BrushFrom("#0069DA"), 125, 34);
                var next = StandardImagePickerPopup.MakeButton("›", StandardImagePickerPopup.BrushFrom("#F1F3F7"),
                    StandardImagePickerPopup.BrushFrom("#28303B"), 34, 34);
                previous.FontSize = 21;
                next.FontSize = 21;
                gallery.FontWeight = FontWeights.SemiBold;
                previous.Margin = new Thickness(2, 0, 4, 0);
                gallery.Margin = new Thickness(2, 0, 4, 0);
                next.Margin = new Thickness(2, 0, 0, 0);
                navigation.Children.Add(previous);
                navigation.Children.Add(gallery);
                navigation.Children.Add(next);
                Grid.SetColumn(navigation, 3);
                row.Children.Add(navigation);
                var choose = new CheckBox
                {
                    Content = blockedStations.Contains(stationNumber) ? "待修復" : "套用",
                    IsChecked = stationNumber == originalStation && files.Count > 0 && !blockedStations.Contains(stationNumber),
                    IsEnabled = files.Count > 0 && !blockedStations.Contains(stationNumber),
                    VerticalAlignment = VerticalAlignment.Center, FontSize = 12,
                    Foreground = StandardImagePickerPopup.BrushFrom("#293240")
                };
                stationChoices[stationNumber] = choose;
                Grid.SetColumn(choose, 4);
                row.Children.Add(choose);

                void Refresh()
                {
                    bool hasFile = selectedIndex < files.Count && files.Count > 0;
                    string? path = hasFile ? files[selectedIndex] : null;
                    preview.Source = LoadStandardThumbnail(path);
                    bool valid = path != null && preview.Source != null;
                    name.Text = path == null ? "無可用圖片" : System.IO.Path.GetFileName(path);
                    fileInfo.ToolTip = path;
                    detail.Text = path == null ? source : $"{source}　{selectedIndex + 1} / {files.Count}" +
                        (valid ? "" : "（縮圖無法讀取）");
                    previous.IsEnabled = selectedIndex > 0;
                    next.IsEnabled = selectedIndex >= 0 && selectedIndex < files.Count - 1;
                    choose.IsEnabled = valid && !blockedStations.Contains(stationNumber);
                    if (!valid) choose.IsChecked = false;
                    if (valid && choose.IsChecked == true) images[stationNumber] = path!;
                    else images.Remove(stationNumber);
                    UpdateSelectedCount();
                }
                gallery.IsEnabled = files.Count > 0;
                gallery.Click += (_, _) => StandardImagePickerPopup.Open(gallery, files, selectedIndex,
                    stationNumber, index => { selectedIndex = index; Refresh(); });
                previewBorder.MouseLeftButtonUp += (_, _) =>
                {
                    if (gallery.IsEnabled) StandardImagePickerPopup.Open(gallery, files, selectedIndex,
                        stationNumber, index => { selectedIndex = index; Refresh(); });
                };
                previewBorder.Cursor = Cursors.Hand;
                previous.Click += (_, _) => { if (selectedIndex > 0) { selectedIndex--; Refresh(); } };
                next.Click += (_, _) => { if (selectedIndex < files.Count - 1) { selectedIndex++; Refresh(); } };
                choose.Checked += (_, _) =>
                {
                    // 暫停連續多站檔案替換；明確選取新站時，取消舊站選取。
                    foreach (var option in stationChoices)
                        if (option.Key != stationNumber && option.Value.IsChecked == true)
                            option.Value.IsChecked = false;
                    if (selectedIndex < files.Count && files.Count > 0 && preview.Source != null)
                        images[stationNumber] = files[selectedIndex];
                    UpdateSelectedCount();
                };
                choose.Unchecked += (_, _) => { images.Remove(stationNumber); UpdateSelectedCount(); };
                Refresh();
                panel.Children.Add(new Border
                {
                    Child = row, Background = System.Windows.Media.Brushes.White,
                    BorderBrush = StandardImagePickerPopup.BrushFrom("#E8EAF0"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(12, 4, 12, 4),
                    Margin = new Thickness(0, 0, 0, 10)
                });
            }
            var dialog = new Window
            {
                Title = "批次設定各站標準件", Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Width = 1060, Height = 790, MinWidth = 960,
                Background = StandardImagePickerPopup.BrushFrom("#F5F6F9"),
                FontFamily = new System.Windows.Media.FontFamily("Microsoft JhengHei UI")
            };
            var root = new DockPanel { LastChildFill = true };
            var actions = new DockPanel
            {
                Background = System.Windows.Media.Brushes.White,
                LastChildFill = false,
                Margin = new Thickness(0),
                Height = 74
            };
            var summaryBox = new StackPanel
            {
                Margin = new Thickness(26, 14, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            summaryBox.Children.Add(selectedSummary);
            summaryBox.Children.Add(new TextBlock
            {
                Text = "安全模式：每次只更新一站；送出前確認覆蓋", FontSize = 11,
                Foreground = StandardImagePickerPopup.BrushFrom("#828B99")
            });
            DockPanel.SetDock(summaryBox, Dock.Left);
            actions.Children.Add(summaryBox);
            var actionButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 20, 0)
            };
            var cancel = StandardImagePickerPopup.MakeButton("取消", StandardImagePickerPopup.BrushFrom("#EEF0F4"),
                StandardImagePickerPopup.BrushFrom("#343D49"), 100, 40);
            var apply = StandardImagePickerPopup.MakeButton("設定本站標準件…", StandardImagePickerPopup.BrushFrom("#007AFF"),
                System.Windows.Media.Brushes.White, 158, 40);
            cancel.Margin = new Thickness(0, 0, 10, 0);
            cancel.Click += (_, _) => dialog.DialogResult = false;
            apply.Click += (_, _) =>
            {
                if (images.Count == 0)
                {
                    MessageBox.Show(dialog, "沒有選取可用圖片。請檢查各站圖庫路徑、縮圖與套用勾選。");
                    return;
                }
                if (images.Count != 1)
                {
                    MessageBox.Show(dialog, "目前停用連續多站寫入，請一次只選取一站。", "安全單站設定",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                int selectedStation = images.Keys.Single();
                var incomplete = vm.GetIncompleteLightStandardFiles(selectedStation);
                if (incomplete.Count > 0)
                {
                    MessageBox.Show(dialog,
                        $"第 {selectedStation:00} 站還有未完成的標準件資料。本次不覆寫該站，其他站不受影響。" +
                        "\n系統已保留原始資料，請由工程支援檢查。",
                        "偵測到未完成的標準件交易", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (images.Values.Any(path => !File.Exists(path)))
                {
                    MessageBox.Show(dialog, "有圖片已不存在，請重新選取。");
                    return;
                }
                string selected = string.Join("\n", images.OrderBy(pair => pair.Key)
                    .Select(pair => $"第 {pair.Key:00} 站：{System.IO.Path.GetFileName(pair.Value)}"));
                if (MessageBox.Show(dialog,
                    $"安全單站設定：即將設定第 {selectedStation:00} 站的標準件與亮度基準。\n\n{selected}\n\n" +
                    "警告：已存在的標準件將被覆蓋；系統會沿用每站樣本還原點。\n確定執行？",
                    "確認單站覆蓋", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    dialog.DialogResult = true;
            };
            actionButtons.Children.Add(cancel);
            actionButtons.Children.Add(apply);
            DockPanel.SetDock(actionButtons, Dock.Right);
            actions.Children.Add(actionButtons);
            DockPanel.SetDock(actions, Dock.Bottom);
            root.Children.Add(actions);
            root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            dialog.Content = root;
            if (dialog.ShowDialog() != true) return;

            int completed = 0;
            var failures = new System.Collections.Generic.List<string>();
            try
            {
                _nativeFlowBusy = true;
                foreach (var entry in images.OrderBy(pair => pair.Key))
                {
                    if (vm.CurrentRecipe?.Id != recipeId)
                        throw new InvalidOperationException("配方已變更，停止批次設定。");
                    try
                    {
                        vm.SelectStationCommand.Execute(entry.Key);
                        if (vm.CurrentStationNumber != entry.Key)
                            throw new InvalidOperationException("無法切換到指定站別。");
                        vm.CurrentTestImagePath = entry.Value;
                        // 各站可能使用不同的定位模式，不可將原站模式直接套用全站。
                        vm.LightFlowSelection = vm.LoadFlowSelection();
                        await vm.SetCurrentImageAsLightStandardAsync();
                        completed++;
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"第 {entry.Key:00} 站：{ex.GetBaseException().Message}");
                    }
                }
            }
            finally
            {
                if (vm.CurrentRecipe?.Id == recipeId)
                {
                    vm.SelectStationCommand.Execute(originalStation);
                    vm.LightFlowSelection = originalSelection;
                    // 透過正式圖片切換流程恢復畫面，避免只改路徑而預覽仍停在其他圖片。
                    if (File.Exists(originalImage) && vm.TestImageFiles.Contains(originalImage))
                        vm.SelectTestImage(originalImage);
                }
                _nativeFlowBusy = false;
            }
            string result = $"安全單站標準件完成 {completed}/{images.Count} 站。";
            if (failures.Count > 0) result += "\n\n失敗項目：\n" + string.Join("\n", failures);
            AoiWorkflow.SetPipelineStatus(result.Replace("\n", " "));
            MessageBox.Show(result, "批次標準件結果", MessageBoxButton.OK,
                failures.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private static BitmapImage? LoadStandardThumbnail(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            try
            {
                return InMemoryImageLoader.Load(path, 150);
            }
            catch { return null; }
        }

        private void StandardPartMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu menu || menu.Items[0] is not MenuItem item) return;
            var vm = DataContext as mainViewModel;
            bool hasImage = vm != null && !string.IsNullOrWhiteSpace(vm.CurrentTestImagePath) && File.Exists(vm.CurrentTestImagePath);
            bool busy = _nativeFlowBusy || _nativeEditorOpen || vm?.IsAnalyzingTestImages == true ||
                vm?.IsContinuousTestRunning == true || vm?.IsMultiStationTesting == true;
            item.IsEnabled = vm?.CurrentRecipe != null && hasImage && !busy;
            item.Header = vm?.CurrentRecipe == null ? "設為標準件" : $"設為第 {vm.CurrentStationNumber} 站標準件";
            item.ToolTip = busy ? "請先完成檢測或關閉原生參數工具。" : !hasImage ? "請先選擇本站要設為標準件的圖片。"
                : $"配方：{vm!.CurrentRecipe?.Name}\n圖片：{System.IO.Path.GetFileName(vm.CurrentTestImagePath)}\n直接以這張圖片計算基準亮度並保存本站標準件。";
        }

        private async void ConfigureStandardPart_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not mainViewModel vm || vm.CurrentRecipe == null) return;
            if (_nativeFlowBusy || _nativeEditorOpen || vm.IsAnalyzingTestImages || vm.IsContinuousTestRunning || vm.IsMultiStationTesting)
            {
                MessageBox.Show("請先完成檢測或關閉原生參數工具，再設定標準件。", "設定標準件", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string image = vm.CurrentTestImagePath;
            if (string.IsNullOrWhiteSpace(image) || !File.Exists(image))
            {
                MessageBox.Show("請先選擇本站要設為標準件的圖片。", "設定標準件", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string recipeId = vm.CurrentRecipe.Id;
            int station = vm.CurrentStationNumber;
            try
            {
                _nativeFlowBusy = true;
                bool locatorEnabled = AoiWorkflow.IsLocatorEnabled;
                vm.LightFlowSelection = locatorEnabled
                    ? "1" + (AoiWorkflow.IsSelectWayEnabled ? "1" : "0") + (AoiWorkflow.IsSelectFaceEnabled ? "1" : "0")
                    : "000";
                AoiWorkflow.SetPipelineStatus(locatorEnabled
                    ? "正在以目前圖片設定本站標準件…"
                    : "正在以整張圖設定本站光源標準件…");
                string saved = await vm.SetCurrentImageAsLightStandardAsync();
                AoiWorkflow.SetPipelineStatus(saved);
                await RetestSavedImageAsync(vm, recipeId, station, image);
            }
            catch (Exception ex)
            {
                AoiWorkflow.SetPipelineStatus("設定標準件失敗：" + ex.GetBaseException().Message);
                MessageBox.Show(ex.GetBaseException().Message, "設定標準件", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { _nativeFlowBusy = false; }
        }

        private async void OpenAoiLight_Click(object? sender, EventArgs e)
        {
            if (_nativeFlowBusy) return;
            if (DataContext is not mainViewModel vm || vm.CurrentRecipe == null)
            {
                MessageBox.Show("請先載入配方。", "光源檢測", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (vm.IsAnalyzingTestImages || vm.IsContinuousTestRunning || vm.IsMultiStationTesting)
            {
                MessageBox.Show("檢測進行中，請先完成再執行光源流程。", "光源檢測",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (string.IsNullOrWhiteSpace(vm.CurrentTestImagePath) || !File.Exists(vm.CurrentTestImagePath))
                vm.EnsureAoiAllDatabase(showError: true);
            if (string.IsNullOrWhiteSpace(vm.CurrentTestImagePath) || !File.Exists(vm.CurrentTestImagePath))
            {
                MessageBox.Show("請先設定本站 ALL DB 或 OK／NG 圖庫。", "光源檢測",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var recipe = vm.CurrentRecipe;
            int station = vm.CurrentStationNumber;
            string imagePath = vm.CurrentTestImagePath;
            try
            {
                // With localization off, the light editor uses its independent full-image ROI.
                bool locatorEnabled = AoiWorkflow.IsLocatorEnabled;
                AoiWorkflow.SetPipelineStatus(locatorEnabled
                    ? "光源設定已開啟，請選圖、定位及校準 ROI。"
                    : "光源設定已開啟，定位未勾選；請在整張圖上校準獨立 ROI。");
                vm.LightFlowSelection = locatorEnabled
                    ? "1" + (AoiWorkflow.IsSelectWayEnabled ? "1" : "0") + (AoiWorkflow.IsSelectFaceEnabled ? "1" : "0")
                    : "000";
                var lightWindow = new LightInspectionWindow(vm, imagePath) { Owner = this };
                lightWindow.ShowDialog();
                if (lightWindow.SettingsSaved)
                    await RetestSavedImageAsync(vm, recipe.Id, station, imagePath);
            }
            catch (Exception ex)
            {
                AoiWorkflow.SetPipelineStatus("光源流程中止：" + ex.GetBaseException().Message);
                MessageBox.Show("光源流程尚未通過，未做 OK 判定：\n" +
                    ex.GetBaseException().Message, "光源檢測",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { _nativeFlowBusy = false; }
        }

        private void OpenInspToolsPage(string mode, string source, int station)
        {
            if (DataContext is mainViewModel vm && (string.IsNullOrWhiteSpace(vm.CurrentTestImagePath) ||
                                                     !File.Exists(vm.CurrentTestImagePath)))
                vm.EnsureAoiAllDatabase(showError: true);
            string? imagePath = (DataContext as mainViewModel)?.CurrentTestImagePath;
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                MessageBox.Show("本站 ALL DB 沒有可用圖片，請在圖庫設定中指定 ALL DB 或 OK／NG 路徑。", "定位", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string exe = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NativeR3", "InspToolsLocator.exe");
            try
            {
                if (!File.Exists(exe)) throw new FileNotFoundException("找不到原廠非 AI 定位元件，請重新部署程式。", exe);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, mode + " \"" + imagePath + "\" \"" + source + "\" " + station)
                { UseShellExecute = false, WorkingDirectory = System.IO.Path.GetDirectoryName(exe)! });
            }
            catch (Exception ex)
            {
                MessageBox.Show("無法開啟定位工具：" + ex.Message, "定位", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OverviewStation_Click(object sender, RoutedEventArgs e)
        {
            // 站別切換仍用既有 Command，導覽轉向 AOI 而非舊單站頁。
            ShowAoiTools_Click(sender, e);
            // Button raises Click before executing its bound command, so defer restore
            // until SelectStation has updated the view model's station number.
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                if (DataContext is mainViewModel vm)
                    vm.RestoreMultiStationResultsForCurrentStation();
            }));
        }

        private void OverviewDatabase_Click(object sender, RoutedEventArgs e)
        {
            // 由既有圖庫命令載入站別圖片，再進 AOI。
            ShowAoiTools_Click(sender, e);
        }

        private void ShowMainPage(bool overview)
        {
            StationOverviewPage.Visibility = overview ? Visibility.Visible : Visibility.Collapsed;
            InspectionPage.Visibility = overview ? Visibility.Collapsed : Visibility.Visible;
            AoiWorkflow.Visibility = overview ? Visibility.Collapsed : Visibility.Visible;
            AoiPanelReopen.Visibility = Visibility.Collapsed;
            InspectionSideColumn.Width = new GridLength(322);
            OverviewNavButton.Tag = overview ? "Active" : null;
            AoiToolsNavButton.Tag = null;
            var activeBackground = (System.Windows.Media.Brush)FindResource("BlueSoft");
            var activeBorder = (System.Windows.Media.Brush)FindResource("Blue");
            OverviewNavButton.Background = overview ? activeBackground : System.Windows.Media.Brushes.Transparent;
            OverviewNavButton.BorderBrush = overview ? activeBorder : System.Windows.Media.Brushes.Transparent;
            AoiToolsNavButton.Background = System.Windows.Media.Brushes.Transparent;
            AoiToolsNavButton.BorderBrush = System.Windows.Media.Brushes.Transparent;
        }

        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            _sidebarCollapsed = !_sidebarCollapsed;
            SidebarColumn.Width = new GridLength(_sidebarCollapsed ? 88 : 182);
            var visibility = _sidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
            SidebarBrand.Visibility = visibility;
            SidebarSectionLabel.Visibility = visibility;
            SidebarFooter.Visibility = visibility;
        }

        private void OpenRecipeManager_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is mainViewModel vm && vm.IsMultiStationTesting)
            {
                MessageBox.Show("多站測試進行中，請完成或停止後再調整配方。", "多站檢測",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var window = new RecipeManagerWindow(this);
            window.ShowDialog();
        }

        private void OpenDatabaseSetup_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is mainViewModel vm && vm.IsMultiStationTesting)
            {
                MessageBox.Show("多站測試進行中，請完成或停止後再調整圖庫路徑。", "多站檢測",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var window = new DatabaseSetupWindow(this);
            window.ShowDialog();
        }

        private void OpenUserGuide_Click(object sender, RoutedEventArgs e)
        {
            string guidePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "操作說明.html");
            if (!File.Exists(guidePath))
            {
                MessageBox.Show("找不到操作說明檔，請重新建置或部署程式。", "操作說明",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(guidePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("無法開啟操作說明：" + ex.GetBaseException().Message, "操作說明",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenTestImagePicker_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not mainViewModel viewModel || viewModel.TestImageFiles.Count == 0)
                return;

            var picker = new TestImagePickerWindow(viewModel.CreateTestImagePickerTree(), viewModel.CurrentTestImagePath)
            {
                Owner = this
            };
            if (picker.ShowDialog() == true && !string.IsNullOrWhiteSpace(picker.SelectedFilePath))
                viewModel.SelectTestImage(picker.SelectedFilePath);
        }

        private void MainImage_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (DataContext is not mainViewModel viewModel ||
                viewModel.TestImageFiles.Count < 2 ||
                viewModel.IsAnalyzingTestImages)
            {
                return;
            }

            e.Handled = true;
            _ = viewModel.NavigateTestImageByWheelAsync(e.Delta);
        }

    }
}
