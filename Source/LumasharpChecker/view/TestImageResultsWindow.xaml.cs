using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LumasharpChecker.viewmodel;

namespace LumasharpChecker.view
{
    public partial class TestImageResultsWindow : Window
    {
        public TestImageResultsWindow(IEnumerable<TestImageResultRow> results, string thumbnailMode, string statusFilter = "ALL")
        {
            InitializeComponent();
            var filteredResults = statusFilter switch
            {
                "OK" => results.Where(result => result.IsOverallPass),
                "NG" => results.Where(result => !result.IsOverallPass),
                "Retest" => results.Where(result => result.IsRetest),
                _ => results
            };
            Title = statusFilter == "ALL" ? "測試圖片結果" : $"{statusFilter} 檢測結果";
            ResultsTitle.Text = Title;
            DataContext = filteredResults.Select(result => new TestImageResultDisplayRow(result, thumbnailMode)).ToList();
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

    public class TestImageResultDisplayRow
    {
        private readonly string _thumbnailMode;

        public TestImageResultDisplayRow(TestImageResultRow source, string thumbnailMode)
        {
            Source = source;
            _thumbnailMode = thumbnailMode;
        }

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
