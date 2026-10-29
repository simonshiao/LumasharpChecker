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
using LumasharpChecker.model;
using LumasharpChecker.view;

namespace LumasharpChecker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private System.Windows.Point _roiDragStart;
        private System.Windows.Shapes.Rectangle? _roiDraft;
        private bool _sidebarCollapsed;
        private bool _nativeFlowBusy;
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
            AoiWorkflow.LightRequested += OpenAoiLight_Click;
            AoiWorkflow.RetestRequested += async (_, _) =>
            {
                if (DataContext is mainViewModel vm) await vm.RetestFromAoiAsync();
            };
            AoiWorkflow.ResultsRequested += (_, _) =>
            {
                if (DataContext is mainViewModel vm) vm.ShowTestImageResultsCommand.Execute(null);
            };
            AoiWorkflow.ParetoRequested += (_, _) =>
            {
                if (DataContext is mainViewModel vm)
                {
                    var window = new ParetoPreviewWindow(vm.TestImageResults) { Owner = this };
                    window.ShowDialog();
                }
            };
            AoiWorkflow.SizeStatisticsRequested += (_, _) =>
            {
                if (DataContext is mainViewModel vm)
                    new RoiSizeStatisticsWindow(vm.TestImageResults) { Owner = this }.ShowDialog();
            };
            AoiWorkflow.CollapseRequested += (_, _) => SetAoiPanelExpanded(false);

            Version? version = Assembly.GetEntryAssembly().GetName().Version;
            this.Title += $" {version?.ToString()} [NET472 Slim]";
            // 首頁預設為 Chao8Side 風格的極簡看板，設定工具收在左側導覽。
            ToggleSidebar_Click(this, new RoutedEventArgs());
            ShowMainPage(true);
        }

        private void SubscribeAoiMessages()
        {
            if (_messageSource != null) _messageSource.PropertyChanged -= OnAoiSettingsChanged;
            _messageSource = DataContext as System.ComponentModel.INotifyPropertyChanged;
            if (_messageSource != null) _messageSource.PropertyChanged += OnAoiSettingsChanged;
        }

        private void OnAoiSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
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

        // 舊單站 UI 暫時隱藏；任何舊入口都轉到統一的 AOI 工作區。
        private void ShowInspection_Click(object sender, RoutedEventArgs e) => ShowAoiTools_Click(sender, e);

        private void ShowAoiTools_Click(object sender, RoutedEventArgs e)
        {
            ShowMainPage(false);
            InspectionSettingsPanel.Visibility = Visibility.Collapsed;
            SetAoiPanelExpanded(true);
            InspectionNavButton.Tag = null;
            AoiToolsNavButton.Tag = "Active";
            InspectionNavButton.Background = System.Windows.Media.Brushes.Transparent;
            InspectionNavButton.BorderBrush = System.Windows.Media.Brushes.Transparent;
            AoiToolsNavButton.Background = (System.Windows.Media.Brush)FindResource("BlueSoft");
            AoiToolsNavButton.BorderBrush = (System.Windows.Media.Brush)FindResource("Blue");
            // AOI 預設沿用目前站別 OK DB，不用每次開檔案選擇視窗。
            if (DataContext is mainViewModel vm && vm.CurrentRecipe != null)
                vm.EnsureAoiOkDatabase();
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
        private void OpenAoiNativeEditor(string label)
        {
            if (_nativeFlowBusy) return;
            if (DataContext is not mainViewModel vm || vm.CurrentRecipe == null)
            {
                MessageBox.Show("請先載入 Recipe。", label, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (vm.IsAnalyzingTestImages || vm.IsContinuousTestRunning || vm.IsMultiStationTesting)
            {
                MessageBox.Show("檢測進行中，請先完成再調整工具。", label,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                var ready = NativeRecipeBridge.Prepare(vm.CurrentRecipe, AppDomain.CurrentDomain.BaseDirectory);
                NativeRecipeBridge.OpenSandbox(ready, vm.CurrentStationNumber, label == "定位",
                    vm.TestImageFiles, vm.CurrentTestImagePath, label == "選向" ? "way" :
                    label == "選面" ? "face" : null);
                AoiWorkflow.SetPipelineStatus("已開啟第 " + vm.CurrentStationNumber +
                    " 站原生 R3 工具工作區，請選擇「" + label + "」參數頁；此視窗操作隔離 Recipe 副本。");
            }
            catch (Exception ex)
            {
                AoiWorkflow.SetPipelineStatus("無法開啟原生 " + label + " 設定：" + ex.GetBaseException().Message);
                MessageBox.Show(ex.GetBaseException().Message, label + "設定",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async System.Threading.Tasks.Task InspectNativeStageAsync(string step, string label)
        {
            if (_nativeFlowBusy) return;
            if (DataContext is not mainViewModel vm || vm.CurrentRecipe == null)
            {
                MessageBox.Show("請先載入 Recipe。", label, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (vm.IsAnalyzingTestImages || vm.IsContinuousTestRunning || vm.IsMultiStationTesting)
            {
                MessageBox.Show("檢測進行中，請先完成再使用工具。", label,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (string.IsNullOrWhiteSpace(vm.CurrentTestImagePath) || !File.Exists(vm.CurrentTestImagePath))
                vm.EnsureAoiOkDatabase(showError: true);
            if (string.IsNullOrWhiteSpace(vm.CurrentTestImagePath) || !File.Exists(vm.CurrentTestImagePath))
            {
                MessageBox.Show("本站 OK DB 尚未設定，無法執行原生工具。", label,
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
                    throw new InvalidOperationException("站別、Recipe 或圖片已變更，本次結果作廢。");
                AoiWorkflow.SetPipelineStatus(native.Summary);
                string state = step == "way" ? native.SelectWay
                    : step == "face" ? native.SelectFace : native.Locate;
                if (state == "SKIPPED_RECIPE_OFF")
                {
                    MessageBox.Show("第 " + station + " 站原廠 Recipe 未啟用「" + label +
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

        private async void OpenAoiLight_Click(object? sender, EventArgs e)
        {
            if (_nativeFlowBusy) return;
            if (DataContext is not mainViewModel vm || vm.CurrentRecipe == null)
            {
                MessageBox.Show("請先載入 Recipe。", "光源檢測", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (vm.IsAnalyzingTestImages || vm.IsContinuousTestRunning || vm.IsMultiStationTesting)
            {
                MessageBox.Show("檢測進行中，請先完成再執行光源流程。", "光源檢測",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (string.IsNullOrWhiteSpace(vm.CurrentTestImagePath) || !File.Exists(vm.CurrentTestImagePath))
                vm.EnsureAoiOkDatabase(showError: true);
            if (string.IsNullOrWhiteSpace(vm.CurrentTestImagePath) || !File.Exists(vm.CurrentTestImagePath))
            {
                MessageBox.Show("請先設定本站 OK DB。", "光源檢測",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var recipe = vm.CurrentRecipe;
            int station = vm.CurrentStationNumber;
            string imagePath = vm.CurrentTestImagePath;
            try
            {
                AoiWorkflow.SetPipelineStatus("原生非 AI 定位／選向／選面檢查中…");
                var native = await NativeRecipeBridge.RunPreLightAsync(
                    recipe, station, imagePath, AppDomain.CurrentDomain.BaseDirectory);
                if (vm.CurrentRecipe?.Id != recipe.Id ||
                    vm.CurrentStationNumber != station || vm.CurrentTestImagePath != imagePath)
                    throw new InvalidOperationException("檢測期間 Recipe、站別或圖片已改變，結果不採用。");
                AoiWorkflow.SetPipelineStatus(native.Summary + " → Luma 光源檢測");
                if (vm.CurrentProfile == null)
                {
                    MessageBox.Show("原生定位通過，但本站的 Luma 光源 ROI／亮度規格尚未校準。\n" +
                        native.Summary + "\n請先新增本站參數，才能做光源 OK／NG 判定。",
                        "光源待校準", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                new LightInspectionWindow(vm, imagePath) { Owner = this }.ShowDialog();
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
                vm.EnsureAoiOkDatabase(showError: true);
            string? imagePath = (DataContext as mainViewModel)?.CurrentTestImagePath;
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                MessageBox.Show("本站 OK DB 沒有可用圖片，請在圖庫設定中指定 OK 路徑。", "定位", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string exe = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NativeLocator", "InspToolsLocator.exe");
            try
            {
                if (!File.Exists(exe)) throw new FileNotFoundException("找不到原廠定位元件或其 AI 執行環境，請重新部署程式。", exe);
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
        }

        private void OverviewDatabase_Click(object sender, RoutedEventArgs e)
        {
            // 由既有圖庫命令載入站別圖片，再進 AOI。
            ShowAoiTools_Click(sender, e);
        }

        private void ShowMainPage(bool overview)
        {
            // 切到另一站或從總覽返回時，預設先顯示檢測摘要，
            // 不延續上一站展開的編輯閾值，減少誤操作。
            if (!overview)
                AdvancedSettingsExpander.IsExpanded = false;
            StationOverviewPage.Visibility = overview ? Visibility.Visible : Visibility.Collapsed;
            InspectionPage.Visibility = overview ? Visibility.Collapsed : Visibility.Visible;
            InspectionSettingsPanel.Visibility = Visibility.Visible;
            AoiWorkflow.Visibility = Visibility.Collapsed;
            AoiPanelReopen.Visibility = Visibility.Collapsed;
            InspectionSideColumn.Width = new GridLength(322);
            OverviewNavButton.Tag = overview ? "Active" : null;
            InspectionNavButton.Tag = overview ? null : "Active";
            AoiToolsNavButton.Tag = null;
            var activeBackground = (System.Windows.Media.Brush)FindResource("BlueSoft");
            var activeBorder = (System.Windows.Media.Brush)FindResource("Blue");
            OverviewNavButton.Background = overview ? activeBackground : System.Windows.Media.Brushes.Transparent;
            OverviewNavButton.BorderBrush = overview ? activeBorder : System.Windows.Media.Brushes.Transparent;
            InspectionNavButton.Background = overview ? System.Windows.Media.Brushes.Transparent : activeBackground;
            InspectionNavButton.BorderBrush = overview ? System.Windows.Media.Brushes.Transparent : activeBorder;
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

        private void OpenStationPatterns_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not mainViewModel vm || vm.CurrentRecipe == null ||
                vm.CurrentProfile == null)
                return;
            if (vm.IsMultiStationTesting || vm.IsAnalyzingTestImages || vm.IsContinuousTestRunning)
            {
                MessageBox.Show("請先停止檢測，再調整定位樣板。", "定位樣板",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var station = vm.CurrentRecipe.Stations.FirstOrDefault(s => s.Number == vm.CurrentStationNumber);
            if (station?.IsConfigured != true ||
                !Guid.TryParseExact(vm.CurrentRecipe.Id, "N", out _))
            {
                MessageBox.Show("請先將本站檢測參數儲存至 Recipe，再加入樣板。", "定位樣板",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string stationDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "recipes", vm.CurrentRecipe.Id, $"station{vm.CurrentStationNumber:00}");
            try
            {
                string primary = System.IO.Path.GetFullPath(System.IO.Path.Combine(stationDir, "pattern.jpg"));
                if (string.IsNullOrWhiteSpace(vm.CurrentProfile.ImagePath) ||
                    !string.Equals(System.IO.Path.GetFullPath(vm.CurrentProfile.ImagePath), primary,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("目前使用的不是本站 Recipe 設定，請先重新載入本站。");
                var roi = new System.Drawing.Rectangle(
                    vm.CurrentProfile.PatternMatchRoiX.GetValueOrDefault(),
                    vm.CurrentProfile.PatternMatchRoiY.GetValueOrDefault(),
                    vm.CurrentProfile.PatternMatchRoiW.GetValueOrDefault(),
                    vm.CurrentProfile.PatternMatchRoiH.GetValueOrDefault());
                var library = new StationPatternLibrary(primary, roi);
                var manager = new StationPatternsWindow(this, vm.CurrentStationNumber, library, vm.CurrentTestImagePath);
                manager.ShowDialog();
                if (manager.Modified) vm.InvalidatePatternMatchResults();
            }
            catch (Exception ex)
            {
                MessageBox.Show("無法開啟本站定位樣板：" + ex.Message, "定位樣板",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CloseSettingsPanel_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is mainViewModel vm)
                vm.IsSettingsPanelOpen = false;
        }

        private void OpenTestImagePicker_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not mainViewModel viewModel || viewModel.TestImageTree.Count == 0)
                return;

            var picker = new TestImagePickerWindow(viewModel.TestImageTree, viewModel.CurrentTestImagePath)
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

        private void MainRoiCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not mainViewModel viewModel || !viewModel.IsRoiEditing)
                return;

            _roiDragStart = ClampRoiPoint(e.GetPosition(MainRoiCanvas));
            _roiDraft = new System.Windows.Shapes.Rectangle
            {
                Stroke = GetRoiBrush(viewModel.SelectedRoiType),
                StrokeThickness = 3,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = System.Windows.Media.Brushes.Transparent,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(_roiDraft, _roiDragStart.X);
            Canvas.SetTop(_roiDraft, _roiDragStart.Y);
            MainRoiCanvas.Children.Add(_roiDraft);
            MainRoiCanvas.CaptureMouse();
            MainRoiCanvas.Cursor = Cursors.Cross;
            e.Handled = true;
        }

        private void MainRoiCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_roiDraft == null || DataContext is not mainViewModel)
                return;

            System.Windows.Point current = ClampRoiPoint(e.GetPosition(MainRoiCanvas));
            Canvas.SetLeft(_roiDraft, Math.Min(_roiDragStart.X, current.X));
            Canvas.SetTop(_roiDraft, Math.Min(_roiDragStart.Y, current.Y));
            _roiDraft.Width = Math.Abs(current.X - _roiDragStart.X);
            _roiDraft.Height = Math.Abs(current.Y - _roiDragStart.Y);
        }

        private void MainRoiCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_roiDraft == null)
                return;

            System.Windows.Point end = ClampRoiPoint(e.GetPosition(MainRoiCanvas));
            MainRoiCanvas.Children.Remove(_roiDraft);
            _roiDraft = null;
            MainRoiCanvas.ReleaseMouseCapture();
            MainRoiCanvas.Cursor = Cursors.Arrow;

            int x = (int)Math.Round(Math.Min(_roiDragStart.X, end.X));
            int y = (int)Math.Round(Math.Min(_roiDragStart.Y, end.Y));
            int width = (int)Math.Round(Math.Abs(end.X - _roiDragStart.X));
            int height = (int)Math.Round(Math.Abs(end.Y - _roiDragStart.Y));
            if (width < 2 || height < 2)
                return;

            if (DataContext is mainViewModel viewModel)
            {
                viewModel.UpdateStationRoi(viewModel.SelectedRoiType, x, y, width, height);
                viewModel.StationStatusMessage = $"{viewModel.SelectedRoiType} ROI 已儲存：X {x}、Y {y}、W {width}、H {height}。";
            }
            e.Handled = true;
        }

        private System.Windows.Point ClampRoiPoint(System.Windows.Point point)
        {
            double width = Math.Max(0, MainRoiCanvas.Width);
            double height = Math.Max(0, MainRoiCanvas.Height);
            return new System.Windows.Point(Math.Min(Math.Max(point.X, 0), width), Math.Min(Math.Max(point.Y, 0), height));
        }

        private static System.Windows.Media.Brush GetRoiBrush(string roiType) => roiType switch
        {
            "定位" => System.Windows.Media.Brushes.LimeGreen,
            "亮度" => System.Windows.Media.Brushes.DeepSkyBlue,
            "銳利度" => System.Windows.Media.Brushes.OrangeRed,
            _ => System.Windows.Media.Brushes.White
        };
        //[DllImport("user32.dll")]
        //private static extern bool SetForegroundWindow(IntPtr hWnd);
    }

    /// <summary>
    /// RoiModeToBoolConverter — 在 mainViewModel.RoiDisplayStyle (Design/Result) 與 RadioButton.IsChecked 之間轉換。
    /// </summary>
    public class RoiModeToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is mainViewModel.RoiDisplayStyle mode && parameter is string param)
                return mode.ToString() == param;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter is string param)
                return Enum.Parse(typeof(mainViewModel.RoiDisplayStyle), param);
            return Binding.DoNothing;
        }
    }
}
