using LumasharpChecker.viewmodel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LumasharpChecker;

public partial class StationSettingsWindow : Window
{
    private Point _startPoint;
    private Rectangle? _draftRectangle;
    private bool _isDrawing;
    private string _selectedRoi = "定位";

    public StationSettingsWindow(Window owner)
    {
        Resources.MergedDictionaries.Add(owner.Resources);
        InitializeComponent();
        Owner = owner;
        DataContext = owner.DataContext;
    }

    private void RoiType_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string roiType }) return;
        _selectedRoi = roiType;
        RoiCoordinateLabel.Text = $"拖曳圖片即可設定{roiType}範圍";
    }

    private void RoiCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not mainViewModel || RoiCanvas.ActualWidth <= 0 || RoiCanvas.ActualHeight <= 0)
            return;

        _startPoint = ClampPoint(e.GetPosition(RoiCanvas));
        _draftRectangle = new Rectangle
        {
            Stroke = GetSelectedBrush(),
            StrokeThickness = 3,
            StrokeDashArray = new DoubleCollection { 3, 2 },
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_draftRectangle, _startPoint.X);
        Canvas.SetTop(_draftRectangle, _startPoint.Y);
        RoiCanvas.Children.Add(_draftRectangle);
        _isDrawing = true;
        RoiCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void RoiCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDrawing || _draftRectangle == null) return;
        UpdateDraftRectangle(ClampPoint(e.GetPosition(RoiCanvas)));
    }

    private void RoiCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDrawing || _draftRectangle == null || DataContext is not mainViewModel viewModel)
            return;

        Point end = ClampPoint(e.GetPosition(RoiCanvas));
        UpdateDraftRectangle(end);
        double left = Math.Min(_startPoint.X, end.X);
        double top = Math.Min(_startPoint.Y, end.Y);
        int x = (int)Math.Round(left);
        int y = (int)Math.Round(top);
        int width = (int)Math.Round(Math.Abs(end.X - _startPoint.X));
        int height = (int)Math.Round(Math.Abs(end.Y - _startPoint.Y));

        _isDrawing = false;
        RoiCanvas.ReleaseMouseCapture();
        RoiCanvas.Children.Remove(_draftRectangle);
        _draftRectangle = null;

        if (width < 2 || height < 2)
        {
            e.Handled = true;
            return;
        }

        viewModel.UpdateStationRoi(_selectedRoi, x, y, width, height);
        RoiCoordinateLabel.Text = $"{_selectedRoi} ROI 已儲存（原始影像像素）";
        e.Handled = true;
    }

    private Point ClampPoint(Point point)
    {
        double maxX = Math.Max(0, RoiCanvas.ActualWidth);
        double maxY = Math.Max(0, RoiCanvas.ActualHeight);
        return new Point(Math.Min(Math.Max(point.X, 0), maxX), Math.Min(Math.Max(point.Y, 0), maxY));
    }

    private void UpdateDraftRectangle(Point end)
    {
        if (_draftRectangle == null) return;
        Canvas.SetLeft(_draftRectangle, Math.Min(_startPoint.X, end.X));
        Canvas.SetTop(_draftRectangle, Math.Min(_startPoint.Y, end.Y));
        _draftRectangle.Width = Math.Abs(end.X - _startPoint.X);
        _draftRectangle.Height = Math.Abs(end.Y - _startPoint.Y);
    }

    private Brush GetSelectedBrush() => _selectedRoi switch
    {
        "定位" => Brushes.LimeGreen,
        "亮度" => Brushes.DeepSkyBlue,
        _ => Brushes.Red
    };

    private void Done_Click(object sender, RoutedEventArgs e) => Close();
}
