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

            Version? version = Assembly.GetEntryAssembly().GetName().Version;
            this.Title += $" {version?.ToString()} [NET472 Slim]";
            // 首頁預設為 Chao8Side 風格的極簡看板，設定工具收在左側導覽。
            ToggleSidebar_Click(this, new RoutedEventArgs());
            ShowMainPage(true);
        }

        private void ShowOverview_Click(object sender, RoutedEventArgs e) => ShowMainPage(true);

        private void ShowInspection_Click(object sender, RoutedEventArgs e) => ShowMainPage(false);

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
            var activeBackground = (System.Windows.Media.Brush)FindResource("BlueSoft");
            var activeBorder = (System.Windows.Media.Brush)FindResource("Blue");
            OverviewNavButton.Background = overview ? activeBackground : System.Windows.Media.Brushes.Transparent;
            OverviewNavButton.BorderBrush = overview ? activeBorder : System.Windows.Media.Brushes.Transparent;
            InspectionNavButton.Background = overview ? System.Windows.Media.Brushes.Transparent : activeBackground;
            InspectionNavButton.BorderBrush = overview ? System.Windows.Media.Brushes.Transparent : activeBorder;
        }

        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            _sidebarCollapsed = !_sidebarCollapsed;
            SidebarColumn.Width = new GridLength(_sidebarCollapsed ? 64 : 182);
            var visibility = _sidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
            SidebarBrand.Visibility = visibility;
            SidebarSectionLabel.Visibility = visibility;
            SidebarFooter.Visibility = visibility;
            OverviewNavLabel.Visibility = visibility;
            InspectionNavLabel.Visibility = visibility;
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
}
