using Emgu.CV;
using Emgu.CV.CvEnum;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using WpfPoint = System.Windows.Point;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using LayoutOrientation = System.Windows.Controls.Orientation;

namespace LumasharpChecker.view
{
    /// <summary>
    /// AOI locator prototype. All images, ROIs, and thresholds are temporary window state;
    /// this tool is intentionally independent from Recipe and production inspection flows.
    /// </summary>
    public sealed class AoiToolsPreviewWindow : Window
    {
        private sealed class SampleItem : INotifyPropertyChanged
        {
            private bool _enabled = true;

            public string Path { get; }
            public string DisplayName => System.IO.Path.GetFileName(Path);
            public DrawingRectangle PatternRoi { get; set; }
            public bool Enabled
            {
                get => _enabled;
                set { if (_enabled == value) return; _enabled = value; OnPropertyChanged(); }
            }

            public SampleItem(string path) => Path = path;
            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string? name = null) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            public override string ToString() => DisplayName;
        }

        private sealed class SampleSnapshot
        {
            public string Path { get; set; } = "";
            public DrawingRectangle Roi { get; set; }
        }

        private sealed class LocateResult
        {
            public string Path { get; set; } = "";
            public DrawingRectangle Bounds { get; set; }
            public double Score { get; set; } = double.NegativeInfinity;
            public int Tested { get; set; }
            public int Skipped { get; set; }
        }

