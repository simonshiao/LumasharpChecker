using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LumasharpChecker.view
{
    /// <summary>Isolated workflow preview; no station or Recipe state is written here.</summary>
    public sealed class AoiWorkflowPanel : UserControl
    {
        private readonly CheckBox _locateToggle = new()
        {
            Content = "定位", IsChecked = true, FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0), Margin = new Thickness(0, 0, 0, 4)
        };

        public event EventHandler? LocatorRequested;
        public event EventHandler? LocatorEnabledChanged;
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
            var content = new StackPanel { Margin = new Thickness(10, 12, 10, 12) };
            content.Children.Add(new TextBlock { Text = "AOI工具流程", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brush("#113044") });
            content.Children.Add(new TextBlock
            {
                Text = "流程試作：勾選定位，點圖示開啟設定與測試",
                FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Brush("#526E7F"),
                Margin = new Thickness(0, 5, 0, 12)
            });

            var grid = new Grid { Height = 432, ClipToBounds = true };
            for (int i = 0; i < 4; i++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(108) });
            var path = new Polyline { Stroke = Brush("#F05B5B"), StrokeThickness = 4, Opacity = 0.65, StrokeLineJoin = PenLineJoin.Round, IsHitTestVisible = false };
            var wiring = new Canvas { IsHitTestVisible = false };
            wiring.Children.Add(path);
            Grid.SetRowSpan(wiring, 4);
            grid.Children.Add(wiring);
            grid.SizeChanged += (_, e) =>
            {
                double left = e.NewSize.Width / 10.0;
                double right = e.NewSize.Width - left;
                path.Points = new PointCollection
                {
                    new Point(left, 49), new Point(right, 49),
                    new Point(right, 157), new Point(left, 157),
                    new Point(left, 265), new Point(right, 265),
                    new Point(right, 373), new Point(left, 373)
                };
            };

            AddRow(grid, 0, new[]
            {
                ("定位", "\uE707", "#42C973", true), ("選向", "\uE7AD", "#8C82DF", false),
                ("選面", "\uE77B", "#D881C4", false), ("前處理", "\uE91B", "#B4C35B", false),
                ("AI前處理", "\uE9CE", "#B8CF67", false)
            });
            AddRow(grid, 1, new[]
            {
                ("平滑二值", "\uE9D9", "#6097C6", false), ("二值化", "\uE790", "#4BAA83", false),
                ("遮罩", "\uE7B7", "#777C87", false), ("尺寸量測", "\uE8F1", "#54AEC5", false),
                ("進階定位", "\uE8AD", "#CF78B7", false)
            });
            AddRow(grid, 2, new[]
            {
                ("區域二值", "\uE8A7", "#A478B0", false), ("差異二值", "\uE8D4", "#A96E72", false),
                ("線段分析", "\uE9D2", "#68A9B2", false), ("圖形比對", "\uE71A", "#C9A36F", false),
                ("AI", "\uE99A", "#B8CF67", false)
            });
            AddRow(grid, 3, new[]
            {
                ("結果", "\uE9D9", "#8DA0AC", false), ("", "", "#FFFFFF", false),
                ("", "", "#FFFFFF", false), ("色碼工具", "\uE8A5", "#728DA4", false),
                ("文字比對", "\uE8D2", "#CC88BC", false)
            });
            content.Children.Add(grid);
            content.Children.Add(new TextBlock
            {
                Text = "灰色工具尚未開放。定位的勾選與設定只用於此測試區，不會改變正式檢測或站別參數。",
                FontSize = 11, Foreground = Brush("#526E7F"), TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            });
            var border = new Border
            {
                Background = Brushes.White, BorderBrush = Brush("#C6E5F1"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7), Child = content
            };
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = border };
        }

        private void AddRow(Grid grid, int rowIndex, (string Name, string Icon, string Color, bool IsLocator)[] items)
        {
            var row = new UniformGrid { Columns = 5, Rows = 1 };
            foreach (var item in items)
            {
                if (item.Name.Length == 0)
                {
                    row.Children.Add(new Border());
                    continue;
                }
                var cell = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 5, 0, 0) };
                if (item.IsLocator) cell.Children.Add(_locateToggle);
                else
                {
                    var check = new CheckBox
                    {
                        Content = item.Name, IsEnabled = false, FontSize = 10,
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(0), Margin = new Thickness(0, 0, 0, 4), ToolTip = "尚未開放"
                    };
                    ToolTipService.SetShowOnDisabled(check, true);
                    cell.Children.Add(check);
                }
                var icon = new Button
                {
                    Width = 47, Height = 47, MinWidth = 0, MinHeight = 0, Padding = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    BorderThickness = new Thickness(3), BorderBrush = Brush(item.Color),
                    Background = Brushes.White, Foreground = Brush("#152D3A"),
                    IsEnabled = item.IsLocator, ToolTip = item.IsLocator ? "開啟定位設定與測試" : "尚未開放"
                };
                ToolTipService.SetShowOnDisabled(icon, true);
                AutomationProperties.SetName(icon, item.IsLocator ? "開啟定位設定" : item.Name + "尚未開放");
                icon.Content = new TextBlock
                {
                    Text = item.Icon, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 22,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                };
                if (item.IsLocator) icon.Click += (_, _) => LocatorRequested?.Invoke(this, EventArgs.Empty);
                cell.Children.Add(icon);
                row.Children.Add(cell);
            }
            Grid.SetRow(row, rowIndex);
            grid.Children.Add(row);
        }

        private static Brush Brush(string color) => (Brush)new BrushConverter().ConvertFromString(color);
    }
}
