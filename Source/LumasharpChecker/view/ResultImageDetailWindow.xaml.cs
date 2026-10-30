using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using LumasharpChecker.viewmodel;

namespace LumasharpChecker.view
{
    public partial class ResultImageDetailWindow : Window
    {
        private const double MinimumZoom = 0.1;
        private const double MaximumZoom = 8.0;
        private double _zoom = 1.0;
        private Point _dragStartPoint;
        private double _dragStartHorizontalOffset;
        private double _dragStartVerticalOffset;
        private bool _isDragging;

        public ResultImageDetailWindow(TestImageResultRow result)
        {
            InitializeComponent();
            DataContext = new ResultImageDetailViewModel(result);
            UpdateZoomText();
        }

        private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control)
            {
                return;
            }

            e.Handled = true;
            var oldZoom = _zoom;
            var point = e.GetPosition(ImageContainer);
            SetZoom(e.Delta > 0 ? _zoom * 1.15 : _zoom / 1.15);

            double ratio = _zoom / oldZoom;
            ImageScrollViewer.ScrollToHorizontalOffset((ImageScrollViewer.HorizontalOffset + point.X) * ratio - point.X);
            ImageScrollViewer.ScrollToVerticalOffset((ImageScrollViewer.VerticalOffset + point.Y) * ratio - point.Y);
        }

        private void ImageContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(ImageScrollViewer);
            _dragStartHorizontalOffset = ImageScrollViewer.HorizontalOffset;
            _dragStartVerticalOffset = ImageScrollViewer.VerticalOffset;
            ImageContainer.CaptureMouse();
            ImageContainer.Cursor = Cursors.SizeAll;
        }

        private void ImageContainer_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }

            var currentPoint = e.GetPosition(ImageScrollViewer);
            ImageScrollViewer.ScrollToHorizontalOffset(_dragStartHorizontalOffset - (currentPoint.X - _dragStartPoint.X));
            ImageScrollViewer.ScrollToVerticalOffset(_dragStartVerticalOffset - (currentPoint.Y - _dragStartPoint.Y));
        }

        private void ImageContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            EndDrag();
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(_zoom * 1.2);
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(_zoom / 1.2);
        }

        private void ResetZoomButton_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(1.0);
            ImageScrollViewer.ScrollToHorizontalOffset(0);
            ImageScrollViewer.ScrollToVerticalOffset(0);
        }

        private void SetZoom(double zoom)
        {
            _zoom = Math.Min(Math.Max(zoom, MinimumZoom), MaximumZoom);
            ImageScaleTransform.ScaleX = _zoom;
            ImageScaleTransform.ScaleY = _zoom;
            ImageContainer.LayoutTransform = ImageScaleTransform;
            UpdateZoomText();
        }

        private void UpdateZoomText()
        {
            ZoomText.Text = $"{_zoom:P0}";
        }

        private void EndDrag()
        {
            if (!_isDragging)
            {
                return;
            }

            _isDragging = false;
            ImageContainer.ReleaseMouseCapture();
            ImageContainer.Cursor = Cursors.Arrow;
        }
    }

    public class ResultImageDetailViewModel
    {
        public ResultImageDetailViewModel(TestImageResultRow result)
        {
            FileName = result.FileName;
            Image = LoadBitmap(result.FilePath);
            ImagePixelWidth = Image.PixelWidth;
            ImagePixelHeight = Image.PixelHeight;

            BrightnessX = result.BrightnessRoiX;
            BrightnessY = result.BrightnessRoiY;
            BrightnessW = Math.Max(0, result.BrightnessRoiW);
            BrightnessH = Math.Max(0, result.BrightnessRoiH);
            SharpnessX = result.SharpnessRoiX;
            SharpnessY = result.SharpnessRoiY;
            SharpnessW = Math.Max(0, result.SharpnessRoiW);
            SharpnessH = Math.Max(0, result.SharpnessRoiH);
            ShowSharpnessRoi = result.IncludeSharpness && SharpnessW > 0 && SharpnessH > 0;
        }

        public string FileName { get; }
        public BitmapImage Image { get; }
        public int ImagePixelWidth { get; }
        public int ImagePixelHeight { get; }
        public double BrightnessX { get; }
        public double BrightnessY { get; }
        public double BrightnessW { get; }
        public double BrightnessH { get; }
        public double SharpnessX { get; }
        public double SharpnessY { get; }
        public double SharpnessW { get; }
        public double SharpnessH { get; }
        public bool ShowSharpnessRoi { get; }

        private static BitmapImage LoadBitmap(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }
}
