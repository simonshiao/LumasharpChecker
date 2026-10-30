using LumasharpChecker.model;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DrawingRectangle = System.Drawing.Rectangle;
using RoiRectangle = System.Windows.Shapes.Rectangle;

namespace LumasharpChecker.view
{
    /// <summary>互動式多外觀校準流程試作。此試作版不會寫入正式 Recipe。</summary>
    public sealed class MultiAppearanceCalibrationPreviewWindow : Window
    {
        private sealed class AppearanceGroup
        {
            public string Name { get; set; } = "";
            public List<string> Images { get; } = new();
            public DrawingRectangle PatternRoi { get; set; }
            public DrawingRectangle BrightnessRoi { get; set; }
            public DrawingRectangle SharpnessRoi { get; set; }
            public string Statistics { get; set; } = "尚未計算統計";
            public override string ToString() => $"{Name}　{Images.Count} 張";
        }

        private readonly ListBox _groups = new();
        private readonly ListBox _images = new();
        private readonly Image _image = new() { Stretch = Stretch.Fill, IsHitTestVisible = false };
        private readonly Canvas _canvas = new() { Background = Brushes.Transparent };
        private readonly TextBlock _emptyPreview = new() { Text = "目前外觀組沒有圖片\n請按「選擇圖片…」加入樣本", Foreground = Brushes.White, FontSize = 18, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false };
        private readonly TextBlock _roiText = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) };
        private readonly TextBlock _statistics = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        private readonly ComboBox _mode = new() { Width = 210, Margin = new Thickness(0, 0, 8, 0) };
        private readonly List<AppearanceGroup> _groupData = new();
        private string _roiMode = "定位框";
        private Point _drawStart;
        private RoiRectangle? _draft;
        private bool _drawing;
        private int _nextGroup = 1;

        public MultiAppearanceCalibrationPreviewWindow(Window? owner, IEnumerable<string>? initialImages = null)
        {
            Title = "多外觀參數校準｜流程試作";
            Width = 1220;
            Height = 790;
            MinWidth = 1000;
            MinHeight = 660;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Microsoft JhengHei UI");
            FontSize = 13;
            Background = new SolidColorBrush(Color.FromRgb(239, 248, 253));
            Foreground = new SolidColorBrush(Color.FromRgb(17, 48, 68));
            if (owner != null) Owner = owner;

            _mode.Items.Add("新增參數：建立外觀組");
            _mode.Items.Add("編輯參數：調整選取外觀組");
            _mode.Items.Add("重設參數：只重設選取外觀組");
            _mode.SelectedIndex = 0;
            _groups.SelectionChanged += (_, _) => RefreshGroup();
            _images.SelectionChanged += (_, _) => RefreshPreview();
            _canvas.MouseLeftButtonDown += CanvasMouseDown;
            _canvas.MouseMove += CanvasMouseMove;
            _canvas.MouseLeftButtonUp += CanvasMouseUp;

            Content = BuildLayout();
            if (initialImages != null)
            {
                var available = initialImages.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (available.Count > 0)
                {
                    var group = NewGroup("外觀組 1");
                    group.Images.AddRange(available);
                    RefreshGroup();
                }
            }
            if (_groupData.Count == 0) AddGroup("外觀組 1");
            _nextGroup = _groupData.Count + 1;
        }

        private UIElement BuildLayout()
        {
            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            header.Children.Add(new TextBlock { Text = "多外觀參數校準", FontSize = 22, FontWeight = FontWeights.Bold });
            header.Children.Add(new TextBlock
            {
                Text = "每個外觀組獨立設定定位／ROI，並以該組樣本單獨計算統計。先建立組別，再把同方向、同外觀的 OK 圖片放在一起。",
                TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(76, 105, 125)), Margin = new Thickness(0, 4, 0, 0)
            });
            var prototypeNote = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 248, 225)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(235, 189, 72)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(9, 6, 9, 6), Margin = new Thickness(0, 8, 0, 0),
                Child = new TextBlock { Text = "流程試作版：可載圖、分組、畫 ROI、計算各組統計；暫不寫入 Recipe，也不改變正式檢測判定。", Foreground = new SolidColorBrush(Color.FromRgb(122, 84, 0)), FontWeight = FontWeights.SemiBold }
            };
            header.Children.Add(prototypeNote);
            root.Children.Add(header);

            var body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(body, 1);
            root.Children.Add(body);

            var left = new Grid { Margin = new Thickness(0, 0, 12, 0) };
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.Children.Add(new TextBlock { Text = "作業模式", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
            Grid.SetRow(_mode, 1); left.Children.Add(_mode);
            var groupHeader = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 12, 0, 4) };
            groupHeader.Children.Add(new TextBlock { Text = "外觀組", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            var addGroup = SmallButton("＋ 新增組"); addGroup.Click += (_, _) => AddGroup(); DockPanel.SetDock(addGroup, Dock.Right); groupHeader.Children.Add(addGroup);
            var removeGroup = SmallButton("移除組"); removeGroup.Click += (_, _) => RemoveGroup(); DockPanel.SetDock(removeGroup, Dock.Right); groupHeader.Children.Add(removeGroup);
            Grid.SetRow(groupHeader, 2); left.Children.Add(groupHeader);
            _groups.Margin = new Thickness(0, 0, 0, 8);
            _groups.SelectionMode = SelectionMode.Single;
            Grid.SetRow(_groups, 3); left.Children.Add(_groups);

            var imagePanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            imagePanel.Children.Add(new TextBlock { Text = "本組 OK 樣本", FontWeight = FontWeights.SemiBold });
            _images.Height = 118; _images.Margin = new Thickness(0, 5, 0, 5);
            imagePanel.Children.Add(_images);
            var imageButtons = new StackPanel { Orientation = Orientation.Horizontal };
            var choose = SmallButton("選擇圖片…"); choose.Click += (_, _) => ChooseImages(); imageButtons.Children.Add(choose);
            var removeImage = SmallButton("移出本組"); removeImage.Click += (_, _) => RemoveImage(); imageButtons.Children.Add(removeImage);
            imagePanel.Children.Add(imageButtons);
            Grid.SetRow(imagePanel, 4); left.Children.Add(imagePanel);
            var reset = SmallButton("重設選取組 ROI"); reset.Margin = new Thickness(0, 9, 0, 0); reset.Click += (_, _) => ResetSelectedGroup();
            Grid.SetRow(reset, 5); left.Children.Add(reset);
            body.Children.Add(left);

            var right = new Grid();
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var roiPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            roiPanel.Children.Add(new TextBlock { Text = "目前繪製：", VerticalAlignment = VerticalAlignment.Center });
            foreach (string mode in new[] { "定位框", "亮度 ROI", "銳利度 ROI" })
            {
                var radio = new RadioButton { Content = mode, GroupName = "AppearanceRoiMode", IsChecked = mode == _roiMode, Margin = new Thickness(6, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
                radio.Checked += (_, _) => { _roiMode = mode; DrawRois(); };
                roiPanel.Children.Add(radio);
            }
            var calculate = SmallButton("計算本組統計"); calculate.Click += async (_, _) => await CalculateGroupAsync();
            DockPanel roiDock = new() { LastChildFill = false }; roiDock.Children.Add(roiPanel); DockPanel.SetDock(calculate, Dock.Right); roiDock.Children.Add(calculate);
            Grid.SetRow(roiDock, 0); right.Children.Add(roiDock);

            var viewport = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(7, 24, 40)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 142, 177)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(8)
            };
            var viewbox = new Viewbox { Stretch = Stretch.Uniform };
            var canvasGrid = new Grid(); canvasGrid.Children.Add(_image); canvasGrid.Children.Add(_canvas); viewbox.Child = canvasGrid;
            var viewportContent = new Grid(); viewportContent.Children.Add(viewbox); viewportContent.Children.Add(_emptyPreview); viewport.Child = viewportContent;
            Grid.SetRow(viewport, 1); right.Children.Add(viewport);
            _roiText.Foreground = new SolidColorBrush(Color.FromRgb(50, 88, 110));
            Grid.SetRow(_roiText, 2); right.Children.Add(_roiText);
            var statPanel = new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(196, 222, 234)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(9), Margin = new Thickness(0, 8, 0, 0), Child = _statistics };
            Grid.SetRow(statPanel, 3); right.Children.Add(statPanel);
            Grid.SetColumn(right, 1);
            body.Children.Add(right);
            return root;
        }

        private static Button SmallButton(string text) => new()
        {
            Content = text, Padding = new Thickness(9, 5, 9, 5), Margin = new Thickness(0, 0, 5, 0),
            Background = new SolidColorBrush(Color.FromRgb(229, 244, 251)), BorderBrush = new SolidColorBrush(Color.FromRgb(95, 188, 218)),
            Foreground = new SolidColorBrush(Color.FromRgb(14, 86, 120)), Cursor = Cursors.Hand
        };

        private AppearanceGroup NewGroup(string name) => new() { Name = name };

        private void AddGroup(string? name = null)
        {
            var group = NewGroup(name ?? $"外觀組 {_nextGroup++}");
            _groupData.Add(group);
            _groups.Items.Add(group);
            _groups.SelectedItem = group;
        }

        private void RemoveGroup()
        {
            if (SelectedGroup == null || _groupData.Count <= 1) return;
            int index = _groups.SelectedIndex;
            _groupData.Remove(SelectedGroup);
            _groups.Items.Remove(SelectedGroup);
            _groups.SelectedIndex = Math.Max(0, Math.Min(index, _groups.Items.Count - 1));
        }

        private AppearanceGroup? SelectedGroup => _groups.SelectedItem as AppearanceGroup;

        private void ChooseImages()
        {
            var dialog = new OpenFileDialog { Title = "選擇要加入本外觀組的 OK 樣本", Filter = "圖片|*.jpg;*.jpeg;*.png;*.bmp;*.gif", Multiselect = true };
            if (dialog.ShowDialog(this) != true || SelectedGroup == null) return;
            foreach (var path in dialog.FileNames)
                if (!SelectedGroup.Images.Contains(path, StringComparer.OrdinalIgnoreCase)) SelectedGroup.Images.Add(path);
            RefreshGroup();
        }

        private void RemoveImage()
        {
            if (SelectedGroup == null || _images.SelectedItem is not string path) return;
            SelectedGroup.Images.Remove(path);
            RefreshGroup();
        }

        private void ResetSelectedGroup()
        {
            if (SelectedGroup == null) return;
            SelectedGroup.PatternRoi = DrawingRectangle.Empty;
            SelectedGroup.BrightnessRoi = DrawingRectangle.Empty;
            SelectedGroup.SharpnessRoi = DrawingRectangle.Empty;
            SelectedGroup.Statistics = "尚未計算";
            RefreshGroup();
        }

        private void RefreshGroup()
        {
            _groups.Items.Refresh();
            _images.ItemsSource = null;
            _images.ItemsSource = SelectedGroup?.Images.ToList();
            if (_images.Items.Count > 0) _images.SelectedIndex = 0;
            RefreshPreview();
            _statistics.Text = SelectedGroup == null ? "請先建立外觀組。" : SelectedGroup.Statistics;
        }

        private void RefreshPreview()
        {
            var path = _images.SelectedItem as string ?? SelectedGroup?.Images.FirstOrDefault();
            _canvas.Children.Clear();
            _image.Source = null;
            if (path == null || !File.Exists(path)) { _emptyPreview.Visibility = Visibility.Visible; _roiText.Text = "先選擇圖片，再在影像上拖曳繪製目前選取的 ROI。"; return; }
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(path); bitmap.EndInit(); bitmap.Freeze();
                _image.Source = bitmap;
                _emptyPreview.Visibility = Visibility.Collapsed;
                _canvas.Width = bitmap.PixelWidth; _canvas.Height = bitmap.PixelHeight;
                DrawRois();
            }
            catch (Exception ex) { _emptyPreview.Visibility = Visibility.Visible; _emptyPreview.Text = "圖片載入失敗\n" + ex.Message; _roiText.Text = "圖片載入失敗：" + ex.Message; }
        }

        private void DrawRois()
        {
            _canvas.Children.Clear();
            var group = SelectedGroup;
            if (group == null) return;
            AddRoi(group.PatternRoi, Brushes.LimeGreen, "定位");
            AddRoi(group.BrightnessRoi, Brushes.DeepSkyBlue, "亮度");
            AddRoi(group.SharpnessRoi, Brushes.OrangeRed, "銳利度");
            _roiText.Text = $"{group.Name} · 樣本 {group.Images.Count} 張\n定位：{Format(group.PatternRoi)}　亮度：{Format(group.BrightnessRoi)}　銳利度：{Format(group.SharpnessRoi)}";
        }

        private void AddRoi(DrawingRectangle roi, Brush brush, string label)
        {
            if (roi.Width <= 0 || roi.Height <= 0) return;
            var shape = new RoiRectangle { Width = roi.Width, Height = roi.Height, Stroke = brush, StrokeThickness = Math.Max(2, _canvas.Width / 500), Fill = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)), IsHitTestVisible = false };
            Canvas.SetLeft(shape, roi.X); Canvas.SetTop(shape, roi.Y); _canvas.Children.Add(shape);
            var text = new TextBlock { Text = label, Foreground = brush, FontWeight = FontWeights.Bold, FontSize = Math.Max(12, _canvas.Width / 80), IsHitTestVisible = false };
            Canvas.SetLeft(text, roi.X); Canvas.SetTop(text, Math.Max(0, roi.Y - text.FontSize - 2)); _canvas.Children.Add(text);
        }

        private void CanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_image.Source == null || SelectedGroup == null) return;
            _drawStart = e.GetPosition(_canvas);
            _draft = new RoiRectangle { Stroke = _roiMode == "定位框" ? Brushes.LimeGreen : _roiMode == "亮度 ROI" ? Brushes.DeepSkyBlue : Brushes.OrangeRed, StrokeThickness = Math.Max(2, _canvas.Width / 500), Fill = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)) };
            Canvas.SetLeft(_draft, _drawStart.X); Canvas.SetTop(_draft, _drawStart.Y); _canvas.Children.Add(_draft); _drawing = true; _canvas.CaptureMouse();
        }

        private void CanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (!_drawing || _draft == null) return;
            var point = e.GetPosition(_canvas); var x = Math.Min(_drawStart.X, point.X); var y = Math.Min(_drawStart.Y, point.Y);
            _draft.Width = Math.Abs(point.X - _drawStart.X); _draft.Height = Math.Abs(point.Y - _drawStart.Y);
            Canvas.SetLeft(_draft, x); Canvas.SetTop(_draft, y);
        }

        private void CanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_drawing || _draft == null || SelectedGroup == null) return;
            _drawing = false; _canvas.ReleaseMouseCapture();
            var roi = new DrawingRectangle((int)Canvas.GetLeft(_draft), (int)Canvas.GetTop(_draft), (int)_draft.Width, (int)_draft.Height);
            if (roi.Width <= 0 || roi.Height <= 0) { DrawRois(); return; }
            if (_roiMode == "定位框") SelectedGroup.PatternRoi = roi;
            else if (_roiMode == "亮度 ROI") SelectedGroup.BrightnessRoi = roi;
            else SelectedGroup.SharpnessRoi = roi;
            _draft = null; DrawRois();
        }

        private async Task CalculateGroupAsync()
        {
            var group = SelectedGroup;
            if (group == null || group.Images.Count == 0) { _statistics.Text = "請先為本組加入 OK 樣本。"; return; }
            if (group.PatternRoi.Width <= 0 || group.BrightnessRoi.Width <= 0) { _statistics.Text = "請先畫定位框和亮度 ROI。"; return; }
            try
            {
                var processor = new ImageProcessor();
                processor.patternmatchroi = group.PatternRoi;
                processor.brightnessroi = group.BrightnessRoi;
                processor.monitorroi = group.BrightnessRoi;
                processor.sharpnessroi = group.SharpnessRoi.Width > 0 ? group.SharpnessRoi : group.BrightnessRoi;
                var result = await processor.CalculateReferenceValues(group.Images.ToList());
                if (result?.Brightness == null) { _statistics.Text = "本組沒有可用的統計結果。"; return; }
                string sharpness = result.Sharpness == null ? "未計算" : $"平均 {result.Sharpness.Mean:F2}，σ {result.Sharpness.Sigma:F2}";
                group.Statistics = $"{group.Name}：成功計算 {result.BrightnessValues?.Count ?? 0} / {group.Images.Count} 張\n" +
                    $"亮度：平均 {result.Brightness.Mean:F2}，σ {result.Brightness.Sigma:F2}，3σ {result.Brightness.ThreeSigmaLower:F2}～{result.Brightness.ThreeSigmaUpper:F2}\n" +
                    $"銳利度：{sharpness}\n統計只使用本組樣本與本組 ROI。";
                _statistics.Text = group.Statistics;
            }
            catch (Exception ex) { _statistics.Text = "統計計算失敗：" + ex.Message; }
        }

        private static string Format(DrawingRectangle roi) => roi.Width <= 0 || roi.Height <= 0 ? "未設定" : $"({roi.X},{roi.Y},{roi.Width},{roi.Height})";
    }
}
