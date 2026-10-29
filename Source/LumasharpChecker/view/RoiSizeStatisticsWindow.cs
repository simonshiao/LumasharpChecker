using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LumasharpChecker.viewmodel;

namespace LumasharpChecker.view
{
    /// <summary>以真實定位結果顯示像素尺寸，不推測毫米數值。</summary>
    public sealed class RoiSizeStatisticsWindow : Window
    {
        public RoiSizeStatisticsWindow(IEnumerable<TestImageResultRow> results)
        {
            Title = "定位尺寸統計";
            Width = 540;
            Height = 395;
            MinWidth = 440;
            MinHeight = 340;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Microsoft JhengHei UI");
            Background = Brushes.White;
            var all = results.ToArray();
            var located = all.Where(x => x.IsMatched && x.RoiW > 0 && x.RoiH > 0).ToArray();
            var panel = new StackPanel { Margin = new Thickness(23, 19, 23, 19) };
            panel.Children.Add(new TextBlock
            {
                Text = "定位尺寸統計", FontSize = 19, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(25, 57, 76))
            });
            panel.Children.Add(new TextBlock
            {
                Text = $"目前已判定 {all.Length} 張／成功定位 {located.Length} 張",
                Margin = new Thickness(0, 9, 0, 17), Foreground = Brushes.SlateGray
            });
            if (located.Length == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "目前沒有可統計的定位尺寸。請先在 AOI 工作區按「重新檢測」。",
                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0)
                });
            }
            else
            {
                AddMetric(panel, "寬度", located.Select(x => (double)x.RoiW));
                AddMetric(panel, "高度", located.Select(x => (double)x.RoiH));
                AddMetric(panel, "面積", located.Select(x => (double)x.RoiW * x.RoiH), "px²");
            }
            panel.Children.Add(new TextBlock
            {
                Text = "統計採用現有檢測結果中的定位 ROI，單位為像素；未經校正不得當作實體尺寸（mm）。",
                Margin = new Thickness(0, 19, 0, 0), TextWrapping = TextWrapping.Wrap,
                FontSize = 12, Foreground = Brushes.SlateGray
            });
            Content = new ScrollViewer
            {
                Content = panel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
        }

        private static void AddMetric(Panel panel, string label, IEnumerable<double> values, string unit = "px")
        {
            var data = values.ToArray();
            panel.Children.Add(new TextBlock
            {
                Text = $"{label}　 最小 {data.Min():F1}　平均 {data.Average():F1}　最大 {data.Max():F1} {unit}",
                Margin = new Thickness(0, 10, 0, 10),
                Foreground = new SolidColorBrush(Color.FromRgb(17, 70, 102)),
                FontSize = 13
            });
        }
    }
}