        private readonly List<SampleItem> _sampleData = new();
        private readonly ListBox _samples = new();
        private readonly ComboBox _previewMode = new() { MinWidth = 145 };
        private readonly Image _image = new() { Stretch = Stretch.Fill, IsHitTestVisible = false };
        private readonly Canvas _overlay = new() { Background = Brushes.Transparent };
        private readonly TextBlock _emptyMessage = new()
        {
            Text = "加入定位樣板，或先選擇目標圖片",
            Foreground = Brushes.White,
            FontSize = 17,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsHitTestVisible = false
        };
        private readonly TextBlock _targetPathText = new() { Text = "尚未選擇目標圖片", TextTrimming = TextTrimming.CharacterEllipsis };
        private readonly TextBlock _sampleInfoText = new() { TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _resultText = new() { Text = "尚未執行定位", TextWrapping = TextWrapping.Wrap };
        private readonly TextBox _thresholdBox = new() { Text = "0.70", Width = 74, VerticalContentAlignment = VerticalAlignment.Center };
        private readonly Button _locateButton = new() { Content = "執行定位", Padding = new Thickness(12, 7, 12, 7) };
        private string? _targetPath;
        private DrawingRectangle _searchRoi;
        private DrawingRectangle _matchBounds;
        private double _matchScore = double.NaN;
        private WpfPoint _dragStart;
        private WpfRectangle? _draft;
        private bool _drawing;
        private bool _locatorEnabled = true;
        private bool _isLocating;

        public AoiToolsPreviewWindow(Window owner)
        {
            Title = "AOI工具｜定位測試";
            Width = 1280;
            Height = 820;
            MinWidth = 1040;
            MinHeight = 650;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Microsoft JhengHei UI");
            FontSize = 13;
            Background = BrushFromRgb(239, 248, 253);
            Foreground = BrushFromRgb(17, 48, 68);
            Owner = owner;

            _previewMode.Items.Add("樣板圖：設定定位框");
            _previewMode.Items.Add("目標圖：設定搜尋區");
            _previewMode.SelectedIndex = 0;
            _previewMode.SelectionChanged += (_, _) => RefreshPreview();
            _samples.SelectionChanged += (_, _) =>
            {
                if (SelectedSample != null) _previewMode.SelectedIndex = 0;
                RefreshSampleInfo();
                RefreshPreview();
            };
            _overlay.MouseLeftButtonDown += Overlay_MouseLeftButtonDown;
            _overlay.MouseMove += Overlay_MouseMove;
            _overlay.MouseLeftButtonUp += Overlay_MouseLeftButtonUp;
            _locateButton.Click += async (_, _) => await RunLocateAsync();

            Content = BuildLayout();
            RefreshSampleInfo();
            RefreshPreview();
        }

        private UIElement BuildLayout()
        {
            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titlePanel = new StackPanel();
            titlePanel.Children.Add(new TextBlock { Text = "AOI工具", FontSize = 23, FontWeight = FontWeights.Bold });
            titlePanel.Children.Add(new TextBlock
            {
                Text = "獨立定位測試｜CPU 樣板比對｜不連接 InspTools AI、不修改正式檢測參數",
                Foreground = BrushFromRgb(76, 105, 122),
                Margin = new Thickness(0, 3, 0, 0)
            });
            header.Children.Add(titlePanel);
            var close = MakeButton("關閉");
            close.Click += (_, _) => Close();
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(285) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            Grid.SetRow(body, 1);
            root.Children.Add(body);

            var left = MakePanel();
            left.Margin = new Thickness(0, 0, 10, 0);
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.Children.Add(PanelHeading("定位樣板"));
            var sampleButtons = new StackPanel { Orientation = LayoutOrientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
            var add = MakeButton("加入圖片…"); add.Click += (_, _) => AddSamples(); sampleButtons.Children.Add(add);
            var remove = MakeButton("移除"); remove.Margin = new Thickness(6, 0, 0, 0); remove.Click += (_, _) => RemoveSelectedSample(); sampleButtons.Children.Add(remove);
            Grid.SetRow(sampleButtons, 1); left.Children.Add(sampleButtons);
            _samples.SelectionMode = SelectionMode.Single;
            _samples.ItemTemplate = BuildSampleTemplate();
            _samples.BorderBrush = BrushFromRgb(196, 222, 234);
            _samples.BorderThickness = new Thickness(1);
            _samples.Background = Brushes.White;
            Grid.SetRow(_samples, 2); left.Children.Add(_samples);
            _sampleInfoText.Margin = new Thickness(0, 9, 0, 0);
            _sampleInfoText.Foreground = BrushFromRgb(76, 105, 122);
            Grid.SetRow(_sampleInfoText, 3); left.Children.Add(_sampleInfoText);
            body.Children.Add(left);

            var center = new Grid { Margin = new Thickness(0, 0, 10, 0) };
            center.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            center.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            center.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var previewHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 8), LastChildFill = false };
            previewHeader.Children.Add(new TextBlock { Text = "影像與定位框", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            DockPanel.SetDock(_previewMode, Dock.Right); previewHeader.Children.Add(_previewMode);
            Grid.SetRow(previewHeader, 0); center.Children.Add(previewHeader);

            var viewport = new Border
            {
                Background = BrushFromRgb(7, 24, 40),
                BorderBrush = BrushFromRgb(48, 142, 177),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8)
            };
            var viewbox = new Viewbox { Stretch = Stretch.Uniform };
            var imageGrid = new Grid();
            imageGrid.Children.Add(_image);
            imageGrid.Children.Add(_overlay);
            viewbox.Child = imageGrid;
            var viewportGrid = new Grid();
            viewportGrid.Children.Add(viewbox);
            viewportGrid.Children.Add(_emptyMessage);
            viewport.Child = viewportGrid;
            Grid.SetRow(viewport, 1); center.Children.Add(viewport);
            var instructions = new TextBlock
            {
                Text = "在影像上拖曳設定框：樣板圖畫出要比對的特徵；目標圖可縮小搜尋範圍。不畫搜尋框時會搜尋整張圖。",
                TextWrapping = TextWrapping.Wrap,
                Foreground = BrushFromRgb(76, 105, 122),
                Margin = new Thickness(0, 8, 0, 0)
            };
            Grid.SetRow(instructions, 2); center.Children.Add(instructions);
            Grid.SetColumn(center, 1); body.Children.Add(center);

            var right = MakePanel();
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var toolsHeading = PanelHeading("工具");
            Grid.SetRow(toolsHeading, 0); right.Children.Add(toolsHeading);
            var locatorTool = new Button
            {
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 9, 10, 9),
                Margin = new Thickness(0, 8, 0, 12),
                Background = BrushFromRgb(224, 246, 253),
                BorderBrush = BrushFromRgb(65, 193, 226),
                ToolTip = "CPU 樣板定位"
            };
            var toolContent = new StackPanel { Orientation = LayoutOrientation.Horizontal };
            toolContent.Children.Add(new TextBlock { Text = "⌖", FontSize = 24, Foreground = BrushFromRgb(0, 137, 190), Width = 34, VerticalAlignment = VerticalAlignment.Center });
            var toolLabel = new StackPanel();
            toolLabel.Children.Add(new TextBlock { Text = "定位", FontWeight = FontWeights.SemiBold });
            toolLabel.Children.Add(new TextBlock { Text = "樣板特徵比對", FontSize = 11, Foreground = BrushFromRgb(76, 105, 122) });
            toolContent.Children.Add(toolLabel);
            locatorTool.Content = toolContent;
            Grid.SetRow(locatorTool, 1); right.Children.Add(locatorTool);

            var settingsHeader = PanelHeading("定位設定");
            Grid.SetRow(settingsHeader, 2); right.Children.Add(settingsHeader);
            var targetBlock = new StackPanel { Margin = new Thickness(0, 8, 0, 10) };
            var chooseTarget = MakeButton("選擇目標圖片…"); chooseTarget.Click += (_, _) => ChooseTarget(); targetBlock.Children.Add(chooseTarget);
            _targetPathText.Margin = new Thickness(0, 6, 0, 0);
            _targetPathText.Foreground = BrushFromRgb(76, 105, 122);
            targetBlock.Children.Add(_targetPathText);
            Grid.SetRow(targetBlock, 3); right.Children.Add(targetBlock);

            var thresholdPanel = new StackPanel { Orientation = LayoutOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            thresholdPanel.Children.Add(new TextBlock { Text = "相似度門檻", VerticalAlignment = VerticalAlignment.Center });
            _thresholdBox.Margin = new Thickness(9, 0, 5, 0);
            thresholdPanel.Children.Add(_thresholdBox);
            thresholdPanel.Children.Add(new TextBlock { Text = "0～1", Foreground = BrushFromRgb(76, 105, 122), VerticalAlignment = VerticalAlignment.Center });
            Grid.SetRow(thresholdPanel, 4); right.Children.Add(thresholdPanel);

            var resultCard = new Border
            {
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(196, 222, 234),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10),
                Child = _resultText
            };
            Grid.SetRow(resultCard, 5); right.Children.Add(resultCard);
            _locateButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            _locateButton.Background = BrushFromRgb(0, 128, 182);
            _locateButton.Foreground = Brushes.White;
            _locateButton.BorderBrush = BrushFromRgb(0, 110, 158);
            Grid.SetRow(_locateButton, 6); right.Children.Add(_locateButton);
            Grid.SetColumn(right, 2); body.Children.Add(right);
            return root;
        }

