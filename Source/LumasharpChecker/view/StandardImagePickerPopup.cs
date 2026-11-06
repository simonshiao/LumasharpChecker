using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LumasharpChecker.model;

namespace LumasharpChecker.view
{
    /// <summary>
    /// 批次標準件用的可移動、可縮放圖片視窗。只解碼當頁縮圖，不建立整座圖庫的影像快取。
    /// 選圖只回傳清單索引，不會改寫配方或切換主視窗站別。
    /// </summary>
    internal static class StandardImagePickerPopup
    {
        private static readonly Brush TextColor = BrushFrom("#20232B");
        private static readonly Brush SubtleText = BrushFrom("#747B87");
        private static readonly Brush Accent = BrushFrom("#007AFF");
        private static readonly Brush BorderColor = BrushFrom("#E5E9F0");
        private static readonly Brush CanvasColor = BrushFrom("#F5F6F9");
        private static double _preferredThumbnailWidth = 180;
        private static readonly Dictionary<int, Window> OpenPickers = new();

        internal static Brush BrushFrom(string hex)
            => (Brush)new BrushConverter().ConvertFromString(hex)!;

        internal static Style RoundedButtonStyle()
        {
            var style = new Style(typeof(Button));
            var template = new ControlTemplate(typeof(Button));
            var chrome = new FrameworkElementFactory(typeof(Border));
            chrome.Name = "ButtonChrome";
            chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            chrome.SetValue(Border.BackgroundProperty, new System.Windows.TemplateBindingExtension(Control.BackgroundProperty));
            chrome.SetValue(Border.BorderBrushProperty, new System.Windows.TemplateBindingExtension(Control.BorderBrushProperty));
            chrome.SetValue(Border.BorderThicknessProperty, new System.Windows.TemplateBindingExtension(Control.BorderThicknessProperty));
            chrome.SetValue(Border.PaddingProperty, new System.Windows.TemplateBindingExtension(Control.PaddingProperty));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            chrome.AppendChild(content);
            template.VisualTree = chrome;

            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.42));
            template.Triggers.Add(disabled);
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.83));
            template.Triggers.Add(hover);
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
            return style;
        }

        internal static Button MakeButton(string text, Brush background, Brush foreground, double width, double height)
            => new Button
            {
                Content = text,
                Width = width,
                Height = height,
                Padding = new Thickness(6),
                Background = background,
                Foreground = foreground,
                BorderBrush = BorderColor,
                BorderThickness = new Thickness(background == Brushes.Transparent ? 1 : 0),
                Style = RoundedButtonStyle(),
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };

        internal static BitmapImage? LoadThumbnail(string? path, int decodeWidth = 170)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            try
            {
                return InMemoryImageLoader.Load(path, decodeWidth);
            }
            catch { return null; }
        }

        internal static void Open(Button anchor, IReadOnlyList<string> files,
            int selectedIndex, int station, Action<int> onSelect)
        {
            if (files.Count == 0) return;
            if (OpenPickers.TryGetValue(station, out var existing))
            {
                existing.Activate();
                return;
            }
            var owner = Window.GetWindow(anchor);
            var window = new Window
            {
                Title = $"第 {station:00} 站 · 標準件圖片選擇",
                Width = 950, Height = 735,
                MinWidth = 680, MinHeight = 470,
                ResizeMode = ResizeMode.CanResizeWithGrip,
                WindowStyle = WindowStyle.SingleBorderWindow,
                WindowStartupLocation = owner == null
                    ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
                Background = CanvasColor,
                FontFamily = new FontFamily("Microsoft JhengHei UI")
            };
            if (owner != null) window.Owner = owner;
            OpenPickers[station] = window;
            window.Closed += (_, _) =>
            {
                if (OpenPickers.TryGetValue(station, out var current) && ReferenceEquals(current, window))
                    OpenPickers.Remove(station);
            };

            var layout = new Grid { Margin = new Thickness(22, 18, 22, 16) };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            header.Children.Add(new TextBlock
            {
                Text = $"第 {station:00} 站  ·  選擇標準件圖片",
                FontSize = 21, FontWeight = FontWeights.SemiBold,
                Foreground = TextColor
            });
            header.Children.Add(new TextBlock
            {
                Text = "點選圖片即可帶回批次設定；拖動標題列移動視窗、拉視窗邊緣調整大小。",
                Foreground = SubtleText, FontSize = 12, Margin = new Thickness(0, 5, 0, 0)
            });
            Grid.SetRow(header, 0);
            layout.Children.Add(header);

            var toolbar = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
            var zoom = new Slider
            {
                Minimum = 120, Maximum = 420, TickFrequency = 20,
                IsSnapToTickEnabled = true, Value = _preferredThumbnailWidth,
                Width = 170, VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "拖曳調整圖片縮圖大小"
            };
            var zoomText = new TextBlock
            {
                Foreground = SubtleText, FontSize = 12,
                Width = 45, VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right
            };
            var zoomPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            zoomPanel.Children.Add(new TextBlock
            {
                Text = "縮圖大小", FontSize = 12, Foreground = SubtleText,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0)
            });
            zoomPanel.Children.Add(zoom);
            zoomPanel.Children.Add(zoomText);
            DockPanel.SetDock(zoomPanel, Dock.Right);
            toolbar.Children.Add(zoomPanel);

            var searchBox = new TextBox
            {
                FontSize = 13,
                Height = 36,
                Padding = new Thickness(12, 7, 12, 6),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                ToolTip = "輸入部分檔名即可搜尋"
            };
            var searchBorder = new Border
            {
                Child = searchBox,
                Background = Brushes.White,
                BorderBrush = BorderColor,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 20, 0)
            };
            toolbar.Children.Add(searchBorder);
            Grid.SetRow(toolbar, 1);
            layout.Children.Add(toolbar);

            var tilePanel = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Left };
            var viewport = new ScrollViewer
            {
                Content = tilePanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = Brushes.Transparent
            };
            Grid.SetRow(viewport, 2);
            layout.Children.Add(viewport);

            var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            var pageLabel = new TextBlock { Foreground = SubtleText, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
            DockPanel.SetDock(pageLabel, Dock.Left);
            footer.Children.Add(pageLabel);
            var pager = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var prev = MakeButton("‹ 上一頁", Brushes.White, TextColor, 92, 34);
            var next = MakeButton("下一頁 ›", Brushes.White, TextColor, 92, 34);
            next.Margin = new Thickness(8, 0, 0, 0);
            pager.Children.Add(prev);
            pager.Children.Add(next);
            DockPanel.SetDock(pager, Dock.Right);
            footer.Children.Add(pager);
            Grid.SetRow(footer, 3);
            layout.Children.Add(footer);
            window.Content = layout;

            int page = 0;
            int pageSize = 12;
            int lastColumns = 0;
            int lastRows = 0;
            List<int> matches = Enumerable.Range(0, files.Count).ToList();
            void Render(bool preserveFirstItem = false)
            {
                // 根據目前視窗尺寸與縮圖大小，決定當頁能展示幾張。
                int width = (int)Math.Round(zoom.Value);
                int tileWidth = width + 12;
                int imageHeight = Math.Max(85, (int)(width * 0.68));
                int tileHeight = imageHeight + 45;
                int columns = Math.Max(1, (int)Math.Floor(
                    Math.Max(300, viewport.ActualWidth - 18) / (tileWidth + 8.0)));
                int rows = Math.Max(1, (int)Math.Floor(
                    Math.Max(160, viewport.ActualHeight - 12) / (tileHeight + 8.0)));
                int newPageSize = Math.Max(1, columns * rows);
                if (preserveFirstItem && newPageSize != pageSize)
                    page = (page * pageSize) / newPageSize;
                pageSize = newPageSize;
                lastColumns = columns;
                lastRows = rows;
                int totalPages = Math.Max(1, (matches.Count + pageSize - 1) / pageSize);
                page = Math.Max(0, Math.Min(page, totalPages - 1));
                tilePanel.Children.Clear();
                foreach (int index in matches.Skip(page * pageSize).Take(pageSize))
                {
                    var tile = MakeButton("", Brushes.White, TextColor, width, tileHeight);
                    tile.Margin = new Thickness(4);
                    tile.BorderBrush = index == selectedIndex ? Accent : BorderColor;
                    tile.BorderThickness = new Thickness(index == selectedIndex ? 2 : 1);
                    var body = new StackPanel { Margin = new Thickness(5) };
                    var bitmap = LoadThumbnail(files[index], Math.Min(720, width * 2));
                    body.Children.Add(new Border
                    {
                        Child = new Image
                        {
                            Source = bitmap, Height = imageHeight,
                            Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Stretch
                        },
                        Background = CanvasColor, CornerRadius = new CornerRadius(8),
                        Height = imageHeight
                    });
                    body.Children.Add(new TextBlock
                    {
                        Text = Path.GetFileName(files[index]), FontSize = 11,
                        Foreground = index == selectedIndex ? Accent : TextColor,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        ToolTip = files[index], Margin = new Thickness(0, 6, 0, 0)
                    });
                    tile.Content = body;
                    tile.ToolTip = files[index];
                    tile.IsEnabled = bitmap != null;
                    tile.Click += (_, _) => { window.Close(); onSelect(index); };
                    tilePanel.Children.Add(tile);
                }
                if (matches.Count == 0)
                {
                    tilePanel.Children.Add(new TextBlock
                    {
                        Text = "沒有符合檔名的圖片", Foreground = SubtleText,
                        Margin = new Thickness(18), VerticalAlignment = VerticalAlignment.Center
                    });
                }
                pageLabel.Text = $"符合 {matches.Count} 張  ·  第 {page + 1} / {totalPages} 頁  ·  滾輪翻頁，Ctrl＋滾輪調整縮圖";
                zoomText.Text = $"{width} px";
                prev.IsEnabled = page > 0;
                next.IsEnabled = page < totalPages - 1;
                viewport.ScrollToTop();
            }
            searchBox.TextChanged += (_, _) =>
            {
                string query = searchBox.Text.Trim();
                matches = Enumerable.Range(0, files.Count)
                    .Where(i => Path.GetFileName(files[i]).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                page = 0;
                Render();
            };
            zoom.ValueChanged += (_, _) =>
            {
                _preferredThumbnailWidth = zoom.Value;
                Render(preserveFirstItem: true);
            };
            prev.Click += (_, _) => { if (page > 0) { page--; Render(); } };
            next.Click += (_, _) => { page++; Render(); };
            window.PreviewMouseWheel += (_, e) =>
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    zoom.Value = Math.Max(zoom.Minimum, Math.Min(zoom.Maximum,
                        zoom.Value + (e.Delta > 0 ? 20 : -20)));
                }
                else
                {
                    if (e.Delta < 0) page++;
                    else if (e.Delta > 0) page--;
                    Render();
                }
                e.Handled = true;
            };
            window.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) { window.Close(); e.Handled = true; }
                else if (e.Key == Key.PageDown) { page++; Render(); e.Handled = true; }
                else if (e.Key == Key.PageUp) { page--; Render(); e.Handled = true; }
            };
            viewport.SizeChanged += (_, _) =>
            {
                if (viewport.ActualWidth <= 0 || viewport.ActualHeight <= 0) return;
                int cols = Math.Max(1, (int)Math.Floor(
                    Math.Max(300, viewport.ActualWidth - 18) / (zoom.Value + 20)));
                int rows = Math.Max(1, (int)Math.Floor(
                    Math.Max(160, viewport.ActualHeight - 12) /
                    (Math.Max(85, zoom.Value * 0.68) + 53)));
                if (cols != lastColumns || rows != lastRows)
                    Render(preserveFirstItem: true);
            };
            window.Loaded += (_, _) =>
            {
                int initialPos = matches.IndexOf(selectedIndex);
                if (initialPos >= 0) page = initialPos / pageSize;
                Render();
                searchBox.Focus();
            };
            window.Show();
        }
    }
}
