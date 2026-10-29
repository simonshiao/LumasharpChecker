using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Emgu.CV;
using Emgu.CV.CvEnum;
using LumasharpChecker.model;
using LumasharpChecker.viewmodel;
using RectD = System.Drawing.Rectangle;
using Shape = System.Windows.Shapes.Rectangle;

namespace LumasharpChecker.view
{
    internal sealed class LightInspectionWindow : Window
    {
        private readonly mainViewModel _vm;
        private string _path;
        private readonly string[] _images;
        private int _imageIndex;
        private readonly LightInspectionSettings _settings;
        private NativeLocationService.Frame? _frame;
        private readonly Canvas _canvas = new Canvas { Background = Brushes.Transparent };
        private readonly Image _image = new Image { Stretch = Stretch.Fill, IsHitTestVisible = false };
        private readonly Shape _located = new Shape { Stroke = Brushes.LimeGreen, StrokeThickness = 3, IsHitTestVisible = false };
        private readonly Shape _roi = new Shape { Stroke = Brushes.DeepSkyBlue, StrokeThickness = 3, IsHitTestVisible = false };
        private readonly TextBlock _status = new TextBlock { Text = "先執行定位，再於綠色框內拖曳設定光源 ROI。", TextWrapping = TextWrapping.Wrap, LineHeight = 23, Margin = new Thickness(0, 16, 0, 0) };
        private readonly TextBox _min = new TextBox { Height = 32, Margin = new Thickness(0, 4, 0, 8) };
        private readonly TextBox _max = new TextBox { Height = 32, Margin = new Thickness(0, 4, 0, 8) };
        private readonly ComboBox _mode = new ComboBox { Height = 32, Margin = new Thickness(0, 4, 0, 8) };
        private readonly TextBox _base = new TextBox { Height = 32, Margin = new Thickness(0, 4, 0, 8) };
        private readonly TextBox _percentLow = new TextBox { Height = 32, Margin = new Thickness(0, 4, 0, 8) };
        private readonly TextBox _percentHigh = new TextBox { Height = 32, Margin = new Thickness(0, 4, 0, 8) };
        private readonly StackPanel _fixedPanel = new StackPanel();
        private readonly StackPanel _percentPanel = new StackPanel();
        private readonly StackPanel _controls = new StackPanel { Margin = new Thickness(20) };
        private Point? _start;
        public LightInspectionWindow(mainViewModel vm, string imagePath)
        {
            _vm = vm; _path = imagePath; _settings = vm.LoadLightSettings();
            _images = vm.TestImageFiles.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            _imageIndex = Array.FindIndex(_images, p => string.Equals(p, imagePath, StringComparison.OrdinalIgnoreCase));
            if (_imageIndex < 0) _imageIndex = 0;
            Title = "光源檢測 · Luma（取代原廠燈泡）"; Width = 1100; Height = 720; MinWidth = 800; MinHeight = 560; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var root = new Grid { Margin = new Thickness(16) }; root.ColumnDefinitions.Add(new ColumnDefinition()); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            var picture = new Grid(); picture.Children.Add(_image); picture.Children.Add(_canvas); _canvas.Children.Add(_located); _canvas.Children.Add(_roi);
            root.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(32,37,45)), Padding = new Thickness(12), Margin = new Thickness(0,0,14,0), Child = new Viewbox { Stretch = Stretch.Uniform, Child = picture } });
            _controls.Children.Add(new TextBlock { Text = "光源檢測", FontSize = 21, FontWeight = FontWeights.SemiBold });
            _controls.Children.Add(new TextBlock { Text = $"{vm.CurrentRecipe?.Name} · 第 {vm.CurrentStationNumber} 站", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,8,0,16) });
            var navigation = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            var previous = new Button { Content = "上一張", Padding = new Thickness(8,5,8,5) };
            var next = new Button { Content = "下一張", Padding = new Thickness(8,5,8,5), Margin = new Thickness(8,0,0,0) };
            var indexText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(9,0,0,0) };
            navigation.Children.Add(previous); navigation.Children.Add(next); navigation.Children.Add(indexText);
            _controls.Children.Add(navigation);
            void SelectImage(int offset)
            {
                if (_images.Length == 0) return;
                _imageIndex = (_imageIndex + offset + _images.Length) % _images.Length;
                _path = _images[_imageIndex];
                _frame = null; _roi.Visibility = _located.Visibility = Visibility.Collapsed;
                SetImage(_path);
                indexText.Text = (_imageIndex + 1) + " / " + _images.Length;
                _status.Text = "已切換圖檔，請定位並設定光源 ROI。";
            }
            previous.Click += (_,__) => SelectImage(-1);
            next.Click += (_,__) => SelectImage(1);
            indexText.Text = _images.Length == 0 ? "0 / 0" : (_imageIndex + 1) + " / " + _images.Length;
            // 光源點檢優先使用 Luma 已校準的定位／亮度引擎，
            // 不把原廠 CheckAuth 當成離線光源檢測的必要前置條件。
            AddButton("以本站參數重新檢測", RunLocalLightCheckAsync);
            AddButton("原廠定位（需授權）", async () => {
                _frame = null; _roi.Visibility = _located.Visibility = Visibility.Collapsed; _status.Text = "原廠定位中…";
                var frame = await vm.LocateLightFrameAsync(_path); _frame = frame; SetImage(frame.ImagePath); Show(_located, frame.Bounds);
                _status.Text = "定位成功。綠框：定位區；藍框：光源 ROI。";
                if (_settings.HasRoi) Show(_roi, _settings.GetRoi(frame.Bounds));
            });
            _controls.Children.Add(new TextBlock { Text = "亮度規格模式", Margin = new Thickness(0,16,0,0) });
            _mode.Items.Add("百分比偏差"); _mode.Items.Add("固定上下限");
            _mode.SelectedItem = _settings.LimitMode;
            _controls.Children.Add(_mode);
            _fixedPanel.Children.Add(new TextBlock { Text = "固定下限（0～255）" }); _fixedPanel.Children.Add(_min);
            _fixedPanel.Children.Add(new TextBlock { Text = "固定上限（0～255）" }); _fixedPanel.Children.Add(_max);
            _percentPanel.Children.Add(new TextBlock { Text = "基準亮度（0～255）" }); _percentPanel.Children.Add(_base);
            _percentPanel.Children.Add(new TextBlock { Text = "下限偏差 %（基準 -%）" }); _percentPanel.Children.Add(_percentLow);
            _percentPanel.Children.Add(new TextBlock { Text = "上限偏差 %（基準 +%）" }); _percentPanel.Children.Add(_percentHigh);
            _controls.Children.Add(_fixedPanel); _controls.Children.Add(_percentPanel);
            _min.Text = _settings.Minimum.ToString(CultureInfo.CurrentCulture); _max.Text = _settings.Maximum.ToString(CultureInfo.CurrentCulture);
            _base.Text = _settings.BaseValue.ToString(CultureInfo.CurrentCulture);
            _percentLow.Text = _settings.PercentLower.ToString(CultureInfo.CurrentCulture);
            _percentHigh.Text = _settings.PercentUpper.ToString(CultureInfo.CurrentCulture);
            _mode.SelectionChanged += (_, __) => UpdateModeVisibility();
            UpdateModeVisibility();
            AddButton("測試自訂 ROI", () => { Measure(); return System.Threading.Tasks.Task.CompletedTask; });
            AddButton("儲存本站光源設定", () => { ReadLimits(); vm.SaveLightSettings(_settings); _status.Text = "已儲存本站光源 ROI 與上下限。"; return System.Threading.Tasks.Task.CompletedTask; });
            _controls.Children.Add(_status);
            _controls.Children.Add(new TextBlock { Text = "本站參數檢測直接使用 Luma 現有樣板／ROI／規格，不需要原廠授權；原廠定位與自訂 ROI 則另外測試。亮度使用 HSV V 平均值。", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0,18,0,0) });
            var side = new ScrollViewer { Content = _controls, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetColumn(side,1); root.Children.Add(side); Content = root;
            _canvas.MouseLeftButtonDown += (_,e) => { if (_frame == null) return; var p=e.GetPosition(_canvas); if (!_frame.Bounds.Contains((int)p.X,(int)p.Y)) return; _start=p; _canvas.CaptureMouse(); };
            _canvas.MouseLeftButtonUp += (_,e) => {
                if (_start == null || _frame == null) return; Point a=_start.Value,b=e.GetPosition(_canvas); _start=null; _canvas.ReleaseMouseCapture();
                var box=_frame.Bounds; int l=(int)Math.Max(box.Left,Math.Min(a.X,b.X)), t=(int)Math.Max(box.Top,Math.Min(a.Y,b.Y)); int r=(int)Math.Min(box.Right,Math.Max(a.X,b.X)), d=(int)Math.Min(box.Bottom,Math.Max(a.Y,b.Y));
                if(r-l<2 || d-t<2) return; _settings.Left=l-box.Left; _settings.Top=t-box.Top; _settings.Right=box.Right-r; _settings.Bottom=box.Bottom-d; _settings.HasRoi=true; Show(_roi,new RectD(l,t,r-l,d-t)); _status.Text="光源 ROI 已設定，尚未儲存。";
            };
            SetImage(imagePath);
            Loaded += async (_, __) =>
            {
                try { await RunLocalLightCheckAsync(); }
                catch (Exception error) { _status.Text = "亮度檢測失敗：" + error.GetBaseException().Message; }
            };
        }

        private async System.Threading.Tasks.Task RunLocalLightCheckAsync()
        {
            _frame = null;
            _located.Visibility = _roi.Visibility = Visibility.Collapsed;
            _status.Text = "本站亮度檢測中…";
            if (!_settings.HasRoi)
            {
                _status.Text = "尚未設定光源 ROI。請先定位、拖曳光源 ROI，設定亮度規格後儲存。";
                return;
            }
            if (_frame == null)
            {
                _frame = await _vm.LocateLightFrameAsync(_path);
                SetImage(_frame.ImagePath);
            }
            ReadLimits();
            Measure();
        }
        private void AddButton(string text, Func<System.Threading.Tasks.Task> action)
        {
            var b=new Button { Content=text, Padding=new Thickness(12,8,12,8), Margin=new Thickness(0,6,0,0) }; _controls.Children.Add(b);
            b.Click += async (_,__) => { _controls.IsEnabled=false; try { await action(); } catch(Exception ex) { _status.Text=ex.GetBaseException().Message; } finally { _controls.IsEnabled=true; } };
        }
        private void SetImage(string path)
        {
            var b=new BitmapImage(); b.BeginInit(); b.UriSource=new Uri(path,UriKind.Absolute); b.CacheOption=BitmapCacheOption.OnLoad; b.EndInit(); b.Freeze(); _image.Source=b; _canvas.Width=_image.Width=b.PixelWidth; _canvas.Height=_image.Height=b.PixelHeight;
        }
        private static void Show(Shape shape,RectD r) { Canvas.SetLeft(shape,r.X); Canvas.SetTop(shape,r.Y); shape.Width=r.Width; shape.Height=r.Height; shape.Visibility=Visibility.Visible; }
        private void UpdateModeVisibility()
        {
            bool percent = (string)_mode.SelectedItem == "百分比偏差";
            _fixedPanel.Visibility = percent ? Visibility.Collapsed : Visibility.Visible;
            _percentPanel.Visibility = percent ? Visibility.Visible : Visibility.Collapsed;
        }
        private void ReadLimits()
        {
            if (!double.TryParse(_min.Text, out var min) || !double.TryParse(_max.Text, out var max) ||
                !double.TryParse(_base.Text, out var baseValue) ||
                !double.TryParse(_percentLow.Text, out var low) || !double.TryParse(_percentHigh.Text, out var high))
                throw new InvalidOperationException("請輸入有效亮度或百分比數值。");
            _settings.LimitMode = (string)_mode.SelectedItem;
            _settings.Minimum = min; _settings.Maximum = max;
            _settings.BaseValue = baseValue; _settings.PercentLower = low; _settings.PercentUpper = high;
            _settings.EffectiveLimits();
        }
        private void Measure()
        {
            if(_frame==null)throw new InvalidOperationException("請先定位目前圖片。"); ReadLimits(); var r=_settings.GetRoi(_frame.Bounds);
            using var image=new Mat(); CvInvoke.Imdecode(File.ReadAllBytes(_frame.ImagePath),ImreadModes.Color,image);
            if(r.Left<0||r.Top<0||r.Right>image.Width||r.Bottom>image.Height)throw new InvalidOperationException("ROI 超出定位後影像範圍。");
            using var crop=new Mat(image,r); using var hsv=new Mat(); CvInvoke.CvtColor(crop,hsv,ColorConversion.Bgr2Hsv); double value=CvInvoke.Mean(hsv).V2;
            var limits = _settings.EffectiveLimits();
            _status.Text=$"亮度 {(value>=limits.Min&&value<=limits.Max?"OK":"NG")}\n平均 {value:F2}\n範圍 {limits.Min:F2} ～ {limits.Max:F2}\nROI {r.X}, {r.Y}, {r.Width} × {r.Height}";
        }
    }
}
