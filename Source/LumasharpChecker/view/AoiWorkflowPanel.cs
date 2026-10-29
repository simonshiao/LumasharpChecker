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

        public event EventHandler? LocatorRequested;
        public event EventHandler? LocatorEnabledChanged;
        public event EventHandler? CollapseRequested;
        public event EventHandler? LightRequested;
        public event EventHandler? RetestRequested;
        public event EventHandler? ParetoRequested;
        public event EventHandler? ResultsRequested;
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
            var content = new StackPanel { Margin = new Thickness(16) };
            var header = new DockPanel { LastChildFill = true };
            var collapse = new Button { Content = "收合", Padding = new Thickness(9, 3, 9, 3), MinHeight = 30, ToolTip = "收合 AOI 工具側欄" };
            collapse.Click += (_, _) => CollapseRequested?.Invoke(this, EventArgs.Empty);
            DockPanel.SetDock(collapse, Dock.Right);
            header.Children.Add(collapse);
            header.Children.Add(new TextBlock { Text = "AOI 工具流程", FontSize = 17, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("#18354A") });
            content.Children.Add(header);
            var flow = new Grid { Margin = new Thickness(-3, 0, -3, 0) };
            var wiring = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
            var route = new Polyline { Stroke = Brush("#A4CEDC"), StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round, Opacity = 0.85 };
            wiring.Children.Add(route);
            flow.Children.Add(wiring);
            var grid = new UniformGrid { Columns = 4 };
            grid.Children.Add(ToolCard("定位", "\uE707", "定位設定", true, () => LocatorRequested?.Invoke(this, EventArgs.Empty)));
            grid.Children.Add(ToolCard("選向", "\uE7AD", "", false, null));
            grid.Children.Add(ToolCard("選面", "\uE77B", "", false, null));
            grid.Children.Add(ToolCard("AI前處理", "\uE9CE", "", false, null));
            grid.Children.Add(ToolCard("二值化", "\uE790", "", false, null));
            grid.Children.Add(ToolCard("遮罩", "\uE7B7", "", false, null));
            grid.Children.Add(ToolCard("尺寸量測", "\uE8F1", "", false, null));
            grid.Children.Add(ToolCard("進階定位", "\uE8AD", "", false, null));
            grid.Children.Add(ToolCard("AI", "\uE99A", "", false, null));
            grid.Children.Add(ToolCard("光源檢測", "\uEA80", "使用 Luma 亮度檢測", true, () => LightRequested?.Invoke(this, EventArgs.Empty)));
            grid.Children.Add(ToolCard("結果", "\uE9D9", "", false, null));
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
                double left = width / 8.0, right = width * 7.0 / 8.0;
                var points = new PointCollection();
                for (int row = 0; row < rowCount; row++)
                {
                    double y = rowHeight * row + 16 + 5 + 34;
                    int count = Math.Min(4, toolCount - row * 4);
                    double end = row % 2 == 0 ? width * (count - 0.5) / 4.0 : width * (4 - count + 0.5) / 4.0;
                    points.Add(new Point(row % 2 == 0 ? left : right, y));
                    points.Add(new Point(end, y));
                }
                route.Points = points;
            };
            content.Children.Add(flow);
            content.Children.Add(new TextBlock { Text = "檢測與分析", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brush("#29465B"), Margin = new Thickness(3, 14, 0, 6) });
            var actions = new UniformGrid { Columns = 4, Margin = new Thickness(-3, 0, -3, 0) };
            actions.Children.Add(AuxiliaryCard("重新檢測", "\uE72C", () => RetestRequested?.Invoke(this, EventArgs.Empty)));
            actions.Children.Add(AuxiliaryCard("柏拉圖", "\uE9D2", () => ParetoRequested?.Invoke(this, EventArgs.Empty)));
            actions.Children.Add(AuxiliaryCard("尺寸統計", "\uE80A"));
            actions.Children.Add(AuxiliaryCard("檢測結果", "\uE890", () => ResultsRequested?.Invoke(this, EventArgs.Empty)));
            actions.Children.Add(AuxiliaryCard("AI 訓練", "\uE99A"));
            content.Children.Add(actions);
            content.Children.Add(new TextBlock { Text = "工具時間待檢測流程接入後顯示；光源測試使用已儲存的站別設定。", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#526D7C"), FontSize = 11, Margin = new Thickness(3, 12, 3, 0) });
            var frame = new Border { Background = Brushes.White, BorderBrush = Brush("#D8E5EC"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Child = content };
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = frame };
        }

        private UIElement ToolCard(string name, string glyph, string detail, bool enabled, Action? action)
        {
            var body = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            body.Children.Add(new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 21, Foreground = Brush(enabled ? "#087AAF" : "#8597A4"), HorizontalAlignment = HorizontalAlignment.Center });
            body.Children.Add(new TextBlock { Text = name, FontSize = 11, Foreground = Brush(enabled ? "#1D455B" : "#657D8B"), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 7, 0, 0) });
            var button = new Button { Content = body, Margin = new Thickness(5), Padding = new Thickness(2, 8, 2, 8), Height = 68, MinHeight = 0, Background = Brush(enabled ? "#E8F7FD" : "#F5F8FA"), BorderBrush = Brush(enabled ? "#A5D8E9" : "#E1EAF0"), BorderThickness = new Thickness(1), IsEnabled = enabled, ToolTip = enabled ? detail : "尚未開放" };
            AutomationProperties.SetName(button, enabled ? "開啟" + name : name + "尚未開放");
            ToolTipService.SetShowOnDisabled(button, true);
            if (action != null) button.Click += (_, _) => action();
            var cell = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
            if (name == "定位")
            {
                _locateToggle.Content = null;
                _locateToggle.HorizontalAlignment = HorizontalAlignment.Center;
                _locateToggle.ToolTip = "啟用定位設定入口（目前不影響正式檢測）";
                cell.Children.Add(_locateToggle);
            }
            else if (name != "光源檢測" && name != "結果")
            {
                var toggle = new CheckBox { IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Center, ToolTip = "工具尚未接入" };
                AutomationProperties.SetName(toggle, name + "尚未開放");
                ToolTipService.SetShowOnDisabled(toggle, true);
                cell.Children.Add(toggle);
            }
            else cell.Children.Add(new Border { Height = 16 });
            cell.Children.Add(button);
            cell.Children.Add(new TextBlock { Text = name == "光源檢測" || name == "結果" ? "" : "— ms", HorizontalAlignment = HorizontalAlignment.Center, Foreground = Brush("#758A98"), FontSize = 10, Margin = new Thickness(0, 0, 0, 4) });
            return cell;
        }

        private static UIElement AuxiliaryCard(string name, string glyph, Action? action = null)
        {
            var body = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            body.Children.Add(new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 21, Foreground = Brush(action != null ? "#087AAF" : "#8597A4"), HorizontalAlignment = HorizontalAlignment.Center });
            body.Children.Add(new TextBlock { Text = name, FontSize = 11, Foreground = Brush(action != null ? "#1D455B" : "#657D8B"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 7, 0, 0) });
            var button = new Button { Content = body, Margin = new Thickness(5), Padding = new Thickness(2, 8, 2, 8), Height = 68, MinHeight = 0, Background = Brush(action != null ? "#E8F7FD" : "#F5F8FA"), BorderBrush = Brush(action != null ? "#A5D8E9" : "#E1EAF0"), BorderThickness = new Thickness(1), IsEnabled = action != null, ToolTip = action != null ? "開啟" + name : "尚未接入" };
            if (action != null) button.Click += (_, _) => action();
            AutomationProperties.SetName(button, action != null ? "開啟" + name : name + "尚未接入");
            ToolTipService.SetShowOnDisabled(button, true);
            return button;
        }

        private static Brush Brush(string color) => (Brush)new BrushConverter().ConvertFromString(color);
    }
}
