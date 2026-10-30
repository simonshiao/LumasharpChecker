using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
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
using Orientation = System.Windows.Controls.Orientation;
using RoiPart = LumasharpChecker.model.RoiEditorGeometry.Part;

namespace LumasharpChecker.view;

internal sealed class LightInspectionWindow : Window
{
    private static Brush Ink => SystemParameters.HighContrast ? SystemColors.WindowTextBrush : ColorBrush("#15334B");
    private static Brush Muted => SystemParameters.HighContrast ? SystemColors.WindowTextBrush : ColorBrush("#526779");
    private static Brush Accent => SystemParameters.HighContrast ? SystemColors.HighlightBrush : ColorBrush("#0079C8");
    private static Brush Line => SystemParameters.HighContrast ? SystemColors.WindowTextBrush : ColorBrush("#CCDCE8");
    private readonly mainViewModel _vm;
    private readonly string[] _images;
    private readonly string _recipeId;
    private readonly int _station;
    private string _path, _savedState = "";
    private int _imageIndex;
    private LightInspectionSettings _settings;
    private NativeLocationService.Frame? _frame;
    private bool _busy, _cancelBatch, _drawing, _loading = true;
    private Point? _start;
    private RectD _dragOriginal, _dragPreview;
    private RoiPart _dragPart;
    private readonly Canvas _canvas = new() { Background = Brushes.Transparent, Focusable = true };
    private readonly Viewbox _imageView = new() { Stretch = Stretch.Uniform };
    private readonly Shape[] _handles = RoiEditorGeometry.Handles.Select(_ => new Shape { Fill = Brushes.White, Stroke = Brushes.DeepSkyBlue, IsHitTestVisible = false, Visibility = Visibility.Collapsed }).ToArray();
    private readonly Image _image = new() { Stretch = Stretch.Fill, IsHitTestVisible = false };
    private readonly Shape _located = new() { Stroke = Brushes.LimeGreen, StrokeThickness = 2, IsHitTestVisible = false };
    private readonly Shape _roi = new() { Stroke = Brushes.DeepSkyBlue, StrokeThickness = 2, IsHitTestVisible = false };
    private readonly TextBlock _status = Text("正在準備定位影像…"), _dirty = Text(""), _file = Text(""), _index = Text("");
    private readonly TextBlock _roiInfo = Text("尚未設定光源 ROI"), _range = Text(""), _average = Text("尚未計算樣本基準");
    private readonly TextBlock _value = Text("—", 30), _verdict = Text("尚未量測", 14), _resultDetail = Text("設定 ROI 後即可量測亮度。");
    private readonly TextBox _min = Field(), _max = Field(), _base = Field(), _low = Field(), _high = Field();
    private readonly TextBox[] _margins = { Field(), Field(), Field(), Field() };
    private readonly ComboBox _mode = new() { MinHeight = 32, Margin = new Thickness(0, 4, 0, 8) };
    private readonly StackPanel _fixed = new(), _percent = new();
    private readonly Grid _body = new();
    private readonly StackPanel _actions = new() { Orientation = Orientation.Horizontal };
    private readonly ProgressBar _progress = new() { Height = 3, Visibility = Visibility.Collapsed };
    private readonly Button _cancel = new() { Content = "停止計算", Visibility = Visibility.Collapsed };
    private readonly Button _draw, _save;

