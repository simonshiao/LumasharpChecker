using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LumasharpChecker.view
{
    /// <summary>Preview of AOI tools; settings remain in each tool window.</summary>
    public sealed class AoiWorkflowPanel : UserControl
    {
        private readonly CheckBox _locateToggle = new CheckBox
        {
            Content = "啟用定位", IsChecked = true, FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        public event Action<string>? PipelineStatusChanged;
        public void SetPipelineStatus(string message) => PipelineStatusChanged?.Invoke(message);

        public event EventHandler? LocatorRequested;
        public event EventHandler? SelectWayRequested;
        public event EventHandler? SelectFaceRequested;
        public event EventHandler? LocatorEnabledChanged;
        public event EventHandler? CollapseRequested;
        public event EventHandler? LightRequested;
        public event EventHandler? RetestRequested;
        public event EventHandler? ParetoRequested;
        public event EventHandler? ResultsRequested;
        public event EventHandler? SizeStatisticsRequested;
        public bool IsLocatorEnabled => _locateToggle.IsChecked == true;

        public AoiWorkflowPanel()
        {
            AutomationProperties.SetName(_locateToggle, "啟用定位測試");
            _locateToggle.Checked += (_, _) => LocatorEnabledChanged?.Invoke(this, EventArgs.Empty);
            _locateToggle.Unchecked += (_, _) => LocatorEnabledChanged?.Invoke(this, EventArgs.Empty);
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
            var flow = new Grid { Height = toolRowHeight * 3, Margin = new Thickness(-3, 3, -3, 0) };
            var wiring = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
            flow.Children.Add(wiring);
            var grid = new UniformGrid { Columns = 4 };
            grid.Children.Add(ToolCard("定位", "\uE707", "開啟原廠定位設定；光源流程會跳過原廠 AI 定位", true, () => LocatorRequested?.Invoke(this, EventArgs.Empty)));
            grid.Children.Add(ToolCard("選向", "\uE7AD", "使用原生 SelectWayTool 檢查目前站別／圖片", true,
                () => SelectWayRequested?.Invoke(this, EventArgs.Empty)));
            grid.Children.Add(ToolCard("選面", "\uE77B", "使用原生 SelectFaceTool 檢查目前站別／圖片", true,
                () => SelectFaceRequested?.Invoke(this, EventArgs.Empty)));
            grid.Children.Add(ToolCard("AI前處理", "\uE9CE", "", false, null));
            // 光源檢測沿用原廠燈泡節點位置：AI 前處理後、遮罩前。
            // Luma 的現有 LightRequested 行為維持不變。
            grid.Children.Add(ToolCard("光源檢測", "\uEA80", "取代原廠流程的燈泡節點；原生定位／選向／選面後使用 Luma 亮度檢測", true, () => LightRequested?.Invoke(this, EventArgs.Empty)));
            grid.Children.Add(ToolCard("遮罩", "\uE7B7", "", false, null));
            grid.Children.Add(ToolCard("尺寸量測", "\uE8F1", "", false, null));
            grid.Children.Add(ToolCard("進階定位", "\uE8AD", "", false, null));
            grid.Children.Add(ToolCard("AI", "\uE99A", "", false, null));
            grid.Children.Add(ToolCard("結果", "\uE9D9", "查看本站檢測結果", true,
                () => ResultsRequested?.Invoke(this, EventArgs.Empty)));
            int toolCount = grid.Children.Count;
            int rowCount = (toolCount + 3) / 4;
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
            content.Children.Add(new TextBlock { Text = "檢測與分析", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brush("#29465B"), Margin = new Thickness(3, 10, 0, 5) });
            var actions = new UniformGrid { Columns = 4, Margin = new Thickness(-3, 0, -3, 0) };
            actions.Children.Add(AuxiliaryCard("重新檢測", "\uE72C", () => RetestRequested?.Invoke(this, EventArgs.Empty)));
            actions.Children.Add(AuxiliaryCard("柏拉圖", "\uE9D2", () => ParetoRequested?.Invoke(this, EventArgs.Empty)));
            actions.Children.Add(AuxiliaryCard("尺寸統計", "\uE80A",
                () => SizeStatisticsRequested?.Invoke(this, EventArgs.Empty)));
            actions.Children.Add(AuxiliaryCard("檢測結果", "\uE890", () => ResultsRequested?.Invoke(this, EventArgs.Empty)));
            actions.Children.Add(AuxiliaryCard("AI 訓練", "\uE99A"));
            content.Children.Add(actions);
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
            else if (name != "光源檢測" && name != "結果")
            {
                var nativeToggle = name == "選向" || name == "選面";
                var toggle = new CheckBox { IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Center,
                    ToolTip = nativeToggle ? "原廠 Recipe 控制此工具是否啟用；此處不修改原廠參數" : "工具尚未接入" };
                AutomationProperties.SetName(toggle, nativeToggle ? name + "依原廠 Recipe 設定" : name + "尚未開放");
                ToolTipService.SetShowOnDisabled(toggle, true);
                Grid.SetRow(toggle, 0);
                cell.Children.Add(toggle);
            }
            else cell.Children.Add(new Border { Height = 18 });
            Grid.SetRow(button, 1);
            cell.Children.Add(button);
            var duration = new TextBlock { Text = "— ms", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("#758A98"), FontSize = 10, MinHeight = 16 };
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

        private static UIElement AuxiliaryCard(string name, string glyph, Action? action = null)
        {
            var body = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            body.Children.Add(new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 21, Foreground = Brush(action != null ? "#087AAF" : "#8597A4"), HorizontalAlignment = HorizontalAlignment.Center });
            body.Children.Add(new TextBlock { Text = name, FontSize = 11, Foreground = Brush(action != null ? "#1D455B" : "#657D8B"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 7, 0, 0) });
            var button = new Button { Content = body, Margin = new Thickness(6, 4, 6, 4), Padding = new Thickness(1, 3, 1, 3), Height = 58, MinHeight = 0, Background = Brush(action != null ? "#E8F7FD" : "#F5F8FA"), BorderBrush = Brush(action != null ? "#A5D8E9" : "#E1EAF0"), BorderThickness = new Thickness(1), IsEnabled = action != null, ToolTip = action != null ? "開啟" + name : "尚未接入" };
            if (action != null) button.Click += (_, _) => action();
            AutomationProperties.SetName(button, action != null ? "開啟" + name : name + "尚未接入");
            ToolTipService.SetShowOnDisabled(button, true);
            return button;
        }

        private static Brush Brush(string color) => (Brush)new BrushConverter().ConvertFromString(color);
    }
}
