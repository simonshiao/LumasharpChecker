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

        public MainWindow()
        {
            InitializeComponent();

            AoiWorkflow.LocatorRequested += OpenAoiLocator_Click;
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
            AoiWorkflow.CollapseRequested += (_, _) => SetAoiPanelExpanded(false);

            Version? version = Assembly.GetEntryAssembly().GetName().Version;
            this.Title += $" {version?.ToString()} [NET472 Slim]";
            // 首頁預設為 Chao8Side 風格的極簡看板，設定工具收在左側導覽。
            ToggleSidebar_Click(this, new RoutedEventArgs());
            ShowMainPage(true);
        }

        private void ShowOverview_Click(object sender, RoutedEventArgs e) => ShowMainPage(true);

        private void ShowInspection_Click(object sender, RoutedEventArgs e) => ShowMainPage(false);

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
            if (DataContext is not mainViewModel vm || vm.CurrentRecipe == null)
            {
                MessageBox.Show("請先載入 Recipe。", "定位", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var source = vm.GetVendorRecipeSnapshotPath();
            if (source == null || !File.Exists(System.IO.Path.Combine(source, $"station{vm.CurrentStationNumber}.ini")))
            {
                MessageBox.Show("目前 Recipe 沒有此站的原廠定位資料。Luma 定位參數與原廠定位設定格式不同，尚未自動轉換。", "定位", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            OpenInspToolsPage("locate", source, vm.CurrentStationNumber);
        }

        private void OpenAoiLight_Click(object? sender, EventArgs e)
        {
            if (DataContext is not mainViewModel vm || string.IsNullOrWhiteSpace(vm.CurrentTestImagePath) || !File.Exists(vm.CurrentTestImagePath))
            {
                MessageBox.Show("請先在單站檢測選擇圖片。", "光源檢測", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (vm.CurrentRecipe == null || (vm.CurrentProfile == null && vm.GetVendorRecipeSnapshotPath() == null))
            {
                MessageBox.Show("請先載入已校準的 Recipe 與站別。", "光源檢測", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try { new LightInspectionWindow(vm, vm.CurrentTestImagePath) { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show(ex.GetBaseException().Message, "光源檢測", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void OpenInspToolsPage(string mode, string source, int station)
        {
            string? imagePath = (DataContext as mainViewModel)?.CurrentTestImagePath;
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                MessageBox.Show("請先在單站檢測選擇圖片。", "定位", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string exe = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "InspToolsLocator", "InspToolsLocator.exe");
            try
            {
                if (!File.Exists(exe)) throw new FileNotFoundException("找不到 InspTools 定位元件，請重新建置程式。", exe);
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
            // Station selection uses the existing SelectStationCommand / Recipe flow.
            ShowMainPage(false);
        }

        private void OverviewDatabase_Click(object sender, RoutedEventArgs e)
        {
            // The existing OpenStationDatabaseCommand loads pictures; only switch the view here.
            ShowMainPage(false);
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
