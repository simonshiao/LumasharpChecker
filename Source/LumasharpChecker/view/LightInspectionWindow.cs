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
    private bool _busy, _loading = true;
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
    private readonly TextBlock _roiInfo = Text(""), _range = Text(""), _average = Text("");
    private readonly TextBlock _value = Text("—", 30), _verdict = Text("尚未量測", 14), _resultDetail = Text("");
    private readonly TextBox _min = Field(), _max = Field(), _base = Field(), _low = Field(), _high = Field();
    private readonly ComboBox _mode = new() { MinHeight = 32, Margin = new Thickness(0, 4, 0, 8) };
    private readonly StackPanel _fixed = new(), _percent = new();
    private readonly Grid _body = new();
    private readonly StackPanel _actions = new() { Orientation = Orientation.Horizontal };
    private readonly ProgressBar _progress = new() { Height = 3, Visibility = Visibility.Collapsed };
    private readonly Button _save;
    public bool SettingsSaved { get; private set; }

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
        var heading = new StackPanel(); heading.Children.Add(Text("光源檢測", 23, true)); heading.Children.Add(Text($"{vm.CurrentRecipe.Name}  /  第 {_station} 站"));
        header.Children.Add(heading); root.Children.Add(header);
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
        AutomationProperties.SetName(_canvas, "光源 ROI 編輯區");
        var imageBorder = new Border { Background = ColorBrush("#202831"), CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Child = _imageView };
        Grid.SetRow(imageBorder, 1); preview.Children.Add(imageBorder);
        var caption = new StackPanel { Margin = new Thickness(2, 10, 0, 0) }; _file.TextTrimming = TextTrimming.CharacterEllipsis; _file.TextWrapping = TextWrapping.NoWrap;
        caption.Children.Add(_file); Grid.SetRow(caption, 2); preview.Children.Add(caption); _body.Children.Add(preview);
        var side = new Grid(); side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); side.RowDefinitions.Add(new RowDefinition());
        var result = new StackPanel(); var resultTop = new DockPanel(); _verdict.Foreground = Accent; _verdict.FontWeight = FontWeights.SemiBold;
        DockPanel.SetDock(_verdict, Dock.Right); resultTop.Children.Add(_verdict); resultTop.Children.Add(Text("本張亮度 · HSV V", 12)); result.Children.Add(resultTop); result.Children.Add(_value); result.Children.Add(_resultDetail);
        side.Children.Add(Card(result, 16, new Thickness(0, 0, 0, 10)));
        var settings = new StackPanel(); settings.Children.Add(Text("量測區域", 15, true)); _roiInfo.Margin = new Thickness(0, 4, 0, 4); settings.Children.Add(_roiInfo);
        settings.Children.Add(Separator()); settings.Children.Add(Text("基準亮度", 15, true));
        var baseActions = new WrapPanel();
        var setStandard = Button("設為標準件並儲存參數", () =>
        {
            RequireFrame();
            double baseline = MeasureFrame(_frame!);
            // The standard-part action derives a valid baseline from the current image.
            // Read the other fields first without rejecting the old (possibly zero) baseline.
            var candidate = ReadForm(validateLimits: false);
            candidate.BaseValue = baseline;
            candidate.EffectiveLimits();
            candidate.GetRoi(_frame!.Bounds);
            string saved = _vm.SaveLightStandardPart(_path, candidate);
            SettingsSaved = true;
            SetBaseline(baseline);
            _settings = candidate; _savedState = FormState(); UpdateForm();
            Status(saved);
            return Task.CompletedTask;
        });
        setStandard.ToolTip = "保存目前圖片作為本站標準件，並一併儲存目前 ROI、基準亮度與允收範圍。";
        baseActions.Children.Add(setStandard);
        baseActions.Children.Add(Button("計算全部樣本基準值", CalculateAllSamplesAsync));
        baseActions.Children.Add(Button("還原樣本…", RestoreSampleAsync));
        baseActions.Children.Add(Button("重設 ROI", () =>
        {
            RequireFrame(); _settings.ResetRoi(_frame!.Bounds); ApplyRoi(_settings.GetRoi(_frame.Bounds));
            Status("已依目前定位範圍重設 ROI，尚未儲存；請調整後重新設為標準件。");
            return Task.CompletedTask;
        }));
        settings.Children.Add(baseActions); settings.Children.Add(Labeled("亮度（0～255）", _base)); _average.Margin = new Thickness(0, 4, 0, 0); settings.Children.Add(_average);
        settings.Children.Add(Separator()); settings.Children.Add(Text("允收範圍", 15, true));
        _mode.Items.Add("百分比偏差"); _mode.Items.Add("固定上下限"); _mode.SelectedItem = _settings.LimitMode; settings.Children.Add(_mode);
        _percent.Children.Add(Fields(new[] { "下限偏差（%）", "上限偏差（%）" }, new[] { _low, _high }));
        _fixed.Children.Add(Fields(new[] { "亮度下限", "亮度上限" }, new[] { _min, _max })); settings.Children.Add(_percent); settings.Children.Add(_fixed);
        _range.Margin = new Thickness(0, 8, 0, 0); _range.FontWeight = FontWeights.SemiBold; settings.Children.Add(_range);
        var scroll = new ScrollViewer { Content = settings, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(16) };
        var settingsCard = Card(scroll, 0, new Thickness(0)); Grid.SetRow(settingsCard, 1); side.Children.Add(settingsCard); Grid.SetColumn(side, 1); _body.Children.Add(side); Grid.SetRow(_body, 1); root.Children.Add(_body);
        var footer = new Grid { Margin = new Thickness(24, 0, 24, 16) }; footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); Grid.SetColumnSpan(_progress, 2); footer.Children.Add(_progress);
        _status.Margin = new Thickness(0, 10, 16, 0); _status.VerticalAlignment = VerticalAlignment.Center; Grid.SetRow(_status, 1); footer.Children.Add(_status);
        var footerActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        _actions.Children.Add(Button("測試本張", async () => { if (_frame == null) await LoadFrameAsync(); else Measure(); }));
        _save = Button("儲存本站設定", () => { Save(); return Task.CompletedTask; }); _save.Background = Accent; _save.Foreground = SystemColors.HighlightTextBrush; _save.FontWeight = FontWeights.SemiBold; _actions.Children.Add(_save);
        footerActions.Children.Add(_actions); Grid.SetColumn(footerActions, 1); Grid.SetRow(footerActions, 1); footer.Children.Add(footerActions); Grid.SetRow(footer, 2); root.Children.Add(footer); Content = root;
        _min.Text = Number(_settings.Minimum); _max.Text = Number(_settings.Maximum); _base.Text = Number(_settings.BaseValue); _low.Text = Number(_settings.PercentLower); _high.Text = Number(_settings.PercentUpper);
        foreach (var field in new[] { _min, _max, _base, _low, _high }) field.TextChanged += (_, _) => { if (!_loading) { InvalidateResult(); UpdateForm(); } };
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
        finally { _busy = false; _body.IsEnabled = _actions.IsEnabled = true; _progress.Visibility = Visibility.Collapsed; UpdateForm(); }
    }
    private void CheckContext() { if (_vm.CurrentRecipe?.Id != _recipeId || _vm.CurrentStationNumber != _station) throw new InvalidOperationException("配方或站別已切換，請重新開啟光源設定。"); }
    private void Status(string text) { _status.Text = text; _status.Foreground = Ink; }
    private static string Number(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);
    private string FormState() => string.Join("|", new[] { _settings.HasRoi.ToString(), _settings.Left.ToString(), _settings.Top.ToString(), _settings.Right.ToString(), _settings.Bottom.ToString(), _mode.SelectedItem?.ToString(), _min.Text, _max.Text, _base.Text, _low.Text, _high.Text });
    private static double Read(TextBox field, string label, double maximum)
    {
        bool valid = double.TryParse(field.Text, out double value) && !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= maximum;
        field.BorderBrush = valid ? Line : Brushes.Firebrick; field.ToolTip = valid ? null : $"{label}須為 0～{maximum} 的數值。";
        if (!valid) throw new InvalidOperationException($"{label}須為 0～{maximum} 的數值。"); return value;
    }
    private LightInspectionSettings ReadForm(bool validateLimits = true)
    {
        // Validate a copy: incomplete input must not change the active measurement settings.
        var c = new LightInspectionSettings { Left = _settings.Left, Top = _settings.Top, Right = _settings.Right, Bottom = _settings.Bottom, HasRoi = _settings.HasRoi,
            Minimum = _settings.Minimum, Maximum = _settings.Maximum, BaseValue = _settings.BaseValue, PercentLower = _settings.PercentLower, PercentUpper = _settings.PercentUpper, LimitMode = (string)_mode.SelectedItem };
        c.BaseValue = Read(_base, "基準亮度", 255);
        if (c.LimitMode == "百分比偏差") { c.PercentLower = Read(_low, "下限偏差", 100); c.PercentUpper = Read(_high, "上限偏差", 100); }
        else { c.Minimum = Read(_min, "亮度下限", 255); c.Maximum = Read(_max, "亮度上限", 255); }
        if (validateLimits) c.EffectiveLimits(); return c;
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
        _start = null; _frame = null; _canvas.ReleaseMouseCapture(); _located.Visibility = _roi.Visibility = Visibility.Collapsed; HideHandles(); _canvas.Cursor = Cursors.Arrow; InvalidateResult(); SetImage(_path); Status("正在定位影像…");
        _frame = await _vm.LocateLightFrameAsync(_path); CheckContext(); SetImage(_frame.ImagePath); Show(_located, _frame.Bounds);
        bool hadRoi = _settings.HasRoi;
        if (_settings.EnsureEditableRoi(_frame.Bounds))
        {
            ApplyRoi(_settings.GetRoi(_frame.Bounds));
            Status(hadRoi ? "原 ROI 不適用目前定位尺寸，已重建可調整的 ROI，尚未儲存；請確認後重新設為標準件。"
                : "已依目前定位尺寸建立初始 ROI，尚未儲存；請調整後設為標準件。");
            return;
        }
        var roi = _settings.GetRoi(_frame.Bounds); ShowRoi(roi);
        try { Measure(); } catch (Exception ex) { Status("定位與選向完成；" + ex.Message); }
    }
    private void RequireFrame() { if (_frame == null) throw new InvalidOperationException("請先完成目前圖片的定位與選向；可按「重新定位」重試。"); }
    private void WireCanvas()
    {
        _canvas.PreviewMouseWheel += async (_, e) =>
        {
            if (_busy || _start != null || _images.Length < 2) return;
            e.Handled = true;
            await RunAsync(() => SelectImageAsync(e.Delta < 0 ? 1 : -1));
        };
        _canvas.MouseLeftButtonDown += (_, e) =>
        {
            if (_busy || _frame == null) return; var p = e.GetPosition(_canvas);
            var part = HitRoi(p); if (part == RoiPart.None) return;
            _canvas.Focus(); _start = p; _dragPart = part;
            _dragOriginal = part == RoiPart.Draw ? RectD.Empty : _settings.GetRoi(_frame.Bounds);
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
            if (e.Key == Key.Escape) { _start = null; _canvas.ReleaseMouseCapture(); RefreshRoiVisual(); Status("已取消這次 ROI 調整。"); e.Handled = true; return; }
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
        if (!_settings.HasRoi) return _frame.Bounds.Contains((int)p.X, (int)p.Y) ? RoiPart.Draw : RoiPart.None;
        try { return RoiEditorGeometry.HitTest(_settings.GetRoi(_frame.Bounds), p.X, p.Y, 7 / ImageScale()); }
        catch (InvalidOperationException) { return _frame.Bounds.Contains((int)p.X, (int)p.Y) ? RoiPart.Draw : RoiPart.None; }
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
    private void ApplyRoi(RectD roi)
    {
        var box = _frame!.Bounds; _settings.Left = roi.Left - box.Left; _settings.Top = roi.Top - box.Top; _settings.Right = box.Right - roi.Right; _settings.Bottom = box.Bottom - roi.Bottom; _settings.HasRoi = true;
        ShowRoi(roi); _canvas.Cursor = Cursors.Arrow; InvalidateResult(); UpdateForm(); _average.Text = ""; Status("ROI 已變更，尚未儲存。");
    }
    private void UpdateRoiInfo(RectD roi) => _roiInfo.Text = $"{roi.Width} × {roi.Height} px";
    private void SetImage(string path) { var b = new BitmapImage(); b.BeginInit(); b.UriSource = new Uri(path, UriKind.Absolute); b.CacheOption = BitmapCacheOption.OnLoad; b.EndInit(); b.Freeze(); _image.Source = b; _canvas.Width = _image.Width = b.PixelWidth; _canvas.Height = _image.Height = b.PixelHeight; }
    private static void Show(Shape shape, RectD r) { Canvas.SetLeft(shape, r.X); Canvas.SetTop(shape, r.Y); shape.Width = r.Width; shape.Height = r.Height; shape.Visibility = Visibility.Visible; }
    private void Measure()
    {
        RequireFrame(); if (!_settings.HasRoi) throw new InvalidOperationException("請先設定光源 ROI。"); var candidate = ReadForm(); var limits = candidate.EffectiveLimits(); var timer = System.Diagnostics.Stopwatch.StartNew(); double value = MeasureFrame(_frame!); timer.Stop(); _settings = candidate;
        bool pass = value >= limits.Min && value <= limits.Max; _value.Text = value.ToString("F2"); _value.Foreground = Ink; _verdict.Text = pass ? "OK · 範圍內" : "NG · 超出範圍"; _verdict.Foreground = SystemParameters.HighContrast ? Ink : ColorBrush(pass ? "#167147" : "#B22F36");
        _resultDetail.Text = $"允收 {limits.Min:F2} ～ {limits.Max:F2}  ·  {timer.Elapsed.TotalMilliseconds:F1} ms"; Status("量測完成。");
    }
    private double MeasureFrame(NativeLocationService.Frame frame)
    {
        return LightBrightnessMeasurement.Measure(frame, _settings);
    }
    private async Task CalculateAllSamplesAsync()
    {
        CheckContext();
        if (!_settings.HasRoi) throw new InvalidOperationException("請先設定光源 ROI，才能計算樣本基準值。");
        if (_images.Length == 0) throw new InvalidOperationException("尚未載入可計算的樣本圖片。");
        int successes = 0, failures = 0;
        double total = 0;
        foreach (string file in _images)
        {
            NativeLocationService.Frame frame;
            try
            {
                frame = await _vm.LocateLightFrameAsync(file);
            }
            catch { failures++; continue; }
            CheckContext();
            try { total += MeasureFrame(frame); successes++; }
            catch { failures++; }
        }
        if (successes == 0) throw new InvalidOperationException("全部樣本定位或 ROI 量測失敗，未產生基準值。");
        double mean = total / successes;
        _base.Text = Number(mean);
        _average.Text = $"全部樣本平均 {mean:F2}（有效 {successes}/{_images.Length}，失敗 {failures}）";
        Status("樣本平均已帶入基準亮度；請確認允收範圍後儲存本站設定。");
    }
    private void SetBaseline(double value) { _base.Text = Number(value); _average.Text = $"基準亮度 {value:F2}  ·  使用本張圖片"; Status("基準亮度已帶入，正在儲存本站參數…"); UpdateForm(); }
    private void Save()
    {
        CheckContext(); if (!_settings.HasRoi) throw new InvalidOperationException("請先設定光源 ROI。"); var candidate = ReadForm(); if (_frame != null) candidate.GetRoi(_frame.Bounds);
        _vm.SaveLightSettings(candidate); SettingsSaved = true; _settings = candidate; _savedState = FormState(); UpdateForm(); Status($"已儲存至「{_vm.CurrentRecipe!.Name}」第 {_station} 站，後續檢測會使用這組設定。");
    }
    private async Task RestoreSampleAsync()
    {
        CheckContext();
        string directory = _vm.LightSampleHistoryDirectory;
        if (!Directory.Exists(directory) || !Directory.EnumerateFiles(directory, "sample-*.zip").Any())
        { Status("本站尚未建立樣本還原點；設為標準件後會自動保存。"); return; }
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "選擇本站樣本還原點（原圖與光源參數一起還原）",
            InitialDirectory = directory, Filter = "樣本還原點|sample-*.zip", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        CheckContext();
        _settings = _vm.RestoreLightStandardPart(dialog.FileName);
        SettingsSaved = true;
        _loading = true;
        try
        {
            _min.Text = Number(_settings.Minimum); _max.Text = Number(_settings.Maximum);
            _base.Text = Number(_settings.BaseValue); _low.Text = Number(_settings.PercentLower); _high.Text = Number(_settings.PercentUpper);
            _mode.SelectedItem = _settings.LimitMode;
        }
        finally { _loading = false; }
        _path = _vm.GetCurrentStationPatternPath() ?? _path;
        SetImage(_path); _savedState = FormState(); InvalidateResult(); UpdateForm();
        await LoadFrameAsync();
        if (FormState() == _savedState)
            Status("已還原本站樣本原圖與光源參數；舊判定已清除，請重新測試。");
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_busy) { e.Cancel = true; Status("處理中，請稍後再關閉。"); return; }
        if (FormState() == _savedState) return;
        var choice = MessageBox.Show(this, "本站光源設定有未儲存的變更。要儲存後關閉嗎？", "光源檢測", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (choice == MessageBoxResult.Cancel) { e.Cancel = true; return; } if (choice != MessageBoxResult.Yes) return;
        try { Save(); } catch (Exception ex) { e.Cancel = true; Status(ex.GetBaseException().Message); }
    }
}
