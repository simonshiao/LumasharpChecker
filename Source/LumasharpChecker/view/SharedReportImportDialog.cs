using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LumasharpChecker.model;

namespace LumasharpChecker.view
{
    internal sealed class SharedReportImportDialog : Window
    {
        private readonly ListBox _listBox;

        public RecipeManager.SharedReportDayInfo? SelectedDay { get; private set; }

        public SharedReportImportDialog(
            IReadOnlyList<RecipeManager.SharedReportDayInfo> days,
            string title,
            string monthPath)
        {
            Owner = Application.Current.MainWindow;
            Title = title;
            Width = 600;
            Height = 500;
            MinWidth = 450;
            MinHeight = 350;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var layout = new Grid { Margin = new Thickness(18) };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var heading = new TextBlock
            {
                Text = $"來源路徑：{monthPath}\n請選擇要匯入的日期：",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid.SetRow(heading, 0);
            layout.Children.Add(heading);

            _listBox = new ListBox
            {
                Margin = new Thickness(0, 0, 0, 12)
            };

            foreach (var day in days)
            {
                int total = day.Stations.Count;
                int withOk = day.Stations.Count(s => s.HasOkSubfolder);
                int withNg = day.Stations.Count(s => s.HasNgSubfolder);
                int withAll = day.Stations.Count(s => s.HasImages && !s.HasOkSubfolder && !s.HasNgSubfolder);
                var parts = new List<string>();
                parts.Add($"{total} 站有圖");
                if (withOk > 0) parts.Add($"OK {withOk} 站");
                if (withNg > 0) parts.Add($"NG {withNg} 站");
                if (withAll > 0) parts.Add($"ALL {withAll} 站");

                var stack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
                var icon = new TextBlock
                {
                    Text = "\uE769 ", // calendar icon char
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    Foreground = (Brush)Application.Current.FindResource("Blue"),
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var text = new TextBlock
                {
                    Text = $"{day.DayFolder}\n{string.Join(" · ", parts)}",
                    TextWrapping = TextWrapping.Wrap
                };
                stack.Children.Add(icon);
                stack.Children.Add(text);
                _listBox.Items.Add(stack);
            }

            _listBox.SelectionMode = SelectionMode.Single;
            if (_listBox.Items.Count > 0) _listBox.SelectedIndex = 0;

            var scroll = new ScrollViewer
            {
                Content = _listBox,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid.SetRow(scroll, 1);
            layout.Children.Add(scroll);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var cancel = new Button { Content = "取消", MinWidth = 85, Margin = new Thickness(0, 0, 8, 0), IsCancel = true };
            var ok = new Button { Content = "匯入", MinWidth = 85, IsDefault = true };
            ok.Click += (s, e) =>
            {
                if (_listBox.SelectedItem == null) return;
                SelectedDay = days[_listBox.SelectedIndex];
                DialogResult = true;
            };
            buttons.Children.Add(cancel);
            buttons.Children.Add(ok);
            Grid.SetRow(buttons, 2);
            layout.Children.Add(buttons);

            Content = layout;
        }
    }
}
