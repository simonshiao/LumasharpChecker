using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LumasharpChecker.model;

namespace LumasharpChecker.view
{
    internal sealed class TrainingImageSelectionWindow : Window
    {
        private readonly Dictionary<int, ComboBox> _choices = new();
        public List<TrainingImageCandidate> SelectedCandidates => _choices.Values
            .Select(box => box.SelectedItem as TrainingImageCandidate).Where(item => item != null && !string.IsNullOrEmpty(item.FolderName)).Cast<TrainingImageCandidate>().ToList();

        public TrainingImageSelectionWindow(Window owner, string product, IReadOnlyList<TrainingImageCandidate> candidates, int stationCount)
        {
            Owner = owner; Title = "選擇訓練圖庫"; Width = 760; Height = 620;
            MinWidth = 620; MinHeight = 440; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var layout = new DockPanel { Margin = new Thickness(18) };
            var heading = new TextBlock { Text = $"產品前綴：{product}\n每站選擇一個版本；留空的站別保留原路徑。多個版本不會自動合併。\n僅列出 Origional／Original 站號內有圖片的圖庫，X／Y 不納入。",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
            DockPanel.SetDock(heading, Dock.Top); layout.Children.Add(heading);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var cancel = new Button { Content = "取消", MinWidth = 85, Margin = new Thickness(0, 0, 8, 0), IsCancel = true };
            var apply = new Button { Content = "套用選取站別", MinWidth = 120, IsDefault = true };
            apply.Click += (s, e) => { if (SelectedCandidates.Count == 0) { MessageBox.Show(this, "請至少選擇一個站別版本。", Title); return; } DialogResult = true; };
            buttons.Children.Add(cancel); buttons.Children.Add(apply); DockPanel.SetDock(buttons, Dock.Bottom); layout.Children.Add(buttons);
            var panel = new StackPanel();
            for (int station = 1; station <= stationCount; station++)
            {
                var options = candidates.Where(c => c.Station == station).ToList();
                int candidateCount = options.Count;
                options.Insert(0, new TrainingImageCandidate { Station = station });
                var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var label = new TextBlock { Text = $"第 {station:00} 站", VerticalAlignment = VerticalAlignment.Center };
                var combo = new ComboBox { ItemsSource = options, DisplayMemberPath = "Display", MinHeight = 32,
                    ToolTip = candidateCount == 0 ? "沒有符合的原始圖庫" : "選擇不變更可保留本站現有路徑", IsEnabled = candidateCount > 0 };
                combo.SelectedIndex = candidateCount == 1 ? 1 : 0;
                Grid.SetColumn(combo, 1); row.Children.Add(label); row.Children.Add(combo); panel.Children.Add(row); _choices.Add(station, combo);
            }
            layout.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Content = layout;
        }
    }
}
