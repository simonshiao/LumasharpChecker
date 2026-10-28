using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using LumasharpChecker.model;
using LiveChartsCore.SkiaSharpView.WPF;
using System.Windows;

namespace LumasharpChecker.viewmodel
{
    public partial class StatisticResultModel : ObservableObject
    {
        [ObservableProperty]
        private bool useMaxMinRange = false; // 預設使用 ±3σ
        [ObservableProperty]
        private bool enableDynamicThreshold = true;  // 預設啟用
        [ObservableProperty]
        private int dynamicThresholdCount = 30;      // 預設 30 次
        [ObservableProperty]
        model.AnalysisResult? _analysisResult;
        public void Initialize(model.AnalysisResult analysisResult)
        {
            AnalysisResult = analysisResult;
            CartesianChart cartesianChart = new CartesianChart();
            var xx = cartesianChart.YAxes.ToList();

            ISeries[] Series = [
                new ScatterSeries<ObservablePoint>
                {
                    GeometrySize = 4,
                    Stroke = new SolidColorPaint { Color = SKColors.Blue, StrokeThickness = 1 },
                    Fill = null,
                    Values = analysisResult?.BrightnessValues?.ToList()
                    .Select((y, index) => new ObservablePoint(index + 1, y)) // x = index + 1
                    .ToArray()
                }
            ];
            

            S1 = Series.ToArray();

            // X轴配置 ‌:ml-citation{ref="6" data="citationList"}
            Se1 = new[]
            {
                new RectangularSection
                {
                    Yi = analysisResult?.Brightness?.Min,
                    Yj = analysisResult?.Brightness?.Min,
                    Stroke = new SolidColorPaint
                    {
                        Color = SKColors.Red,
                        StrokeThickness = 3,
                        PathEffect = new DashEffect([6, 6])
                    }
                },
                new RectangularSection
                {
                    Yi = analysisResult?.Brightness?.Max,
                    Yj = analysisResult?.Brightness?.Max,
                    Stroke = new SolidColorPaint
                    {
                        Color = SKColors.Red,
                        StrokeThickness = 3,
                        PathEffect = new DashEffect([6, 6])
                    }
                },
            };

            YAxes = new[]
            {
                new Axis
                {
                    MinLimit = 0,
                    MaxLimit = 255,
                    Labeler = value => $"{value}"
                }
            };


            ISeries[] Series2 = [
                new ScatterSeries<ObservablePoint>
                {
                    GeometrySize = 4,
                    Stroke = new SolidColorPaint { Color = SKColors.Blue, StrokeThickness = 1 },
                    Fill = null,
                    Values = analysisResult?.SharpnessValues?.ToList()
                    .Select((y, index) => new ObservablePoint(index + 1, y)) // x = index + 1
                    .ToArray()
                }
            ];

            S2 = Series2.ToArray();


            // X轴配置 ‌:ml-citation{ref="6" data="citationList"}
            Se = new[]
            {
                new RectangularSection
                {
                    Yi = analysisResult?.Sharpness?.Min,
                    Yj = analysisResult?.Sharpness?.Min,
                    Stroke = new SolidColorPaint
                    {
                        Color = SKColors.Red,
                        StrokeThickness = 3,
                        PathEffect = new DashEffect([6, 6])
                    }
                },
            };
        }
 

        [RelayCommand]
        private void Save()
        {

        }
        [ObservableProperty]
        private ISeries[]? _s1;

        [ObservableProperty]
        private RectangularSection[]? _se1;

        [ObservableProperty]
        public Axis[]? yAxes;

        [ObservableProperty]
        private ISeries[]? _s2;

        [ObservableProperty]
        private RectangularSection[]? _se;

    }
}