        private static Grid MakePanel()
        {
            return new Grid { Background = Brushes.White, Margin = new Thickness(0), ClipToBounds = true };
        }

        private static TextBlock PanelHeading(string text) => new()
        {
            Text = text,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFromRgb(17, 48, 68)
        };

        private static Button MakeButton(string text) => new()
        {
            Content = text,
            Padding = new Thickness(9, 5, 9, 5),
            Background = BrushFromRgb(229, 244, 251),
            BorderBrush = BrushFromRgb(95, 188, 218),
            Foreground = BrushFromRgb(14, 86, 120),
            Cursor = Cursors.Hand
        };

        private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

        private static DataTemplate BuildSampleTemplate()
        {
            var row = new FrameworkElementFactory(typeof(StackPanel));
            row.SetValue(StackPanel.OrientationProperty, LayoutOrientation.Horizontal);
            row.SetValue(FrameworkElement.MarginProperty, new Thickness(5, 4, 5, 4));
            var check = new FrameworkElementFactory(typeof(CheckBox));
            check.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            check.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(SampleItem.Enabled)) { Mode = BindingMode.TwoWay });
            check.SetValue(FrameworkElement.ToolTipProperty, "參與本次定位");
            row.AppendChild(check);
            var name = new FrameworkElementFactory(typeof(TextBlock));
            name.SetValue(FrameworkElement.MarginProperty, new Thickness(7, 0, 0, 0));
            name.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            name.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            name.SetBinding(TextBlock.TextProperty, new Binding(nameof(SampleItem.DisplayName)));
            name.SetBinding(FrameworkElement.ToolTipProperty, new Binding(nameof(SampleItem.Path)));
            row.AppendChild(name);
            return new DataTemplate { VisualTree = row };
        }

        private SampleItem? SelectedSample => _samples.SelectedItem as SampleItem;
        private bool IsTargetPreview => _previewMode.SelectedIndex == 1;
        private string? CurrentPreviewPath => IsTargetPreview ? _targetPath : SelectedSample?.Path;

        private void AddSamples()
        {
            var dialog = new OpenFileDialog
            {
                Title = "選擇定位樣板圖片（可多選）",
                Filter = "圖片|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = true
            };
            if (dialog.ShowDialog(this) != true) return;
            foreach (string path in dialog.FileNames)
                if (!_sampleData.Any(s => string.Equals(s.Path, path, StringComparison.OrdinalIgnoreCase)))
                {
                    var sample = new SampleItem(path);
                    sample.PropertyChanged += (_, _) => RefreshSampleInfo();
                    _sampleData.Add(sample);
                }
            _samples.ItemsSource = null;
            _samples.ItemsSource = _sampleData;
            if (_samples.Items.Count > 0 && _samples.SelectedIndex < 0) _samples.SelectedIndex = 0;
            RefreshSampleInfo();
            RefreshPreview();
        }

        private void RemoveSelectedSample()
        {
            var selected = SelectedSample;
            if (selected == null) return;
            int index = _samples.SelectedIndex;
            _sampleData.Remove(selected);
            _samples.ItemsSource = null;
            _samples.ItemsSource = _sampleData;
            if (_samples.Items.Count > 0) _samples.SelectedIndex = Math.Min(index, _samples.Items.Count - 1);
            RefreshSampleInfo();
            RefreshPreview();
        }

        private void ChooseTarget()
        {
            var dialog = new OpenFileDialog
            {
                Title = "選擇要定位的目標圖片",
                Filter = "圖片|*.jpg;*.jpeg;*.png;*.bmp"
            };
            if (dialog.ShowDialog(this) != true) return;
            SetTarget(dialog.FileName);
        }

        public void UseInitialTarget(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

            // Reopening the locator follows the currently selected inspection image,
            // while keeping its manually drawn search ROI if that image is unchanged.
            if (!string.Equals(_targetPath, path, StringComparison.OrdinalIgnoreCase))
                SetTarget(path);
        }

        private void SetTarget(string path)
        {
            _targetPath = path;
            _targetPathText.Text = path;
            _targetPathText.ToolTip = path;
            _searchRoi = DrawingRectangle.Empty;
            ClearMatch();
            _previewMode.SelectedIndex = 1;
            RefreshPreview();
        }

        private void RefreshSampleInfo()
        {
            int enabled = _sampleData.Count(s => s.Enabled);
            var selected = SelectedSample;
            string roi = selected == null || selected.PatternRoi.IsEmpty
                ? "未設定"
                : $"({selected.PatternRoi.X}, {selected.PatternRoi.Y}, {selected.PatternRoi.Width}, {selected.PatternRoi.Height})";
            _sampleInfoText.Text = $"已加入 {_sampleData.Count} 張，勾選 {enabled} 張\n選取樣板定位框：{roi}";
        }

        private void RefreshPreview()
        {
            _overlay.Children.Clear();
            _image.Source = null;
            string? path = CurrentPreviewPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                _overlay.Width = 0; _overlay.Height = 0;
                _emptyMessage.Text = IsTargetPreview ? "請先選擇目標圖片" : "請加入並選取一張定位樣板";
                _emptyMessage.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(path); bitmap.EndInit(); bitmap.Freeze();
                _image.Source = bitmap;
                _overlay.Width = bitmap.PixelWidth;
                _overlay.Height = bitmap.PixelHeight;
                _emptyMessage.Visibility = Visibility.Collapsed;
                DrawCurrentOverlays();
            }
            catch (Exception ex)
            {
                _emptyMessage.Text = "圖片載入失敗\n" + ex.Message;
                _emptyMessage.Visibility = Visibility.Visible;
            }
        }

        private void DrawCurrentOverlays()
        {
            _overlay.Children.Clear();
            if (IsTargetPreview)
            {
                if (!_searchRoi.IsEmpty) AddOverlay(_searchRoi, Brushes.Gold, "搜尋區", true);
                if (!_matchBounds.IsEmpty)
                    AddOverlay(_matchBounds, Brushes.DeepSkyBlue, $"定位 {(_matchScore * 100):F1}%", false);
                return;
            }
            if (SelectedSample != null && !SelectedSample.PatternRoi.IsEmpty)
                AddOverlay(SelectedSample.PatternRoi, Brushes.LimeGreen, "樣板定位框", false);
        }

        private void AddOverlay(DrawingRectangle bounds, Brush brush, string label, bool dashed)
        {
            var shape = new WpfRectangle
            {
                Width = bounds.Width,
                Height = bounds.Height,
                Stroke = brush,
                StrokeThickness = Math.Max(2, _overlay.Width / 600),
                Fill = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)),
                IsHitTestVisible = false
            };
            if (dashed) shape.StrokeDashArray = new DoubleCollection { 5, 3 };
            Canvas.SetLeft(shape, bounds.X); Canvas.SetTop(shape, bounds.Y); _overlay.Children.Add(shape);
            var text = new TextBlock
            {
                Text = label,
                Foreground = brush,
                FontWeight = FontWeights.Bold,
                FontSize = Math.Max(12, _overlay.Width / 90),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(text, bounds.X); Canvas.SetTop(text, Math.Max(0, bounds.Y - text.FontSize - 2)); _overlay.Children.Add(text);
        }

        private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_image.Source == null || (!IsTargetPreview && SelectedSample == null)) return;
            _dragStart = ClampToImage(e.GetPosition(_overlay));
            _draft = new WpfRectangle
            {
                Stroke = IsTargetPreview ? Brushes.Gold : Brushes.LimeGreen,
                StrokeThickness = Math.Max(2, _overlay.Width / 600),
                Fill = new SolidColorBrush(Color.FromArgb(22, 255, 255, 255))
            };
            Canvas.SetLeft(_draft, _dragStart.X); Canvas.SetTop(_draft, _dragStart.Y); _overlay.Children.Add(_draft);
            _drawing = true; _overlay.CaptureMouse();
            e.Handled = true;
        }

        private void Overlay_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_drawing || _draft == null) return;
            WpfPoint p = ClampToImage(e.GetPosition(_overlay));
            Canvas.SetLeft(_draft, Math.Min(_dragStart.X, p.X)); Canvas.SetTop(_draft, Math.Min(_dragStart.Y, p.Y));
            _draft.Width = Math.Abs(p.X - _dragStart.X); _draft.Height = Math.Abs(p.Y - _dragStart.Y);
        }

        private void Overlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_drawing || _draft == null) return;
            _drawing = false; _overlay.ReleaseMouseCapture();
            WpfPoint p = ClampToImage(e.GetPosition(_overlay));
            var roi = new DrawingRectangle(
                Math.Max(0, (int)Math.Floor(Math.Min(_dragStart.X, p.X))),
                Math.Max(0, (int)Math.Floor(Math.Min(_dragStart.Y, p.Y))),
                Math.Max(0, (int)Math.Ceiling(Math.Abs(p.X - _dragStart.X))),
                Math.Max(0, (int)Math.Ceiling(Math.Abs(p.Y - _dragStart.Y))));
            _draft = null;
            if (roi.Width > 1 && roi.Height > 1)
            {
                if (IsTargetPreview) _searchRoi = roi;
                else if (SelectedSample != null) SelectedSample.PatternRoi = roi;
                ClearMatch();
            }
            RefreshSampleInfo();
            DrawCurrentOverlays();
        }

        private WpfPoint ClampToImage(WpfPoint point) => new(
            Math.Max(0, Math.Min(_overlay.Width, point.X)),
            Math.Max(0, Math.Min(_overlay.Height, point.Y)));

        private void ClearMatch()
        {
            _matchBounds = DrawingRectangle.Empty;
            _matchScore = double.NaN;
            _resultText.Text = "尚未執行定位";
        }

        public void SetLocatorEnabled(bool enabled)
        {
            _locatorEnabled = enabled;
            _locateButton.IsEnabled = enabled && !_isLocating;
            if (!enabled)
                _resultText.Text = "定位已關閉；勾選流程中的定位後可執行測試。";
            else if (!_isLocating && _resultText.Text.StartsWith("定位已關閉", StringComparison.Ordinal))
                _resultText.Text = "定位已啟用，請選擇樣板和目標圖片。";
        }

        private async Task RunLocateAsync()
        {
            if (!_locatorEnabled) return;
            if (string.IsNullOrWhiteSpace(_targetPath) || !File.Exists(_targetPath))
            {
                _resultText.Text = "請先選擇目標圖片。";
                return;
            }
            if (!double.TryParse(_thresholdBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double threshold) || threshold < 0 || threshold > 1)
            {
                _resultText.Text = "相似度門檻需為 0 到 1 之間的數字。";
                return;
            }
            var selected = _sampleData.Where(s => s.Enabled && !s.PatternRoi.IsEmpty)
                .Select(s => new SampleSnapshot { Path = s.Path, Roi = s.PatternRoi }).ToList();
            if (selected.Count == 0)
            {
                _resultText.Text = "請至少勾選一張已設定定位框的樣板。";
                return;
            }

            string targetPath = _targetPath!;
            DrawingRectangle searchRoi = _searchRoi;
            _isLocating = true;
            _locateButton.IsEnabled = false;
            _resultText.Text = "正在使用 CPU 進行樣板定位…";
            try
            {
                LocateResult result = await Task.Run(() => Locate(targetPath, searchRoi, selected));
                if (!IsVisible) return;
                _matchBounds = result.Bounds;
                _matchScore = result.Score;
                if (result.Tested == 0)
                {
                    _resultText.Text = $"沒有可比對的樣板。已略過 {result.Skipped} 張：請確認定位框有效，且樣板尺寸不大於目標搜尋區。";
                }
                else
                {
                    string verdict = result.Score >= threshold ? "通過門檻" : "低於門檻";
                    _resultText.Text = $"{verdict}\n最佳樣板：{System.IO.Path.GetFileName(result.Path)}\n" +
                        $"相似度：{result.Score:F4}　設定門檻：{threshold:F2}\n" +
                        $"目標位置：X={result.Bounds.X}, Y={result.Bounds.Y}，框 {result.Bounds.Width}×{result.Bounds.Height}\n" +
                        $"有效比對 {result.Tested} 張，略過 {result.Skipped} 張";
                }
                _previewMode.SelectedIndex = 1;
                RefreshPreview();
            }
            catch (Exception ex)
            {
                if (IsVisible) _resultText.Text = "定位失敗：" + ex.Message;
            }
            finally
            {
                _isLocating = false;
                if (IsVisible) _locateButton.IsEnabled = _locatorEnabled;
            }
        }

        private static LocateResult Locate(string targetPath, DrawingRectangle searchRoi, List<SampleSnapshot> samples)
        {
            var result = new LocateResult();
            using Mat target = LoadColorImage(targetPath);
            if (target.IsEmpty) throw new InvalidOperationException("無法讀取目標圖片。");
            DrawingRectangle safeSearch = searchRoi.IsEmpty
                ? new DrawingRectangle(0, 0, target.Width, target.Height)
                : DrawingRectangle.Intersect(searchRoi, new DrawingRectangle(0, 0, target.Width, target.Height));
            if (safeSearch.Width <= 0 || safeSearch.Height <= 0)
                throw new InvalidOperationException("搜尋區超出目標圖片範圍。");

            using Mat search = new Mat(target, safeSearch);
            foreach (SampleSnapshot sample in samples)
            {
                try
                {
                    using Mat source = LoadColorImage(sample.Path);
                    if (source.IsEmpty || DrawingRectangle.Intersect(sample.Roi, new DrawingRectangle(0, 0, source.Width, source.Height)) != sample.Roi ||
                        sample.Roi.Width > search.Width || sample.Roi.Height > search.Height)
                    {
                        result.Skipped++;
                        continue;
                    }

                    using Mat pattern = new Mat(source, sample.Roi);
                    using Mat scores = new Mat();
                    CvInvoke.MatchTemplate(search, pattern, scores, TemplateMatchingType.CcoeffNormed);
                    double min = 0, max = 0;
                    DrawingPoint minLocation = DrawingPoint.Empty, maxLocation = DrawingPoint.Empty;
                    CvInvoke.MinMaxLoc(scores, ref min, ref max, ref minLocation, ref maxLocation);
                    if (double.IsNaN(max) || double.IsInfinity(max))
                    {
                        result.Skipped++;
                        continue;
                    }
                    result.Tested++;
                    if (max > result.Score)
                    {
                        result.Score = max;
                        result.Path = sample.Path;
                        result.Bounds = new DrawingRectangle(safeSearch.X + maxLocation.X, safeSearch.Y + maxLocation.Y, pattern.Width, pattern.Height);
                    }
                }
                catch
                {
                    result.Skipped++;
                }
            }
            return result;
        }

        private static Mat LoadColorImage(string path)
        {
            // File.ReadAllBytes supports Windows Unicode paths; OpenCV's native
            // imread path handling is not reliable for Chinese folder names.
            var image = new Mat();
            try
            {
                CvInvoke.Imdecode(File.ReadAllBytes(path), ImreadModes.Color, image);
                return image;
            }
            catch
            {
                image.Dispose();
                throw;
            }
        }
    }
}
