using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using LumasharpChecker.model;

namespace LumasharpChecker.view
{
    /// <summary>Preview of AOI tools; settings remain in each tool window.</summary>
    public sealed class AoiWorkflowPanel : UserControl
    {
        private readonly Dictionary<string, TextBlock> _durations = new Dictionary<string, TextBlock>();
        private Button? _continuousButton;
        private readonly TextBlock _timing = new TextBlock { Text = "檢測合計 — · 整體 —", FontSize = 11,
            Foreground = Brushes.SlateGray, Margin = new Thickness(4, 2, 4, 4), TextWrapping = TextWrapping.Wrap };
        public event EventHandler? ContinuousRequested;
        public void SetContinuousRunning(bool running)
        {
            if (_continuousButton != null) _continuousButton.Content = running ? "停止連續檢測" : "連續檢測";
        }
        public void SetContinuousProgress(int done, int total)
        {
            if (_continuousButton != null) _continuousButton.Content = $"停止連續檢測  {done}/{total}";
        }
        public void ClearNativeResults()
        {
            _timing.Text = "檢測合計 — · 整體 —";
            _timing.ToolTip = null;
            foreach (string name in new[] { "定位", "選向", "選面", "光源檢測", "結果" })
                if (_durations.TryGetValue(name, out var label)) { label.Text = "— ms"; label.ToolTip = "尚未執行"; }
        }
        public void ShowNativeResults(NativeRecipeBridge.StageReport result)
        {
            _timing.Text = $"檢測合計 {result.InspectionMs:F1} ms · 整體 {result.TotalMs:F1} ms";
            _timing.ToolTip = result.TimingDetails;
            SetStage("定位", result.Locate, result.LocateMs);
            SetStage("選向", result.SelectWay, result.SelectWayMs);
            SetStage("選面", result.SelectFace, result.SelectFaceMs);
            SetStage("光源檢測", result.Light, result.LightMs);
            if (_durations.TryGetValue("結果", out var label))
            {
                string final = result.Light == "SKIPPED_RECIPE_OFF"
                    ? "OK（光源略過）"
                    : result.Exit == 0 && result.Light == "PASS" ? "OK" : "NG";
                label.Text = final + $" · {result.InspectionMs:F1} ms";
                label.ToolTip = "檢測合計＝定位＋選向＋選面＋光源的工具耗時。\n" + result.TimingDetails;
            }
        }
        private void SetStage(string name, string state, double? elapsed)
        {
            if (!_durations.TryGetValue(name, out var label)) return;
            label.Text = elapsed.HasValue ? $"{elapsed.Value:F1} ms" : state == "SKIPPED_RECIPE_OFF" ? "略過" : "— ms";
            label.ToolTip = string.IsNullOrEmpty(state) ? "未執行" : state;
        }
        private readonly CheckBox _locateToggle = new CheckBox
        {
            Content = "啟用定位", IsChecked = true, FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        private readonly CheckBox _selectWayToggle = new CheckBox
        {
            Content = "使用", IsChecked = true, FontSize = 9,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "勾選：本次流程使用選向；取消勾選：略過選向"
        };
        private readonly CheckBox _selectFaceToggle = new CheckBox
        {
            Content = "使用", IsChecked = true, FontSize = 9,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "勾選：本次流程使用選面；取消勾選：略過選面"
        };
        private readonly CheckBox _lightToggle = new CheckBox
        {
            Content = "使用", IsChecked = true, FontSize = 9,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "勾選：執行亮度檢測；取消勾選：略過亮度判定，結果列為 Retest"
        };
        public event Action<string>? PipelineStatusChanged;
        public void SetPipelineStatus(string message) => PipelineStatusChanged?.Invoke(message);

        public event EventHandler? LocatorRequested;
        public event EventHandler? SelectWayRequested;
        public event EventHandler? SelectFaceRequested;
        public event EventHandler? LocatorEnabledChanged;
        public event EventHandler? SelectWayEnabledChanged;
        public event EventHandler? SelectFaceEnabledChanged;
        public event EventHandler? LightEnabledChanged;
        public event EventHandler? CollapseRequested;
        public event EventHandler? LightRequested;
        public event EventHandler? ResultsRequested;
        public bool IsLocatorEnabled => _locateToggle.IsChecked == true;
        public bool IsSelectWayEnabled => _selectWayToggle.IsChecked == true;
        public bool IsSelectFaceEnabled => _selectFaceToggle.IsChecked == true;
        public bool IsLightInspectionEnabled => _lightToggle.IsChecked == true;
        public void SetToolSelection(string selection, bool lightEnabled = true)
        {
            _locateToggle.IsChecked = selection[0] == '1';
            _selectWayToggle.IsChecked = selection[1] == '1';
            _selectFaceToggle.IsChecked = selection[2] == '1';
            _lightToggle.IsChecked = lightEnabled;
        }

        public AoiWorkflowPanel()
        {
            AutomationProperties.SetName(_locateToggle, "啟用定位測試");
            _locateToggle.Checked += (_, _) => LocatorEnabledChanged?.Invoke(this, EventArgs.Empty);
            _locateToggle.Unchecked += (_, _) => LocatorEnabledChanged?.Invoke(this, EventArgs.Empty);
            _selectWayToggle.Checked += (_, _) => SelectWayEnabledChanged?.Invoke(this, EventArgs.Empty);
            _selectWayToggle.Unchecked += (_, _) => SelectWayEnabledChanged?.Invoke(this, EventArgs.Empty);
            _selectFaceToggle.Checked += (_, _) => SelectFaceEnabledChanged?.Invoke(this, EventArgs.Empty);
            _selectFaceToggle.Unchecked += (_, _) => SelectFaceEnabledChanged?.Invoke(this, EventArgs.Empty);
            _lightToggle.Checked += (_, _) => LightEnabledChanged?.Invoke(this, EventArgs.Empty);
            _lightToggle.Unchecked += (_, _) => LightEnabledChanged?.Invoke(this, EventArgs.Empty);
            Content = BuildContent();
        }

        private UIElement BuildContent()
        {
            const double toolRowHeight = 120;
            var content = new StackPanel { Margin = new Thickness(12, 10, 12, 12) };
            var header = new DockPanel { Height = 30, LastChildFill = false };
            var collapse = new Button { Content = "收合", Padding = new Thickness(9, 2, 9, 2), MinHeight = 28, ToolTip = "收合 AOI 工具側欄" };
            collapse.Click += (_, _) => CollapseRequested?.Invoke(this, EventArgs.Empty);
            DockPanel.SetDock(collapse, Dock.Right);
            header.Children.Add(collapse);
            content.Children.Add(header);
            _continuousButton = new Button { Content = "連續檢測", Margin = new Thickness(3, 3, 3, 5), MinHeight = 32,
                ToolTip = "依序執行目前圖片清單：定位 → 選向 → 選面 → 光源；再次按下停止" };
            AutomationProperties.SetName(_continuousButton, "流程連續檢測或停止");
            _continuousButton.Click += (_, _) => ContinuousRequested?.Invoke(this, EventArgs.Empty);
            content.Children.Add(_continuousButton);
            content.Children.Add(_timing);
            var flow = new Grid { Margin = new Thickness(-3, 3, -3, 0) };
            var wiring = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
            flow.Children.Add(wiring);
            var grid = new UniformGrid { Columns = 4 };
            grid.Children.Add(ToolCard("定位", "\uE707", "開啟原廠定位設定；光源流程會跳過原廠 AI 定位", true, () => LocatorRequested?.Invoke(this, EventArgs.Empty)));
            grid.Children.Add(ToolCard("選向", "\uE7AD", "使用原生 SelectWayTool 檢查目前站別／圖片", true,
                () => SelectWayRequested?.Invoke(this, EventArgs.Empty)));
            grid.Children.Add(ToolCard("選面", "\uE77B", "使用原生 SelectFaceTool 檢查目前站別／圖片", true,
                () => SelectFaceRequested?.Invoke(this, EventArgs.Empty)));
            // 此版本聚焦光源點檢；保留前置定位工具與光源結果節點。
            grid.Children.Add(ToolCard("光源檢測", "\uEA80", "取代原廠流程的燈泡節點；原生定位／選向／選面後使用 Luma 亮度檢測", true, () => LightRequested?.Invoke(this, EventArgs.Empty)));
            grid.Children.Add(ToolCard("結果", "\uE9D9", "查看本站檢測結果", true,
                () => ResultsRequested?.Invoke(this, EventArgs.Empty)));
            int toolCount = grid.Children.Count;
            int rowCount = (toolCount + 3) / 4;
            flow.Height = toolRowHeight * rowCount;
            while (grid.Children.Count < rowCount * 4) grid.Children.Add(new Border());
            // Reverse alternate rows so the visual order follows the serpentine route.
            for (int row = 1; row < rowCount; row += 2)
            {
                int start = row * 4;
                int end = Math.Min(start + 4, grid.Children.Count) - 1;
                while (start < end)
                {
                    var first = grid.Children[start];
                    var last = grid.Children[end];
                    grid.Children.RemoveAt(end);
                    grid.Children.RemoveAt(start);
                    grid.Children.Insert(start, last);
                    grid.Children.Insert(end, first);
                    start++; end--;
                }
            }
            flow.Children.Add(grid);
            flow.SizeChanged += (_, e) =>
            {
                double width = e.NewSize.Width;
                double rowHeight = e.NewSize.Height / rowCount;
                double cellWidth = width / 4.0;
                wiring.Children.Clear();
                for (int row = 0; row < rowCount; row++)
                {
                    int count = Math.Min(4, toolCount - row * 4);
                    double y = rowHeight * row + 18 + 33;
                    for (int column = 0; column < count - 1; column++)
                    {
                        // Keep links in the gutters so they do not cover the tools or MS values.
                        AddConnector(wiring, new Point((column + 1) * cellWidth - 6, y), new Point((column + 1) * cellWidth + 6, y));
                    }
                    if (row < rowCount - 1)
                    {
                        double edgeX = row % 2 == 0 ? width - 2 : 2;
                        double cardEdgeX = row % 2 == 0 ? width - 6 : 6;
                        double nextY = y + rowHeight;
                        AddConnector(wiring, new Point(cardEdgeX, y), new Point(edgeX, y));
                        AddConnector(wiring, new Point(edgeX, y), new Point(edgeX, nextY));
                        AddConnector(wiring, new Point(edgeX, nextY), new Point(cardEdgeX, nextY));
                    }
                }
            };
            content.Children.Add(flow);
            var frame = new Border { Background = Brushes.White, BorderBrush = Brush("#D8E5EC"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Child = content };
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = frame };
        }

        private UIElement ToolCard(string name, string glyph, string detail, bool enabled, Action? action)
        {
            var body = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            body.Children.Add(new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 21, Foreground = Brush(enabled ? "#087AAF" : "#8597A4"), HorizontalAlignment = HorizontalAlignment.Center });
            string displayName = name;
            body.Children.Add(new TextBlock { Text = displayName, FontSize = name == "定位" ? 9 : 10, Foreground = Brush(enabled ? "#1D455B" : "#657D8B"), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
            var button = new Button { Content = body, Margin = new Thickness(6, 4, 6, 4), Padding = new Thickness(1, 3, 1, 3), Height = 58, MinHeight = 0, Background = Brush(enabled ? "#E8F7FD" : "#F5F8FA"), BorderBrush = Brush(enabled ? "#A5D8E9" : "#E1EAF0"), BorderThickness = new Thickness(1), IsEnabled = enabled, ToolTip = enabled ? detail : "尚未開放" };
            AutomationProperties.SetName(button, enabled ? "開啟" + name : name + "尚未開放");
            ToolTipService.SetShowOnDisabled(button, true);
            if (action != null) button.Click += (_, _) => action();
            var cell = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
            cell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            cell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(66) });
            cell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            cell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
            if (name == "定位")
            {
                _locateToggle.Content = null;
                _locateToggle.HorizontalAlignment = HorizontalAlignment.Center;
                _locateToggle.ToolTip = "原生定位工具的設定入口；光源流程不啟用原廠 AI 定位";
                Grid.SetRow(_locateToggle, 0);
                cell.Children.Add(_locateToggle);
            }
            else if (name == "光源檢測")
            {
                AutomationProperties.SetName(_lightToggle, "本次使用光源檢測");
                Grid.SetRow(_lightToggle, 0);
                cell.Children.Add(_lightToggle);
            }
            else if (name != "結果")
            {
                var nativeToggle = name == "選向" || name == "選面";
                var toggle = name == "選向" ? _selectWayToggle :
                    name == "選面" ? _selectFaceToggle :
                    new CheckBox { IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Center,
                        ToolTip = "工具尚未接入" };
                AutomationProperties.SetName(toggle, nativeToggle ? "本次使用" + name : name + "尚未開放");
                if (!nativeToggle) ToolTipService.SetShowOnDisabled(toggle, true);
                Grid.SetRow(toggle, 0);
                cell.Children.Add(toggle);
            }
            else cell.Children.Add(new Border { Height = 18 });
            Grid.SetRow(button, 1);
            cell.Children.Add(button);
            var duration = new TextBlock { Text = "— ms", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("#758A98"), FontSize = 10, MinHeight = 16 };
            _durations[name] = duration;
            Grid.SetRow(duration, 2);
            cell.Children.Add(duration);
            return cell;
        }

        private static void AddConnector(Canvas canvas, Point start, Point end)
        {
            canvas.Children.Add(new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = Brush("#A4CEDC"),
                StrokeThickness = 1.5,
                Opacity = 0.75,
                IsHitTestVisible = false
            });
        }

        private static Brush Brush(string color) => (Brush)new BrushConverter().ConvertFromString(color);
    }
}
