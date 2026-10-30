using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LumasharpChecker.model;
using Microsoft.Win32;

namespace LumasharpChecker.view
{
    /// <summary>每站正反面模板管理。採用現有離線圖像；Slim 不連接實體相機。</summary>
    public sealed class StationPatternsWindow : Window
    {
        private readonly StationPatternLibrary _library;
        private readonly string? _currentImagePath;
        private readonly ListBox _list;
        private readonly Image _preview;
        private readonly TextBlock _count;
        private readonly Button _remove;
        private readonly TextBlock _message;
        public bool Modified { get; private set; }

        public StationPatternsWindow(Window owner, int station, StationPatternLibrary library, string? currentImagePath)
        {
            Owner = owner;
            _library = library;
            _currentImagePath = currentImagePath;
            Title = $"站 {station} · 定位樣板";
            Width = 780;
            Height = 530;
            MinWidth = 650;
            MinHeight = 440;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Microsoft JhengHei UI");
            FontSize = 13;
            Background = new SolidColorBrush(Color.FromRgb(240, 248, 252));

            var grid = new Grid { Margin = new Thickness(18) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = grid;

            var heading = new DockPanel { Margin = new Thickness(2, 0, 2, 12) };
            Grid.SetRow(heading, 0);
            grid.Children.Add(heading);
            _count = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.SlateGray };
            DockPanel.SetDock(_count, Dock.Right);
            heading.Children.Add(_count);
            heading.Children.Add(new TextBlock { Text = "定位樣板", FontSize = 21, FontWeight = FontWeights.SemiBold });

            var body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(235) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(body, 1);
            grid.Children.Add(body);
            _list = new ListBox { Margin = new Thickness(0, 0, 10, 0), BorderThickness = new Thickness(1) };
            AutomationProperties.SetName(_list, "定位樣板清單");
            _list.SelectionChanged += (_, __) => UpdateSelection();
            body.Children.Add(_list);

            var previewPanel = new Grid { Background = new SolidColorBrush(Color.FromRgb(10, 33, 47)) };
            Grid.SetColumn(previewPanel, 1);
            body.Children.Add(previewPanel);
            _preview = new Image { Stretch = Stretch.Uniform, Margin = new Thickness(8) };
            previewPanel.Children.Add(_preview);

            var footer = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(footer, 2);
            grid.Children.Add(footer);
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            footer.Children.Add(actions);
            var add = CreateButton("加入圖片…", "從檔案總管選一張或多張正常姿態照片");
            add.Click += (_, __) => SelectAndAdd();
            actions.Children.Add(add);
            var current = CreateButton("加入目前圖片", "以單站目前顯示的圖片作為新定位樣板");
            current.Margin = new Thickness(7, 0, 0, 0);
            current.IsEnabled = !string.IsNullOrWhiteSpace(_currentImagePath) && File.Exists(_currentImagePath);
            current.Click += (_, __) => AddSelected(new[] { _currentImagePath! });
            actions.Children.Add(current);
            _remove = CreateButton("移除樣板", "只能刪除額外加入的樣板，不會刪除原始樣板");
            _remove.Margin = new Thickness(7, 0, 0, 0);
            _remove.Click += (_, __) => RemoveSelected();
            actions.Children.Add(_remove);
            var done = CreateButton("完成", "關閉定位樣板管理");
            done.HorizontalAlignment = HorizontalAlignment.Right;
            done.Margin = new Thickness(12, 0, 0, 0);
            done.Click += (_, __) => Close();
            var buttons = new DockPanel();
            DockPanel.SetDock(done, Dock.Right);
            buttons.Children.Add(done);
            footer.Children.Remove(actions);
            buttons.Children.Add(actions);
            footer.Children.Add(buttons);
            _message = new TextBlock
            {
                Text = "使用同尺寸照片，定位框和亮度框沿用本站設定；反面亮度差異需另外校準。",
                Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap,
                FontSize = 11, Margin = new Thickness(2, 8, 0, 0)
            };
            Grid.SetRow(_message, 1);
            footer.Children.Add(_message);
            Refresh();
        }

        private static Button CreateButton(string caption, string tooltip)
        {
            var button = new Button { Content = caption, Padding = new Thickness(12, 7, 12, 7),
                MinHeight = 34, ToolTip = tooltip };
            AutomationProperties.SetName(button, caption);
            return button;
        }

        private void SelectAndAdd()
        {
            var dialog = new OpenFileDialog
            {
                Title = "加入正面／反面定位樣板（可多選）",
                Filter = "圖片|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = true
            };
            if (dialog.ShowDialog(this) == true) AddSelected(dialog.FileNames);
        }

        private void AddSelected(IEnumerable<string> paths)
        {
            try
            {
                int added = _library.AddFiles(paths);
                if (added > 0) Modified = true;
                _message.Text = added > 0 ? $"已加入 {added} 張定位樣板。" : "未選擇圖片。";
                Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "樣板無法加入", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RemoveSelected()
        {
            if (_list.SelectedItem is not Item item || item.Primary) return;
            if (MessageBox.Show(this, $"移除 {item.Label}？", "確認移除",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                _library.Remove(item.Path);
                Modified = true;
                _message.Text = "已移除附加樣板。";
                Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "樣板無法移除", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Refresh()
        {
            var entries = new List<Item> { new Item("1 · 主樣板", _library.PrimaryPath, true) };
            int index = 2;
            foreach (string file in _library.AdditionalPaths)
                entries.Add(new Item($"{index++} · {Path.GetFileName(file)}", file, false));
            _list.ItemsSource = entries;
            _list.DisplayMemberPath = nameof(Item.Label);
            _list.SelectedIndex = 0;
            _count.Text = $"{entries.Count} / {StationPatternLibrary.MaxTemplates} 張";
            UpdateSelection();
        }

        private void UpdateSelection()
        {
            if (_list.SelectedItem is not Item item)
            {
                _remove.IsEnabled = false;
                _preview.Source = null;
                return;
            }
            _remove.IsEnabled = !item.Primary;
            try
            {
                var source = new BitmapImage();
                source.BeginInit();
                source.UriSource = new Uri(item.Path);
                source.CacheOption = BitmapCacheOption.OnLoad;
                source.DecodePixelWidth = 640;
                source.EndInit();
                source.Freeze();
                _preview.Source = source;
            }
            catch { _preview.Source = null; }
        }

        private sealed class Item
        {
            public string Label { get; }
            public string Path { get; }
            public bool Primary { get; }
            public Item(string label, string path, bool primary)
            {
                Label = label; Path = path; Primary = primary;
            }
        }
    }
}
