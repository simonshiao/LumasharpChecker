using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LumasharpChecker.viewmodel;

namespace LumasharpChecker.view
{
    public partial class TestImageResultsWindow : Window
    {
        private readonly List<TestImageResultDisplayRow> _rows;
        private ListCollectionView? _view;
        public TestImageResultsWindow(
            IEnumerable<(int StationNumber, TestImageResultRow Result)> results,
            string thumbnailMode,
            string statusFilter = "ALL",
            bool allStations = false)
        {
            InitializeComponent();
            var allResults = results.ToArray();
            int stationCount = allResults.Select(item => item.StationNumber).Distinct().Count();
            Title = allStations
                ? $"全部站別檢測結果（{stationCount} 站）"
                : statusFilter == "ALL" ? "測試圖片結果" : $"{statusFilter} 檢測結果";
            ResultsTitle.Text = Title;
            _rows = allResults.Select(item => new TestImageResultDisplayRow(item.StationNumber, item.Result, BatchPreviewModes.All)).ToList();
            _view = new ListCollectionView(_rows);
            ResultsGrid.ItemsSource = _view;
            ResultFilter.SelectedValue = statusFilter is "OK" or "NG" or "Retest" ? statusFilter : "ALL";
            ShowThumbnails.IsChecked = thumbnailMode != BatchPreviewModes.None;
            ApplyOptions();
        }

        private void ResultOptions_Changed(object sender, SelectionChangedEventArgs e) => ApplyOptions();
        private void ThumbnailOptions_Changed(object sender, RoutedEventArgs e) => ApplyOptions();

        private void ApplyOptions()
        {
            if (_view == null) return;
            string filter = ResultFilter.SelectedValue as string ?? "ALL";
            _view.Filter = value => value is TestImageResultDisplayRow row && (filter switch
            {
                "OK" => row.Source.IsOverallPass,
                "NG" => !row.Source.IsOverallPass,
                "Retest" => row.Source.IsRetest,
                _ => true
            });
            bool thumbnails = ShowThumbnails.IsChecked == true;
            foreach (var row in _rows) row.SetThumbnailMode(thumbnails ? BatchPreviewModes.All : BatchPreviewModes.None);
            ThumbnailColumn.Visibility = thumbnails ? Visibility.Visible : Visibility.Collapsed;
            ResultsGrid.RowHeight = thumbnails ? 78 : 32;
            ResultCount.Text = _view.Count == 0 ? $"沒有符合結果（共 {_rows.Count} 張）" : $"顯示 {_view.Count}／{_rows.Count} 張";
        }

        private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsGrid.SelectedItem is not TestImageResultDisplayRow row)
            {
                return;
            }

            var window = new ResultImageDetailWindow(row.Source)
            {
                Owner = this
            };
            window.Show();
        }
    }

    public class TestImageResultDisplayRow : INotifyPropertyChanged
    {
        private string _thumbnailMode;
        public event PropertyChangedEventHandler? PropertyChanged;
        public void SetThumbnailMode(string mode)
        {
            if (_thumbnailMode == mode) return;
            _thumbnailMode = mode;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbnailPath)));
        }

        public TestImageResultDisplayRow(int stationNumber, TestImageResultRow source, string thumbnailMode)
        {
            StationNumber = stationNumber;
            Source = source;
            _thumbnailMode = thumbnailMode;
        }

        public int StationNumber { get; }
        public string StationDisplay => $"第 {StationNumber:00} 站";
        public TestImageResultRow Source { get; }
        public string ThumbnailPath
        {
            get
            {
                if (_thumbnailMode == BatchPreviewModes.None)
                {
                    return string.Empty;
                }

                if (_thumbnailMode == BatchPreviewModes.NgOnly && Source.IsOverallPass)
                {
                    return string.Empty;
                }

                return Source.FilePath;
            }
        }
        public string FileName => Source.FileName;
        public string BrightnessDisplay => Source.BrightnessMeasured ? Source.Brightness.ToString("F2") : "—";
        public string MinimumDisplay => !Source.LightInspectionSkipped && Source.BrightnessMin.HasValue ? Source.BrightnessMin.Value.ToString("F2") : "—";
        public string MaximumDisplay => !Source.LightInspectionSkipped && Source.BrightnessMax.HasValue ? Source.BrightnessMax.Value.ToString("F2") : "—";
        public string Result => Source.OverallResult;
        public string Message => Source.IsRetest ? Source.Message : string.Empty;
    }

    public class DeferredThumbnailConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string path || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
                bitmap.DecodePixelWidth = 140;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class ResultStatusBrushConverter : IValueConverter
    {
        private static readonly Brush OkBrush = new SolidColorBrush(Color.FromRgb(46, 125, 50));
        private static readonly Brush NgBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40));
        private static readonly Brush RetestBrush = new SolidColorBrush(Color.FromRgb(230, 126, 34));
        private static readonly Brush NeutralBrush = new SolidColorBrush(Color.FromRgb(96, 125, 139));

        static ResultStatusBrushConverter()
        {
            OkBrush.Freeze();
            NgBrush.Freeze();
            RetestBrush.Freeze();
            NeutralBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value?.ToString()?.Trim().ToUpperInvariant() ?? string.Empty;
            return status switch
            {
                "OK" => OkBrush,
                "NG" => NgBrush,
                "RETEST" => RetestBrush,
                _ => NeutralBrush
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
