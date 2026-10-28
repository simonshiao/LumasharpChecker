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

        public MainWindow()
        {
            InitializeComponent();

            Version? version = Assembly.GetEntryAssembly().GetName().Version;
            this.Title += $" {version?.ToString()}";
        }

        private void OpenRecipeManager_Click(object sender, RoutedEventArgs e)
        {
            var window = new RecipeManagerWindow(this);
            window.ShowDialog();
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
            return new System.Windows.Point(Math.Clamp(point.X, 0, width), Math.Clamp(point.Y, 0, height));
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
