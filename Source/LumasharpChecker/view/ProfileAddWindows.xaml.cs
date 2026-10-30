using CommunityToolkit.Mvvm.ComponentModel;
using LumasharpChecker.viewmodel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static ProfileManager;

namespace LumasharpChecker.view
{
    /// <summary>
    /// Interaction logic for ProfileAddWindows.xaml
    /// </summary>
    public partial class ProfileAddWindows : Window
    {
        private Point _startPoint;       // 記錄滑鼠起始點
        private Rectangle _currentRect;  // 當前活動的矩形框
        private bool _isDrawing = false; // 是否正在繪製中

        // 存儲所有已繪製的矩形和其座標

        public ProfileAddWindows()
        {
            InitializeComponent();
        }

        private void DrawingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 移除之前的矩形框（如果存在）
            RemovePreviousRect();

            _startPoint = e.GetPosition(DrawingCanvas);
            _currentRect = new Rectangle
            {
                Stroke = GetSelectedRoiBrush(),
                StrokeThickness = 4,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(_currentRect, _startPoint.X);
            Canvas.SetTop(_currentRect, _startPoint.Y);
            DrawingCanvas.Children.Add(_currentRect);
            _isDrawing = true;
        }

        private void RemovePreviousRect()
        {
            if (_currentRect != null)
            {
                DrawingCanvas.Children.Remove(_currentRect);
                _currentRect = null;
            }
        }

        private void DrawingCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing) return;

            var currentPoint = e.GetPosition(DrawingCanvas);
            var width = currentPoint.X - _startPoint.X;
            var height = currentPoint.Y - _startPoint.Y;

            _currentRect.Width = Math.Abs(width);
            _currentRect.Height = Math.Abs(height);

            // 修正左上角位置（支持任意方向拖動）
            Canvas.SetLeft(_currentRect, width >= 0 ? _startPoint.X : currentPoint.X);
            Canvas.SetTop(_currentRect, height >= 0 ? _startPoint.Y : currentPoint.Y);
        }

        private void DrawingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_currentRect == null)
            {
                e.Handled = true;
                return;
            }
            _isDrawing = false;

            // 計算矩形在原始圖片上的座標（考慮圖片縮放）
            var imageActualWidth = MyImage.ActualWidth;
            var imageActualHeight = MyImage.ActualHeight;
            var imageSource = (BitmapSource)MyImage.Source;
            var scaleX = imageSource.PixelWidth / imageActualWidth;
            var scaleY = imageSource.PixelHeight / imageActualHeight;

            var Roi = new Rect(
                Canvas.GetLeft(_currentRect) * scaleX,
                Canvas.GetTop(_currentRect) * scaleY,
                _currentRect.Width * scaleX,
                _currentRect.Height * scaleY
            );

            if (RB_PatternMatch.IsChecked ?? false)
                ((ProfileAddViewModel)DataContext).PatternMatchRoi = Roi;

            if (RB_Brightness.IsChecked ?? false)
            {
                ((ProfileAddViewModel)DataContext).BrightnessRoi = Roi;
                ((ProfileAddViewModel)DataContext).MonitorRoi = Roi;
            }

            if (RB_Sharpness.IsChecked ?? false)
                ((ProfileAddViewModel)DataContext).SharpnessRoi = Roi;
            //Trace.WriteLine(((ProfileAddViewModel)DataContext).Roi);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            RemovePreviousRect();
            ((ProfileAddViewModel)DataContext).PatternMatchRoi = new Rect(
                0,
                0,
                MyImage.Source.Width,
                MyImage.Source.Height
            );
        }
        private void RoiMode_Checked(object sender, RoutedEventArgs e)
        {
            if ((ProfileAddViewModel)DataContext == null) { return; }
            var viewModel = (ProfileAddViewModel)DataContext;
            if (!viewModel.IsSharpnessEnabled && (RB_Sharpness.IsChecked ?? false))
            {
                RB_Brightness.IsChecked = true;
                return;
            }

            RemovePreviousRect();
            var t = GetSelectedRoi();
            if (t.Width <= 0 || t.Height <= 0) { return; }

            _currentRect = new Rectangle
            {
                Stroke = GetSelectedRoiBrush(),
                StrokeThickness = 4,
                Fill = Brushes.Transparent,
                Width = t.Width,
                Height = t.Height
            };

            Canvas.SetLeft(_currentRect, t.X);
            Canvas.SetTop(_currentRect, t.Y);

            DrawingCanvas.Children.Add(_currentRect);
        }

        private Rect GetSelectedRoi()
        {
            var viewModel = (ProfileAddViewModel)DataContext;
            if (RB_PatternMatch.IsChecked ?? false) return viewModel.PatternMatchRoi;
            if (RB_Brightness.IsChecked ?? false) return viewModel.BrightnessRoi;
            if (RB_Sharpness.IsChecked ?? false) return viewModel.SharpnessRoi;
            return Rect.Empty;
        }

        private Brush GetSelectedRoiBrush()
        {
            if (RB_PatternMatch.IsChecked ?? false) return Brushes.LimeGreen;
            if (RB_Brightness.IsChecked ?? false) return Brushes.DeepSkyBlue;
            if (RB_Sharpness.IsChecked ?? false) return Brushes.Red;
            return Brushes.Red;
        }
    }
}
