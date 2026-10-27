using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LumasharpChecker.model
{
    // 定義數據結構的類層次
    public class StatisticalResult
    {
        public StatisticalRange? Bootstrap { get; set; }
        public StatisticalRange? Percentile { get; set; }
        public StatisticalRange? IQR { get; set; }
        public double Max { get; set; }
        public double Min { get; set; }
        public double Mean { get; set; }         // 平均值
        public double Sigma { get; set; }        // 標準差 (σ)
        public double ThreeSigmaLower { get; set; }  // μ - 3σ
        public double ThreeSigmaUpper { get; set; } // μ + 3σ

    }

    public class StatisticalRange
    {
        public double Lower { get; set; }
        public double Upper { get; set; }
    }

    public class AnalysisResult
    {
        public StatisticalResult? Brightness { get; set; }
        public StatisticalResult? Sharpness { get; set; }
        public List<double>? BrightnessValues { get; set; }
        public List<double>? SharpnessValues { get; set; }
        public int DynamicThresholdCount { get; set; }
        public bool EnableDynamicThreshold { get; set; }
    }

    public class StatisticalCalculator
    {
        private readonly Random _random = new Random();
        private const int BootstrapIterations = 1000;
        private const double ConfidenceLevel = 0.95;

        public AnalysisResult CalculateStatisticalRanges(
            double[] brightnessValues,
            double[] sharpnessValues)
        {
            ValidateInput(brightnessValues, sharpnessValues);

            return new AnalysisResult
            {
                Brightness = CalculateForMetric(brightnessValues),
                Sharpness = CalculateForMetric(sharpnessValues)
            };
        }

        private StatisticalResult CalculateForMetric(double[] values)
        {
            double mean = CalculateMean(values);
            double sigma = CalculateSigma(values);
            return new StatisticalResult
            {
                Bootstrap = CalculateBootstrapCI(values),
                Percentile = CalculatePercentileRange(values),
                IQR = CalculateIqrRange(values),
                Max = values.Max(),
                Min = values.Min(),
                Mean = mean,
                Sigma = sigma,
                ThreeSigmaLower  = mean - 3 * sigma,
                ThreeSigmaUpper  = mean + 3 * sigma
            };
        }

        // Bootstrap 置信區間實現
        private StatisticalRange CalculateBootstrapCI(double[] originalData)
        {
            var bootstrapStats = new List<double>();
            int n = originalData.Length;

            for (int i = 0; i < BootstrapIterations; i++)
            {
                var sample = new double[n];
                for (int j = 0; j < n; j++)
                {
                    sample[j] = originalData[_random.Next(n)];
                }
                bootstrapStats.Add(sample.Average());
            }

            double alpha = (1 - ConfidenceLevel) / 2;
            return new StatisticalRange
            {
                Lower = Percentile(bootstrapStats, alpha * 100),
                Upper = Percentile(bootstrapStats, (1 - alpha) * 100)
            };
        }

        // 百分位數範圍計算
        private StatisticalRange CalculatePercentileRange(double[] data)
        {
            return new StatisticalRange
            {
                Lower = Percentile(data, 2.5),
                Upper = Percentile(data, 97.5)
            };
        }

        // 四分位距範圍計算
        private StatisticalRange CalculateIqrRange(double[] data)
        {
            var sorted = data.OrderBy(x => x).ToArray();
            double q1 = Percentile(sorted, 25);
            double q3 = Percentile(sorted, 75);
            return new StatisticalRange
            {
                Lower = q1 - 1.5 * (q3 - q1),
                Upper = q3 + 1.5 * (q3 - q1)
            };
        }

        // 通用百分位數計算方法 (線性插值)
        private double Percentile(IEnumerable<double> data, double percentile)
        {
            var sorted = data.OrderBy(x => x).ToArray();
            int N = sorted.Length;
            double index = (N - 1) * percentile / 100;

            if (Math.Abs(index % 1) < double.Epsilon)
                return sorted[(int)index];

            int lower = (int)Math.Floor(index);
            int upper = (int)Math.Ceiling(index);
            double fraction = index - lower;

            return sorted[lower] + fraction * (sorted[upper] - sorted[lower]);
        }
        private double CalculateMean(double[] values)
        {
            return values.Average();
        }

        private double CalculateSigma(double[] values)
        {
            double mean = values.Average();
            double sumSquares = values.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumSquares / values.Length); // 標準差公式
        }

        private void ValidateInput(double[] brightness, double[] sharpness)
        {
            if (brightness == null || sharpness == null)
                throw new ArgumentNullException("輸入數組不能為null");

            if (brightness.Length < 1 || sharpness.Length < 1)
                throw new ArgumentException("數據點數量需至少1個");

            if (brightness.Any(x => double.IsNaN(x)) ||
                sharpness.Any(x => double.IsNaN(x)))
                throw new ArgumentException("數據包含無效數值");
        }
    }

    // 使用示例
    public class Program
    {
        public static void Mainzzz()
        {
            Random _random = new Random();
            var calculator = new StatisticalCalculator();

            double[] brightness = Enumerable.Range(0, 100)
                .Select(_ => _random.NextDouble() * 100).ToArray();

            double[] sharpness = Enumerable.Range(0, 100)
                .Select(_ => _random.NextDouble() * 50).ToArray();

            var result = calculator.CalculateStatisticalRanges(brightness, sharpness);

            // 輸出結果
            PrintResult("Brightness", result.Brightness);
            PrintResult("Sharpness", result.Sharpness);
        }

        private static void PrintResult(string metricName, StatisticalResult result)
        {
            Console.WriteLine($"{metricName} Ranges:");
            Console.WriteLine($"Bootstrap CI: [{result.Bootstrap.Lower:F2}, {result.Bootstrap.Upper:F2}]");
            Console.WriteLine($"Percentile Range: [{result.Percentile.Lower:F2}, {result.Percentile.Upper:F2}]");
            Console.WriteLine($"IQR Range: [{result.IQR.Lower:F2}, {result.IQR.Upper:F2}]");
            Console.WriteLine();
        }
    }
}