    public LightInspectionWindow(mainViewModel vm, string imagePath)
    {
        _vm = vm; _path = imagePath; _settings = vm.LoadLightSettings(); _recipeId = vm.CurrentRecipe!.Id; _station = vm.CurrentStationNumber;
        _images = vm.TestImageFiles.Where(File.Exists).Concat(new[] { imagePath }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        _imageIndex = Math.Max(0, Array.FindIndex(_images, p => string.Equals(p, imagePath, StringComparison.OrdinalIgnoreCase)));
        Title = "光源檢測 · LumasharpChecker";
        Width = Math.Min(1220, SystemParameters.WorkArea.Width); Height = Math.Min(820, SystemParameters.WorkArea.Height);
        MinWidth = 980; MinHeight = 650; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = SystemParameters.HighContrast ? SystemColors.WindowBrush : ColorBrush("#F3F7FB");
        Foreground = Ink; FontFamily = SystemFonts.MessageFontFamily; FontSize = 13; UseLayoutRounding = true;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new DockPanel { Margin = new Thickness(24, 16, 24, 16) };
        _dirty.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(_dirty, Dock.Right); header.Children.Add(_dirty);
        var heading = new StackPanel(); heading.Children.Add(Text("光源檢測", 23, true)); heading.Children.Add(Text($"{vm.CurrentRecipe.Name}  /  第 {_station} 站  ·  定位 → 選向 → 光源 ROI")); header.Children.Add(heading); root.Children.Add(header);
        _body.Margin = new Thickness(16, 0, 16, 12); _body.ColumnDefinitions.Add(new ColumnDefinition()); _body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(354) });
        var preview = new Grid { Margin = new Thickness(0, 0, 14, 0) };
        preview.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); preview.RowDefinitions.Add(new RowDefinition()); preview.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var toolbar = new DockPanel { Margin = new Thickness(0, 0, 0, 10), LastChildFill = false };
        toolbar.Children.Add(Button("上一張", () => SelectImageAsync(-1))); _index.Margin = new Thickness(8, 0, 8, 0); _index.VerticalAlignment = VerticalAlignment.Center; toolbar.Children.Add(_index);
        toolbar.Children.Add(Button("下一張", () => SelectImageAsync(1))); var retry = Button("重新定位", LoadFrameAsync); DockPanel.SetDock(retry, Dock.Right); toolbar.Children.Add(retry); preview.Children.Add(toolbar);
        var picture = new Grid(); picture.Children.Add(_image); picture.Children.Add(_canvas); _canvas.Children.Add(_located); _canvas.Children.Add(_roi);
        foreach (var handle in _handles) _canvas.Children.Add(handle);
        _imageView.Child = picture;
        _imageView.SizeChanged += (_, _) => RefreshRoiVisual();
        AutomationProperties.SetName(_canvas, "光源 ROI 編輯區：拖曳框內移動，拖曳控制點縮放；方向鍵微調，Shift 加速，Esc 取消");
        var imageBorder = new Border { Background = ColorBrush("#202831"), CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Child = _imageView };
        Grid.SetRow(imageBorder, 1); preview.Children.Add(imageBorder);
        var caption = new StackPanel { Margin = new Thickness(2, 10, 0, 0) }; _file.TextTrimming = TextTrimming.CharacterEllipsis; _file.TextWrapping = TextWrapping.NoWrap;
        caption.Children.Add(_file); caption.Children.Add(Text("藍框內拖曳移動 · 拖四邊／角點縮放 · 方向鍵微調（Shift ×10）", 12)); Grid.SetRow(caption, 2); preview.Children.Add(caption); _body.Children.Add(preview);
        var side = new Grid(); side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); side.RowDefinitions.Add(new RowDefinition());
        var result = new StackPanel(); var resultTop = new DockPanel(); _verdict.Foreground = Accent; _verdict.FontWeight = FontWeights.SemiBold;
        DockPanel.SetDock(_verdict, Dock.Right); resultTop.Children.Add(_verdict); resultTop.Children.Add(Text("本張亮度 · HSV V", 12)); result.Children.Add(resultTop); result.Children.Add(_value); result.Children.Add(_resultDetail);
        side.Children.Add(Card(result, 16, new Thickness(0, 0, 0, 10)));
        var settings = new StackPanel(); settings.Children.Add(Text("1  設定量測區域", 15, true)); settings.Children.Add(Text("綠框為定位範圍；藍色光源框可直接拖曳調整。", 12)); _roiInfo.Margin = new Thickness(0, 6, 0, 8); settings.Children.Add(_roiInfo);
        var roiActions = new StackPanel { Orientation = Orientation.Horizontal }; _draw = Button("重畫 ROI", () => { RequireFrame(); SetDrawing(!_drawing); return Task.CompletedTask; }); roiActions.Children.Add(_draw);
        roiActions.Children.Add(Button("使用整個定位區", () => { RequireFrame(); ApplyRoi(_frame!.Bounds); return Task.CompletedTask; })); settings.Children.Add(roiActions);
        settings.Children.Add(Text("內縮邊距（px）", 12)); settings.Children.Add(Fields(new[] { "左", "上", "右", "下" }, _margins)); settings.Children.Add(Button("套用邊距", ApplyMarginsAsync));
        settings.Children.Add(Separator()); settings.Children.Add(Text("2  建立基準亮度", 15, true)); settings.Children.Add(Text("使用目前載入的圖片；請先確認都是基準樣本。", 12));
        var baseActions = new StackPanel { Orientation = Orientation.Horizontal }; baseActions.Children.Add(Button("使用本張", () => { RequireFrame(); ValidateMargins(); SetBaseline(MeasureFrame(_frame!)); return Task.CompletedTask; }));
        baseActions.Children.Add(Button("計算全部樣本", CalculateAllSamplesAsync)); settings.Children.Add(baseActions); settings.Children.Add(Labeled("基準亮度（0～255）", _base)); _average.Margin = new Thickness(0, 6, 0, 0); settings.Children.Add(_average);
        settings.Children.Add(Separator()); settings.Children.Add(Text("3  設定允收範圍", 15, true));
        _mode.Items.Add("百分比偏差"); _mode.Items.Add("固定上下限"); _mode.SelectedItem = _settings.LimitMode; settings.Children.Add(_mode);
        _percent.Children.Add(Fields(new[] { "下限偏差（%）", "上限偏差（%）" }, new[] { _low, _high }));
        _fixed.Children.Add(Fields(new[] { "亮度下限", "亮度上限" }, new[] { _min, _max })); settings.Children.Add(_percent); settings.Children.Add(_fixed);
        _range.Margin = new Thickness(0, 8, 0, 0); _range.FontWeight = FontWeights.SemiBold; settings.Children.Add(_range);
        var scroll = new ScrollViewer { Content = settings, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(16) };
        var settingsCard = Card(scroll, 0, new Thickness(0)); Grid.SetRow(settingsCard, 1); side.Children.Add(settingsCard); Grid.SetColumn(side, 1); _body.Children.Add(side); Grid.SetRow(_body, 1); root.Children.Add(_body);
        var footer = new Grid { Margin = new Thickness(24, 0, 24, 16) }; footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); Grid.SetColumnSpan(_progress, 2); footer.Children.Add(_progress);
        _status.Margin = new Thickness(0, 10, 16, 0); _status.VerticalAlignment = VerticalAlignment.Center; Grid.SetRow(_status, 1); footer.Children.Add(_status);
        _cancel.Padding = new Thickness(12, 8, 12, 8); _cancel.Click += (_, _) => { _cancelBatch = true; _cancel.IsEnabled = false; Status("正在停止，等待目前圖片完成…"); };
        var footerActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) }; footerActions.Children.Add(_cancel);
        _actions.Children.Add(Button("測試本張", async () => { if (_frame == null) await LoadFrameAsync(); else Measure(); }));
        _save = Button("儲存本站設定", () => { Save(); return Task.CompletedTask; }); _save.Background = Accent; _save.Foreground = SystemColors.HighlightTextBrush; _save.FontWeight = FontWeights.SemiBold; _actions.Children.Add(_save);
        footerActions.Children.Add(_actions); Grid.SetColumn(footerActions, 1); Grid.SetRow(footerActions, 1); footer.Children.Add(footerActions); Grid.SetRow(footer, 2); root.Children.Add(footer); Content = root;
        _min.Text = Number(_settings.Minimum); _max.Text = Number(_settings.Maximum); _base.Text = Number(_settings.BaseValue); _low.Text = Number(_settings.PercentLower); _high.Text = Number(_settings.PercentUpper); FillMargins();
        foreach (var field in new[] { _min, _max, _base, _low, _high }.Concat(_margins)) field.TextChanged += (_, _) => { if (!_loading) { InvalidateResult(); UpdateForm(); } };
        _mode.SelectionChanged += (_, _) => { if (!_loading) { InvalidateResult(); UpdateForm(); } };
        WireCanvas(); _savedState = FormState(); _loading = false; UpdateForm(); UpdateImageLabel(); SetImage(_path);
        Loaded += async (_, _) => await RunAsync(LoadFrameAsync); Closing += OnClosing;
    }

    private static Brush ColorBrush(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;
    private static TextBlock Text(string text, double size = 13, bool bold = false) => new() { Text = text, FontSize = size, Foreground = bold ? Ink : Muted, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
    private static TextBox Field() => new() { MinHeight = 32, Padding = new Thickness(8, 4, 8, 4), VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 0, 4) };
    private static Border Card(UIElement content, double padding, Thickness margin) => new() { Child = content, Padding = new Thickness(padding), Margin = margin, Background = SystemColors.WindowBrush, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
    private static Border Separator() => new() { Height = 1, Background = Line, Margin = new Thickness(0, 14, 0, 14) };
    private static StackPanel Labeled(string label, Control control)
    {
        AutomationProperties.SetName(control, label); var panel = new StackPanel(); panel.Children.Add(new Label { Content = label, Target = control, Padding = new Thickness(0), Foreground = Muted, FontSize = 12 }); panel.Children.Add(control); return panel;
    }
    private static Grid Fields(string[] labels, TextBox[] fields)
    {
        var grid = new Grid(); for (int i = 0; i < fields.Length; i++) { grid.ColumnDefinitions.Add(new ColumnDefinition()); var group = Labeled(labels[i], fields[i]); group.Margin = new Thickness(0, 0, i == fields.Length - 1 ? 0 : 8, 0); Grid.SetColumn(group, i); grid.Children.Add(group); } return grid;
    }
    private Button Button(string text, Func<Task> action)
    {
        var button = new Button { Content = text, Padding = new Thickness(12, 7, 12, 7), MinHeight = 34, Margin = new Thickness(0, 4, 8, 4) }; button.Click += async (_, _) => await RunAsync(action); return button;
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return; _busy = true; _body.IsEnabled = _actions.IsEnabled = false; _progress.Visibility = Visibility.Visible; _progress.IsIndeterminate = true;
        try { CheckContext(); await action(); }
        catch (Exception ex) { _status.Text = ex.GetBaseException().Message; _status.Foreground = SystemParameters.HighContrast ? Ink : Brushes.Firebrick; }
        finally { _busy = false; _body.IsEnabled = _actions.IsEnabled = true; _cancel.Visibility = _progress.Visibility = Visibility.Collapsed; UpdateForm(); }
    }
    private void CheckContext() { if (_vm.CurrentRecipe?.Id != _recipeId || _vm.CurrentStationNumber != _station) throw new InvalidOperationException("配方或站別已切換，請重新開啟光源設定。"); }
    private void Status(string text) { _status.Text = text; _status.Foreground = Ink; }
    private static string Number(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);
    private string FormState() => string.Join("|", new[] { _settings.HasRoi.ToString(), _settings.Left.ToString(), _settings.Top.ToString(), _settings.Right.ToString(), _settings.Bottom.ToString(), _mode.SelectedItem?.ToString(), _min.Text, _max.Text, _base.Text, _low.Text, _high.Text }.Concat(_margins.Select(m => m.Text)));
    private void FillMargins() { int[] values = { _settings.Left, _settings.Top, _settings.Right, _settings.Bottom }; for (int i = 0; i < 4; i++) _margins[i].Text = values[i].ToString(); }
    private static double Read(TextBox field, string label, double maximum)
    {
        bool valid = double.TryParse(field.Text, out double value) && !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= maximum;
        field.BorderBrush = valid ? Line : Brushes.Firebrick; field.ToolTip = valid ? null : $"{label}須為 0～{maximum} 的數值。";
        if (!valid) throw new InvalidOperationException($"{label}須為 0～{maximum} 的數值。"); return value;
    }
    private int[] ReadMargins() => _margins.Select(m => { double value = Read(m, "內縮邊距", int.MaxValue); if (value != Math.Truncate(value)) throw new InvalidOperationException("ROI 邊距請輸入整數像素。"); return (int)value; }).ToArray();
    private void ValidateMargins()
    {
        if (!_settings.HasRoi) throw new InvalidOperationException("請先設定光源 ROI。");
        if (!ReadMargins().SequenceEqual(new[] { _settings.Left, _settings.Top, _settings.Right, _settings.Bottom })) throw new InvalidOperationException("邊距已修改，請先按「套用邊距」確認 ROI。");
    }
    private LightInspectionSettings ReadForm()
    {
        // Validate a copy: incomplete input must not change the active measurement settings.
        var c = new LightInspectionSettings { Left = _settings.Left, Top = _settings.Top, Right = _settings.Right, Bottom = _settings.Bottom, HasRoi = _settings.HasRoi,
            Minimum = _settings.Minimum, Maximum = _settings.Maximum, BaseValue = _settings.BaseValue, PercentLower = _settings.PercentLower, PercentUpper = _settings.PercentUpper, LimitMode = (string)_mode.SelectedItem };
        c.BaseValue = Read(_base, "基準亮度", 255);
        if (c.LimitMode == "百分比偏差") { c.PercentLower = Read(_low, "下限偏差", 100); c.PercentUpper = Read(_high, "上限偏差", 100); }
        else { c.Minimum = Read(_min, "亮度下限", 255); c.Maximum = Read(_max, "亮度上限", 255); }
        c.EffectiveLimits(); return c;
    }
    private void UpdateForm()
    {
        bool percent = (string)_mode.SelectedItem == "百分比偏差"; _percent.Visibility = percent ? Visibility.Visible : Visibility.Collapsed; _fixed.Visibility = percent ? Visibility.Collapsed : Visibility.Visible;
        _dirty.Text = FormState() != _savedState ? "有未儲存的變更" : _settings.HasRoi ? "已儲存至本站 Recipe" : "尚未設定 ROI";
        try { var limits = ReadForm().EffectiveLimits(); _range.Text = $"實際允收  {limits.Min:F2} ～ {limits.Max:F2}"; _range.Foreground = Accent; }
        catch (Exception ex) { _range.Text = ex.Message; _range.Foreground = SystemParameters.HighContrast ? Ink : Brushes.Firebrick; }
        _save.IsEnabled = _settings.HasRoi;
    }
    private void InvalidateResult() { _value.Text = "—"; _verdict.Text = "待測試"; _verdict.Foreground = Muted; _resultDetail.Text = "參數已變更，請測試本張確認結果。"; }
    private void UpdateImageLabel() { _index.Text = $"{_imageIndex + 1} / {_images.Length}"; _file.Text = Path.GetFileName(_path); _file.ToolTip = _path; }
    private async Task SelectImageAsync(int offset) { _imageIndex = (_imageIndex + offset + _images.Length) % _images.Length; _path = _images[_imageIndex]; UpdateImageLabel(); await LoadFrameAsync(); }
    private async Task LoadFrameAsync()
    {
        _start = null; _frame = null; _canvas.ReleaseMouseCapture(); _located.Visibility = _roi.Visibility = Visibility.Collapsed; HideHandles(); SetDrawing(false); InvalidateResult(); SetImage(_path); Status("正在執行本站定位與選向…");
        _frame = await _vm.LocateLightFrameAsync(_path); CheckContext(); SetImage(_frame.ImagePath); Show(_located, _frame.Bounds);
        if (!_settings.HasRoi) { SetDrawing(true); Status("定位與選向完成。請在綠框內拖曳光源 ROI，或使用整個定位區。"); return; }
        var roi = _settings.GetRoi(_frame.Bounds); ShowRoi(roi);
        try { Measure(); } catch (Exception ex) { Status("定位與選向完成；" + ex.Message); }
    }
    private void RequireFrame() { if (_frame == null) throw new InvalidOperationException("請先完成目前圖片的定位與選向；可按「重新定位」重試。"); }
    private void SetDrawing(bool enabled) { _drawing = enabled; _canvas.Cursor = enabled ? Cursors.Cross : Cursors.Arrow; _draw.Content = enabled ? "取消畫框" : "重畫 ROI"; if (enabled) Status("畫框模式：在綠色定位範圍內按住滑鼠拖曳，放開後套用 ROI。"); }
    private void WireCanvas()
    {
        _canvas.MouseLeftButtonDown += (_, e) =>
        {
            if (_busy || _frame == null) return; var p = e.GetPosition(_canvas);
            var part = HitRoi(p); if (part == RoiPart.None) return;
            _canvas.Focus(); _start = p; _dragPart = part;
            _dragOriginal = _settings.HasRoi ? _settings.GetRoi(_frame.Bounds) : RectD.Empty;
            _dragPreview = _dragOriginal; _canvas.CaptureMouse(); _canvas.Cursor = CursorFor(part); e.Handled = true;
        };
        _canvas.MouseMove += (_, e) =>
        {
            var p = e.GetPosition(_canvas);
            if (_start != null && _frame != null) { _dragPreview = DragBox(p); ShowRoi(_dragPreview); }
            else _canvas.Cursor = CursorFor(HitRoi(p));
        };
        _canvas.MouseLeftButtonUp += (_, e) =>
        {
            if (_start == null || _frame == null) return; var roi = DragBox(e.GetPosition(_canvas));
            bool changed = !_settings.HasRoi || roi != _dragOriginal; _start = null; _canvas.ReleaseMouseCapture();
            if (roi.Width < 2 || roi.Height < 2) { RefreshRoiVisual(); Status("ROI 太小，請拖曳至少 2 × 2 像素的區域。"); return; }
            if (changed) ApplyRoi(roi); else RefreshRoiVisual(); e.Handled = true;
        };
        _canvas.LostMouseCapture += (_, _) => { if (_start != null) { _start = null; RefreshRoiVisual(); Status("已取消這次 ROI 調整。"); } };
        _canvas.PreviewKeyDown += (_, e) =>
        {
            if (_busy || _frame == null) return;
            if (e.Key == Key.Escape) { _start = null; _canvas.ReleaseMouseCapture(); SetDrawing(false); RefreshRoiVisual(); Status("已取消這次 ROI 調整。"); e.Handled = true; return; }
            if (_start != null || !_settings.HasRoi) return;
            int step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 10 : 1;
            int dx = e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0, dy = e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0;
            if (dx == 0 && dy == 0) return;
            var original = _settings.GetRoi(_frame.Bounds); var roi = RoiEditorGeometry.Transform(original, _frame.Bounds, RoiPart.Move, dx, dy);
            if (roi != original) ApplyRoi(roi); e.Handled = true;
        };
    }
    private RectD DragBox(Point end)
    {
        var a = _start!.Value;
        return _dragPart == RoiPart.Draw ? RoiEditorGeometry.Draw(_frame!.Bounds, a.X, a.Y, end.X, end.Y)
            : RoiEditorGeometry.Transform(_dragOriginal, _frame!.Bounds, _dragPart, end.X - a.X, end.Y - a.Y);
    }
    private RoiPart HitRoi(Point p)
    {
        if (_frame == null) return RoiPart.None;
        if (_drawing || !_settings.HasRoi) return _frame.Bounds.Contains((int)p.X, (int)p.Y) ? RoiPart.Draw : RoiPart.None;
        try { return RoiEditorGeometry.HitTest(_settings.GetRoi(_frame.Bounds), p.X, p.Y, 7 / ImageScale()); }
        catch (InvalidOperationException) { return RoiPart.None; }
    }
    private static Cursor CursorFor(RoiPart part) => part switch
    {
        RoiPart.Move => Cursors.SizeAll, RoiPart.Draw => Cursors.Cross,
        RoiPart.Left or RoiPart.Right => Cursors.SizeWE, RoiPart.Top or RoiPart.Bottom => Cursors.SizeNS,
        RoiPart.Left | RoiPart.Top => Cursors.SizeNWSE, RoiPart.Right | RoiPart.Bottom => Cursors.SizeNWSE,
        RoiPart.Right | RoiPart.Top => Cursors.SizeNESW, RoiPart.Left | RoiPart.Bottom => Cursors.SizeNESW,
        _ => Cursors.Arrow
    };
    private double ImageScale()
    {
        var transform = _canvas.TransformToAncestor(_imageView); var a = transform.Transform(new Point(0, 0)); var b = transform.Transform(new Point(1, 0));
        double scale = Math.Abs(b.X - a.X); return scale > 0.0001 ? scale : 1;
    }
    private void HideHandles() { foreach (var handle in _handles) handle.Visibility = Visibility.Collapsed; }
    private void RefreshRoiVisual()
    {
        if (_frame == null) return;
        if (_start != null) { ShowRoi(_dragPreview); return; }
        if (!_settings.HasRoi) { _roi.Visibility = Visibility.Collapsed; HideHandles(); return; }
        try { ShowRoi(_settings.GetRoi(_frame.Bounds)); }
        catch (InvalidOperationException) { _roi.Visibility = Visibility.Collapsed; HideHandles(); }
    }
    private void ShowRoi(RectD roi)
    {
        Show(_roi, roi); UpdateRoiInfo(roi); double scale = ImageScale(); _roi.StrokeThickness = 2 / scale; _located.StrokeThickness = 1.5 / scale;
        for (int i = 0; i < _handles.Length; i++)
        {
            var p = RoiEditorGeometry.Anchor(roi, RoiEditorGeometry.Handles[i]); var h = _handles[i]; h.Width = h.Height = 8 / scale; h.StrokeThickness = 1 / scale;
            Canvas.SetLeft(h, p.X - h.Width / 2); Canvas.SetTop(h, p.Y - h.Height / 2); h.Visibility = Visibility.Visible;
        }
    }
    private Task ApplyMarginsAsync()
    {
        RequireFrame(); var v = ReadMargins(); var b = _frame!.Bounds; long w = (long)b.Width - v[0] - v[2], h = (long)b.Height - v[1] - v[3];
        if (w < 2 || h < 2) throw new InvalidOperationException("內縮邊距超過定位範圍，請保留至少 2 × 2 像素的 ROI。"); ApplyRoi(new RectD(b.X + v[0], b.Y + v[1], (int)w, (int)h)); return Task.CompletedTask;
    }
    private void ApplyRoi(RectD roi)
    {
        var box = _frame!.Bounds; _settings.Left = roi.Left - box.Left; _settings.Top = roi.Top - box.Top; _settings.Right = box.Right - roi.Right; _settings.Bottom = box.Bottom - roi.Bottom; _settings.HasRoi = true;
        FillMargins(); ShowRoi(roi); SetDrawing(false); InvalidateResult(); UpdateForm(); _average.Text = "ROI 已變更，樣本基準請重新確認。"; Status("ROI 已套用，尚未儲存。框內可拖曳移動，四邊／角點可調整大小。");
    }
    private void UpdateRoiInfo(RectD roi) => _roiInfo.Text = $"{roi.Width} × {roi.Height} px  ·  X {roi.X} / Y {roi.Y}";
    private void SetImage(string path) { var b = new BitmapImage(); b.BeginInit(); b.UriSource = new Uri(path, UriKind.Absolute); b.CacheOption = BitmapCacheOption.OnLoad; b.EndInit(); b.Freeze(); _image.Source = b; _canvas.Width = _image.Width = b.PixelWidth; _canvas.Height = _image.Height = b.PixelHeight; }
    private static void Show(Shape shape, RectD r) { Canvas.SetLeft(shape, r.X); Canvas.SetTop(shape, r.Y); shape.Width = r.Width; shape.Height = r.Height; shape.Visibility = Visibility.Visible; }
    private void Measure()
    {
        RequireFrame(); ValidateMargins(); var candidate = ReadForm(); var limits = candidate.EffectiveLimits(); var timer = System.Diagnostics.Stopwatch.StartNew(); double value = MeasureFrame(_frame!); timer.Stop(); _settings = candidate;
        bool pass = value >= limits.Min && value <= limits.Max; _value.Text = value.ToString("F2"); _value.Foreground = Ink; _verdict.Text = pass ? "OK · 範圍內" : "NG · 超出範圍"; _verdict.Foreground = SystemParameters.HighContrast ? Ink : ColorBrush(pass ? "#167147" : "#B22F36");
        _resultDetail.Text = $"允收 {limits.Min:F2} ～ {limits.Max:F2}  ·  量測 {timer.Elapsed.TotalMilliseconds:F1} ms"; Status("光源量測完成。主介面檢測會依完整流程加入選面並更新判定。");
    }
    private double MeasureFrame(NativeLocationService.Frame frame)
    {
        var roi = _settings.GetRoi(frame.Bounds); using var image = new Mat(); CvInvoke.Imdecode(File.ReadAllBytes(frame.ImagePath), ImreadModes.Color, image);
        if (roi.X < 0 || roi.Y < 0 || roi.Right > image.Width || roi.Bottom > image.Height) throw new InvalidOperationException("光源 ROI 超出定位後影像範圍。");
        using var crop = new Mat(image, roi); using var hsv = new Mat(); CvInvoke.CvtColor(crop, hsv, ColorConversion.Bgr2Hsv); return CvInvoke.Mean(hsv).V2;
    }
    private void SetBaseline(double value) { _base.Text = Number(value); _average.Text = $"基準亮度 {value:F2}  ·  使用本張圖片"; Status("基準亮度已帶入。請確認允收範圍並儲存本站設定。"); UpdateForm(); }
    private async Task CalculateAllSamplesAsync()
    {
        RequireFrame(); ValidateMargins(); _cancelBatch = false; _cancel.IsEnabled = true; _cancel.Visibility = Visibility.Visible; _progress.IsIndeterminate = false; _progress.Maximum = _images.Length; _progress.Value = 0;
        int successes = 0, failures = 0; double total = 0, squares = 0, min = double.MaxValue, max = double.MinValue; string firstError = "";
        foreach (string file in _images)
        {
            if (_cancelBatch) break;
            try { Status($"正在計算樣本 {successes + failures + 1} / {_images.Length}：{Path.GetFileName(file)}"); var frame = await _vm.LocateLightFrameAsync(file); CheckContext(); double value = MeasureFrame(frame); total += value; squares += value * value; min = Math.Min(min, value); max = Math.Max(max, value); successes++; }
            catch (Exception ex) { failures++; if (firstError.Length == 0) firstError = ex.GetBaseException().Message; }
            _progress.Value = successes + failures; _average.Text = $"已處理 {successes + failures} / {_images.Length}  ·  失敗 {failures}";
        }
        if (_cancelBatch) { _average.Text += " · 已停止"; Status("已停止樣本計算，保留原本基準值。"); return; }
        if (successes == 0) throw new InvalidOperationException("全部樣本定位或量測失敗，未變更基準值。" + firstError);
        double mean = total / successes, sd = Math.Sqrt(Math.Max(0, squares / successes - mean * mean)); _base.Text = Number(mean);
        _average.Text = $"平均 {mean:F2}  ·  有效 {successes}/{_images.Length}  ·  失敗 {failures}\n最小 {min:F2} / 最大 {max:F2} / 標準差 {sd:F2}"; _average.ToolTip = firstError.Length == 0 ? null : "第一筆失敗：" + firstError;
        Status(failures == 0 ? "全部樣本計算完成，基準值已帶入；請確認範圍後儲存。" : $"已排除 {failures} 張失敗樣本，請確認樣本有效性後儲存基準值。"); UpdateForm();
    }
    private void Save()
    {
        CheckContext(); ValidateMargins(); var candidate = ReadForm(); if (_frame != null) candidate.GetRoi(_frame.Bounds);
        _vm.SaveLightSettings(candidate); _settings = candidate; _savedState = FormState(); UpdateForm(); Status($"已儲存至「{_vm.CurrentRecipe!.Name}」第 {_station} 站，後續檢測會使用這組設定。");
    }
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_busy) { _cancelBatch = true; e.Cancel = true; Status("等待目前工作完成後即可關閉。"); return; }
        if (FormState() == _savedState) return;
        var choice = MessageBox.Show(this, "本站光源設定有未儲存的變更。要儲存後關閉嗎？", "光源檢測", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (choice == MessageBoxResult.Cancel) { e.Cancel = true; return; } if (choice != MessageBoxResult.Yes) return;
        try { Save(); } catch (Exception ex) { e.Cancel = true; Status(ex.GetBaseException().Message); }
    }
}
