using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LumasharpChecker.viewmodel;

namespace LumasharpChecker.view
{
    public sealed class ParetoPreviewWindow : Window
    {
        public ParetoPreviewWindow(IEnumerable<TestImageResultRow> results)
        {
            Title = "NG 原因統計";
            Width = 500; Height = 390; MinWidth = 400; MinHeight = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brushes.White;
            var rows = results.ToArray();
            var ng = rows.Where(r => !r.IsOverallPass).ToArray();
            var counts = new[] {
                (Name: "處理失敗", Count: ng.Count(r => r.Message.StartsWith("處理失敗:"))),
                (Name: "定位未匹配", Count: ng.Count(r => !r.IsMatched && !r.Message.StartsWith("處理失敗:"))),
                (Name: "亮度不合格", Count: ng.Count(r => r.IsMatched && !r.BrightnessSafe)),
                (Name: "銳利度不合格", Count: ng.Count(r => r.IsMatched && r.IncludeSharpness && !r.SharpnessSafe))
            }.OrderByDescending(x => x.Count).ToArray();
            var stack = new StackPanel { Margin = new Thickness(24) };
            stack.Children.Add(new TextBlock { Text = "NG 原因統計", FontSize = 19, FontWeight = FontWeights.SemiBold });
            stack.Children.Add(new TextBlock { Text = $"目前站別：已檢測 {rows.Length} 張，NG {ng.Length} 張", Margin = new Thickness(0, 8, 0, 16), Foreground = Brushes.DimGray });
            foreach (var item in counts)
            {
                stack.Children.Add(new TextBlock { Text = $"{item.Name}　{item.Count} 張", Margin = new Thickness(0, 6, 0, 4) });
                stack.Children.Add(new ProgressBar { Minimum = 0, Maximum = ng.Length > 0 ? ng.Length : 1, Value = item.Count, Height = 12, Foreground = new SolidColorBrush(Color.FromRgb(38, 143, 184)) });
            }
            stack.Children.Add(new TextBlock { Text = "同一張圖片可能同時有亮度與銳利度異常，因此各項數量可以重疊。定位未匹配時不統計亮度與銳利度原因。", Margin = new Thickness(0, 18, 0, 0), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 12 });
            Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
    }
}
