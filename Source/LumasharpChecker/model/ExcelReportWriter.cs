using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;
using LumasharpChecker.viewmodel;

namespace LumasharpChecker.model
{
    public static class ExcelReportWriter
    {
        public static void WriteTestResults(
            string filePath,
            IEnumerable<string[]> specRows,
            IEnumerable<TestImageResultRow> results,
            bool includeSharpness,
            double brightnessLowerLimit,
            double brightnessUpperLimit,
            double sharpnessLowerLimit)
        {
            var resultRows = results.ToList();
            var sheets = new List<WorksheetData>
            {
                new("檢測規格", specRows.ToList(), new List<ChartData>()),
                new("總表", BuildSummaryRows(resultRows).ToList(), new List<ChartData>()),
                BuildMetricSheet(
                    "亮度",
                    resultRows.Select((result, index) => new MetricPoint(index + 1, result.FileName, result.Brightness, result.BrightnessSafe, result.BrightnessDeltaPercent)).ToList(),
                    brightnessLowerLimit,
                    brightnessUpperLimit),
                includeSharpness
                    ? BuildMetricSheet(
                        "銳利度",
                        resultRows.Select((result, index) => new MetricPoint(index + 1, result.FileName, result.Sharpness, result.SharpnessSafe, null)).ToList(),
                        sharpnessLowerLimit,
                        null)
                    : new("銳利度", new List<string[]> { new[] { "亮度模式未檢測銳利度" } }, new List<ChartData>())
            };

            using var archive = ZipFile.Open(filePath, ZipArchiveMode.Create);
            int drawingCount = sheets.Count(sheet => sheet.Charts.Count > 0);
            int chartCount = sheets.Sum(sheet => sheet.Charts.Count);

            AddText(archive, "[Content_Types].xml", BuildContentTypes(sheets.Count, drawingCount, chartCount));
            AddText(archive, "_rels/.rels", BuildPackageRelationships());
            AddText(archive, "xl/workbook.xml", BuildWorkbook(sheets));
            AddText(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships(sheets.Count));

            int drawingIndex = 1;
            int chartIndex = 1;
            for (int i = 0; i < sheets.Count; i++)
            {
                var sheet = sheets[i];
                bool hasCharts = sheet.Charts.Count > 0;
                AddText(archive, $"xl/worksheets/sheet{i + 1}.xml", BuildWorksheet(sheet.Rows, hasCharts));
                if (!hasCharts)
                {
                    continue;
                }

                var chartIndices = Enumerable.Range(chartIndex, sheet.Charts.Count).ToList();
                AddText(archive, $"xl/worksheets/_rels/sheet{i + 1}.xml.rels", BuildWorksheetRelationship(drawingIndex));
                AddText(archive, $"xl/drawings/drawing{drawingIndex}.xml", BuildDrawing(sheet.Charts, chartIndices));
                AddText(archive, $"xl/drawings/_rels/drawing{drawingIndex}.xml.rels", BuildDrawingRelationships(chartIndices));

                for (int chartOffset = 0; chartOffset < sheet.Charts.Count; chartOffset++)
                {
                    AddText(archive, $"xl/charts/chart{chartIndex}.xml", BuildChart(sheet.Charts[chartOffset]));
                    chartIndex++;
                }

                drawingIndex++;
            }
        }

        private static IEnumerable<string[]> BuildSummaryRows(IEnumerable<TestImageResultRow> results)
        {
            yield return new[] { "檔名", "完整路徑", "亮度", "亮度差異(%)", "銳利度", "亮度合格", "銳利度合格", "總判定", "樣式匹配", "亮度ROI_X", "亮度ROI_Y", "亮度ROI_W", "亮度ROI_H", "銳利度ROI_X", "銳利度ROI_Y", "銳利度ROI_W", "銳利度ROI_H", "備註" };
            foreach (var result in results)
            {
                yield return new[]
                {
                    result.FileName,
                    result.FilePath,
                    FormatNumber(result.Brightness),
                    FormatPercent(result.BrightnessDeltaPercent),
                    result.IncludeSharpness ? FormatNumber(result.Sharpness) : "N/A",
                    result.BrightnessResult,
                    result.SharpnessResult,
                    result.OverallResult,
                    result.MatchResult,
                    result.BrightnessRoiX.ToString(CultureInfo.InvariantCulture),
                    result.BrightnessRoiY.ToString(CultureInfo.InvariantCulture),
                    result.BrightnessRoiW.ToString(CultureInfo.InvariantCulture),
                    result.BrightnessRoiH.ToString(CultureInfo.InvariantCulture),
                    result.SharpnessRoiX.ToString(CultureInfo.InvariantCulture),
                    result.SharpnessRoiY.ToString(CultureInfo.InvariantCulture),
                    result.SharpnessRoiW.ToString(CultureInfo.InvariantCulture),
                    result.SharpnessRoiH.ToString(CultureInfo.InvariantCulture),
                    result.Message
                };
            }
        }

        private static WorksheetData BuildMetricSheet(string metricName, IReadOnlyList<MetricPoint> points, double lowerLimit, double? upperLimit)
        {
            var values = points.Select(point => point.Value).ToList();
            var histogram = BuildHistogram(values, 12);
            bool hasDeltaPercent = points.Any(point => point.DeltaPercent.HasValue);
            var rows = new List<string[]>
            {
                new[] { $"{metricName}圖表" },
                new[] { "規格下限", FormatNumber(lowerLimit) },
                new[] { "規格上限", upperLimit.HasValue ? FormatNumber(upperLimit.Value) : "N/A" },
                Array.Empty<string>(),
                new[] { "分布區間", "張數" }
            };

            rows.AddRange(histogram.Select(bin => new[] { bin.Label, bin.Count.ToString(CultureInfo.InvariantCulture) }));
            rows.Add(Array.Empty<string>());
            rows.Add(hasDeltaPercent
                ? new[] { "圖片序號", "檔名", metricName, "亮度差異(%)", "判定", "規格下限", "規格上限" }
                : new[] { "圖片序號", "檔名", metricName, "判定", "規格下限", "規格上限" });
            foreach (var point in points)
            {
                if (hasDeltaPercent)
                {
                    rows.Add(new[]
                    {
                        point.Index.ToString(CultureInfo.InvariantCulture),
                        point.FileName,
                        FormatNumber(point.Value),
                        point.DeltaPercent.HasValue ? FormatPercent(point.DeltaPercent.Value) : "N/A",
                        point.IsPass ? "OK" : "NG",
                        FormatNumber(lowerLimit),
                        upperLimit.HasValue ? FormatNumber(upperLimit.Value) : "N/A"
                    });
                }
                else
                {
                    rows.Add(new[]
                    {
                        point.Index.ToString(CultureInfo.InvariantCulture),
                        point.FileName,
                        FormatNumber(point.Value),
                        point.IsPass ? "OK" : "NG",
                        FormatNumber(lowerLimit),
                        upperLimit.HasValue ? FormatNumber(upperLimit.Value) : "N/A"
                    });
                }
            }

            var charts = new List<ChartData>
            {
                ChartData.Bar($"{metricName}分布圖", histogram.Select(bin => bin.Label).ToList(), histogram.Select(bin => bin.Count).ToList(), ChartAnchor.Top),
                ChartData.Scatter($"{metricName}點檢結果圖", points, lowerLimit, upperLimit, ChartAnchor.Bottom)
            };

            return new WorksheetData(metricName, rows, charts);
        }

        private static List<HistogramBin> BuildHistogram(IReadOnlyList<double> values, int binCount)
        {
            if (values.Count == 0)
            {
                return new List<HistogramBin> { new("-", 0) };
            }

            double min = values.Min();
            double max = values.Max();
            if (Math.Abs(max - min) < double.Epsilon)
            {
                return new List<HistogramBin> { new($"{min:F2}", values.Count) };
            }

            double width = (max - min) / binCount;
            var bins = Enumerable.Range(0, binCount)
                .Select(index =>
                {
                    double lower = min + index * width;
                    double upper = index == binCount - 1 ? max : lower + width;
                    return new HistogramBin($"{lower:F2} ~ {upper:F2}", 0);
                })
                .ToList();

            foreach (double value in values)
            {
                int index = Math.Min(binCount - 1, (int)((value - min) / width));
                bins[index] = bins[index] with { Count = bins[index].Count + 1 };
            }

            return bins;
        }

        private static string BuildContentTypes(int sheetCount, int drawingCount, int chartCount)
        {
            var sb = new StringBuilder();
            sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
            sb.Append("""<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">""");
            sb.Append("""<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>""");
            sb.Append("""<Default Extension="xml" ContentType="application/xml"/>""");
            sb.Append("""<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>""");
            for (int i = 1; i <= sheetCount; i++)
            {
                sb.Append($"""<Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""");
            }
            for (int i = 1; i <= drawingCount; i++)
            {
                sb.Append($"""<Override PartName="/xl/drawings/drawing{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.drawing+xml"/>""");
            }
            for (int i = 1; i <= chartCount; i++)
            {
                sb.Append($"""<Override PartName="/xl/charts/chart{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.drawingml.chart+xml"/>""");
            }
            sb.Append("</Types>");
            return sb.ToString();
        }

        private static string BuildPackageRelationships()
        {
            return """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""";
        }

        private static string BuildWorkbook(IReadOnlyList<WorksheetData> sheets)
        {
            var sb = new StringBuilder();
            sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
            sb.Append("""<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>""");
            for (int i = 0; i < sheets.Count; i++)
            {
                sb.Append($"""<sheet name="{Escape(sheets[i].Name)}" sheetId="{i + 1}" r:id="rId{i + 1}"/>""");
            }
            sb.Append("</sheets></workbook>");
            return sb.ToString();
        }

        private static string BuildWorkbookRelationships(int sheetCount)
        {
            var sb = new StringBuilder();
            sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
            sb.Append("""<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""");
            for (int i = 1; i <= sheetCount; i++)
            {
                sb.Append($"""<Relationship Id="rId{i}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{i}.xml"/>""");
            }
            sb.Append("</Relationships>");
            return sb.ToString();
        }

        private static string BuildWorksheet(IEnumerable<string[]> rows, bool hasCharts)
        {
            var sb = new StringBuilder();
            sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
            sb.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheetData>""");
            int rowIndex = 1;
            foreach (var row in rows)
            {
                sb.Append($"""<row r="{rowIndex}">""");
                foreach (var cell in row)
                {
                    sb.Append($"""<c t="inlineStr"><is><t>{Escape(cell)}</t></is></c>""");
                }
                sb.Append("</row>");
                rowIndex++;
            }
            sb.Append("</sheetData>");
            if (hasCharts)
            {
                sb.Append("""<drawing r:id="rId1"/>""");
            }
            sb.Append("</worksheet>");
            return sb.ToString();
        }

        private static string BuildWorksheetRelationship(int drawingIndex)
        {
            return $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing" Target="../drawings/drawing{drawingIndex}.xml"/></Relationships>""";
        }

        private static string BuildDrawing(IReadOnlyList<ChartData> charts, IReadOnlyList<int> chartIndices)
        {
            var sb = new StringBuilder();
            sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><xdr:wsDr xmlns:xdr="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">""");
            for (int i = 0; i < charts.Count; i++)
            {
                var anchor = charts[i].Anchor;
                int fromRow = anchor == ChartAnchor.Top ? 1 : 24;
                int toRow = anchor == ChartAnchor.Top ? 21 : 49;
                int relId = i + 1;
                sb.Append($"""<xdr:twoCellAnchor><xdr:from><xdr:col>3</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>{fromRow}</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from><xdr:to><xdr:col>18</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>{toRow}</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:to><xdr:graphicFrame macro=""><xdr:nvGraphicFramePr><xdr:cNvPr id="{chartIndices[i] + 1}" name="Chart {chartIndices[i]}"/><xdr:cNvGraphicFramePr/></xdr:nvGraphicFramePr><xdr:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/></xdr:xfrm><a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/chart"><c:chart xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" r:id="rId{relId}"/></a:graphicData></a:graphic></xdr:graphicFrame><xdr:clientData/></xdr:twoCellAnchor>""");
            }
            sb.Append("</xdr:wsDr>");
            return sb.ToString();
        }

        private static string BuildDrawingRelationships(IReadOnlyList<int> chartIndices)
        {
            var sb = new StringBuilder();
            sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""");
            for (int i = 0; i < chartIndices.Count; i++)
            {
                sb.Append($"""<Relationship Id="rId{i + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart" Target="../charts/chart{chartIndices[i]}.xml"/>""");
            }
            sb.Append("</Relationships>");
            return sb.ToString();
        }

        private static string BuildChart(ChartData chart)
        {
            return chart.Kind == ChartKind.Bar ? BuildBarChart(chart) : BuildScatterChart(chart);
        }

        private static string BuildBarChart(ChartData chart)
        {
            const int categoryAxisId = 48650112;
            const int valueAxisId = 48672768;
            var categories = BuildStringLiteral(chart.Categories);
            var values = BuildNumberLiteral(chart.Counts);
            return $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><c:chartSpace xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><c:chart><c:title><c:tx><c:rich><a:bodyPr/><a:lstStyle/><a:p><a:r><a:t>{Escape(chart.Title)}</a:t></a:r></a:p></c:rich></c:tx><c:layout/></c:title><c:plotArea><c:layout/><c:barChart><c:barDir val="col"/><c:grouping val="clustered"/><c:ser><c:idx val="0"/><c:order val="0"/><c:tx><c:v>張數</c:v></c:tx><c:cat><c:strLit>{categories}</c:strLit></c:cat><c:val><c:numLit>{values}</c:numLit></c:val></c:ser><c:axId val="{categoryAxisId}"/><c:axId val="{valueAxisId}"/></c:barChart><c:catAx><c:axId val="{categoryAxisId}"/><c:scaling><c:orientation val="minMax"/></c:scaling><c:axPos val="b"/><c:tickLblPos val="nextTo"/><c:crossAx val="{valueAxisId}"/><c:crosses val="autoZero"/></c:catAx><c:valAx><c:axId val="{valueAxisId}"/><c:scaling><c:orientation val="minMax"/></c:scaling><c:axPos val="l"/><c:majorGridlines/><c:tickLblPos val="nextTo"/><c:crossAx val="{categoryAxisId}"/><c:crosses val="autoZero"/></c:valAx></c:plotArea><c:legend><c:legendPos val="r"/><c:layout/></c:legend><c:plotVisOnly val="1"/></c:chart></c:chartSpace>""";
        }

        private static string BuildScatterChart(ChartData chart)
        {
            const int xAxisId = 62650048;
            const int yAxisId = 62652672;
            string okSeries = BuildPointSeries(0, "OK 測試值", chart.OkXValues, chart.OkYValues, "156082", "circle", 5, false);
            string ngSeries = BuildPointSeries(1, "NG 測試值", chart.NgXValues, chart.NgYValues, "C00000", "diamond", 7, false);
            string lowerSeries = BuildSpecLineSeries(2, "規格下限", chart.AllXValues, chart.LowerLineValue);
            string upperSeries = chart.UpperLineValue.HasValue
                ? BuildSpecLineSeries(3, "規格上限", chart.AllXValues, chart.UpperLineValue.Value)
                : string.Empty;
            string yScaling = BuildAxisScaling(chart.YAxisMinimum, chart.YAxisMaximum);
            string xScaling = BuildAxisScaling(1, Math.Max(1, chart.AllXValues.Count));
            string xMajorUnitValue = chart.AllXValues.Count > 30
                ? Math.Ceiling(chart.AllXValues.Count / 10.0).ToString("G", CultureInfo.InvariantCulture)
                : "1";

            return $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><c:chartSpace xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><c:chart><c:title><c:tx><c:rich><a:bodyPr/><a:lstStyle/><a:p><a:r><a:rPr sz="1400"/><a:t>{Escape(chart.Title)}</a:t></a:r></a:p></c:rich></c:tx><c:layout/></c:title><c:plotArea><c:layout/><c:scatterChart><c:scatterStyle val="lineMarker"/>{okSeries}{ngSeries}{lowerSeries}{upperSeries}<c:axId val="{xAxisId}"/><c:axId val="{yAxisId}"/></c:scatterChart><c:valAx><c:axId val="{xAxisId}"/><c:delete val="0"/>{xScaling}<c:axPos val="b"/>{BuildAxisTitle("圖片序號")}<c:numFmt formatCode="0" sourceLinked="0"/><c:majorTickMark val="out"/><c:minorTickMark val="none"/><c:tickLblPos val="nextTo"/><c:spPr><a:ln w="9525"><a:solidFill><a:srgbClr val="595959"/></a:solidFill></a:ln></c:spPr><c:txPr>{BuildAxisTextProperties()}</c:txPr><c:crossAx val="{yAxisId}"/><c:crosses val="min"/><c:majorUnit val="{xMajorUnitValue}"/></c:valAx><c:valAx><c:axId val="{yAxisId}"/><c:delete val="0"/>{yScaling}<c:axPos val="l"/>{BuildMajorGridlines()}<c:numFmt formatCode="0.00" sourceLinked="0"/><c:majorTickMark val="out"/><c:minorTickMark val="none"/><c:tickLblPos val="nextTo"/><c:spPr><a:ln w="9525"><a:solidFill><a:srgbClr val="595959"/></a:solidFill></a:ln></c:spPr><c:txPr>{BuildAxisTextProperties()}</c:txPr><c:crossAx val="{xAxisId}"/><c:crosses val="min"/><c:majorUnit val="{chart.YAxisMajorUnit.ToString("G", CultureInfo.InvariantCulture)}"/></c:valAx></c:plotArea><c:legend><c:legendPos val="r"/><c:layout/></c:legend><c:plotVisOnly val="1"/></c:chart></c:chartSpace>""";
        }

        private static string BuildPointSeries(int index, string name, IReadOnlyList<double> xValues, IReadOnlyList<double> yValues, string color, string marker, int markerSize, bool showValues)
        {
            if (xValues.Count == 0)
            {
                return string.Empty;
            }

            string labels = showValues
                ? """<c:dLbls><c:showLegendKey val="0"/><c:showVal val="1"/><c:showCatName val="0"/><c:showSerName val="0"/></c:dLbls>"""
                : string.Empty;

            return $"""<c:ser><c:idx val="{index}"/><c:order val="{index}"/><c:tx><c:v>{Escape(name)}</c:v></c:tx><c:spPr><a:ln><a:noFill/></a:ln></c:spPr><c:marker><c:symbol val="{marker}"/><c:size val="{markerSize}"/><c:spPr><a:solidFill><a:srgbClr val="{color}"/></a:solidFill><a:ln><a:solidFill><a:srgbClr val="{color}"/></a:solidFill></a:ln></c:spPr></c:marker>{labels}<c:xVal><c:numLit>{BuildNumberLiteral(xValues)}</c:numLit></c:xVal><c:yVal><c:numLit>{BuildNumberLiteral(yValues)}</c:numLit></c:yVal></c:ser>""";
        }

        private static string BuildSpecLineSeries(int index, string name, IReadOnlyList<double> xValues, double yValue)
        {
            var yValues = Enumerable.Repeat(yValue, Math.Max(1, xValues.Count)).ToList();
            var effectiveXValues = xValues.Count > 0 ? xValues : new List<double> { 1 };
            return $"""<c:ser><c:idx val="{index}"/><c:order val="{index}"/><c:tx><c:v>{Escape(name)}</c:v></c:tx><c:spPr><a:ln w="28575"><a:solidFill><a:srgbClr val="FF0000"/></a:solidFill><a:prstDash val="dash"/></a:ln></c:spPr><c:marker><c:symbol val="none"/></c:marker><c:xVal><c:numLit>{BuildNumberLiteral(effectiveXValues)}</c:numLit></c:xVal><c:yVal><c:numLit>{BuildNumberLiteral(yValues)}</c:numLit></c:yVal></c:ser>""";
        }

        private static string BuildAxisScaling(double minimum, double maximum)
        {
            return $"""<c:scaling><c:orientation val="minMax"/><c:min val="{minimum.ToString("G", CultureInfo.InvariantCulture)}"/><c:max val="{maximum.ToString("G", CultureInfo.InvariantCulture)}"/></c:scaling>""";
        }

        private static string BuildAxisTitle(string title)
        {
            return $"""<c:title><c:tx><c:rich><a:bodyPr/><a:lstStyle/><a:p><a:r><a:rPr sz="900"/><a:t>{Escape(title)}</a:t></a:r></a:p></c:rich></c:tx><c:layout/></c:title>""";
        }

        private static string BuildAxisTextProperties()
        {
            return """<a:bodyPr/><a:lstStyle/><a:p><a:pPr><a:defRPr sz="900"><a:solidFill><a:srgbClr val="404040"/></a:solidFill></a:defRPr></a:pPr><a:endParaRPr lang="zh-TW"/></a:p>""";
        }

        private static string BuildMajorGridlines()
        {
            return """<c:majorGridlines><c:spPr><a:ln w="6350"><a:solidFill><a:srgbClr val="D9D9D9"/></a:solidFill></a:ln></c:spPr></c:majorGridlines>""";
        }

        private static (double Minimum, double Maximum, double MajorUnit) CalculateYAxisRange(IReadOnlyList<double> values, double lowerLimit, double? upperLimit)
        {
            var rangeValues = values.Concat(new[] { lowerLimit }).ToList();
            if (upperLimit.HasValue)
            {
                rangeValues.Add(upperLimit.Value);
            }

            if (rangeValues.Count == 0)
            {
                return (0, 1, 0.2);
            }

            double minimum = rangeValues.Min();
            double maximum = rangeValues.Max();
            double span = Math.Max(maximum - minimum, Math.Max(Math.Abs(maximum) * 0.02, 1));
            double padding = span * 0.10;
            double majorUnit = CalculateNiceStep(span / 5);
            double axisMinimum = Math.Floor((minimum - padding) / majorUnit) * majorUnit;
            double axisMaximum = Math.Ceiling((maximum + padding) / majorUnit) * majorUnit;
            if (Math.Abs(axisMaximum - axisMinimum) < double.Epsilon)
            {
                axisMaximum = axisMinimum + majorUnit;
            }

            return (axisMinimum, axisMaximum, majorUnit);
        }

        private static double CalculateNiceStep(double rawStep)
        {
            if (rawStep <= 0 || double.IsNaN(rawStep) || double.IsInfinity(rawStep))
            {
                return 1;
            }

            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
            double normalized = rawStep / magnitude;
            double niceNormalized = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
            return niceNormalized * magnitude;
        }

        private static string BuildStringLiteral(IReadOnlyList<string> values)
        {
            var sb = new StringBuilder();
            sb.Append($"""<c:ptCount val="{values.Count}"/>""");
            for (int i = 0; i < values.Count; i++)
            {
                sb.Append($"""<c:pt idx="{i}"><c:v>{Escape(values[i])}</c:v></c:pt>""");
            }
            return sb.ToString();
        }

        private static string BuildNumberLiteral(IReadOnlyList<int> values)
        {
            return BuildNumberLiteral(values.Select(value => (double)value).ToList());
        }

        private static string BuildNumberLiteral(IReadOnlyList<double> values)
        {
            var sb = new StringBuilder();
            sb.Append("""<c:formatCode>General</c:formatCode>""");
            sb.Append($"""<c:ptCount val="{values.Count}"/>""");
            for (int i = 0; i < values.Count; i++)
            {
                sb.Append($"""<c:pt idx="{i}"><c:v>{values[i].ToString("G", CultureInfo.InvariantCulture)}</c:v></c:pt>""");
            }
            return sb.ToString();
        }

        private static void AddText(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(content);
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static string FormatPercent(double value)
        {
            return double.IsNaN(value) ? "N/A" : value.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);
        }

        private static string Escape(string? value)
        {
            return SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
        }

        private sealed record WorksheetData(string Name, List<string[]> Rows, List<ChartData> Charts);
        private sealed record HistogramBin(string Label, int Count);
        private sealed record MetricPoint(int Index, string FileName, double Value, bool IsPass, double? DeltaPercent);

        private enum ChartKind
        {
            Bar,
            Scatter
        }

        private enum ChartAnchor
        {
            Top,
            Bottom
        }

        private sealed record ChartData(
            ChartKind Kind,
            string Title,
            ChartAnchor Anchor,
            IReadOnlyList<string> Categories,
            IReadOnlyList<int> Counts,
            IReadOnlyList<double> AllXValues,
            IReadOnlyList<double> OkXValues,
            IReadOnlyList<double> OkYValues,
            IReadOnlyList<double> NgXValues,
            IReadOnlyList<double> NgYValues,
            double LowerLineValue,
            double? UpperLineValue,
            double YAxisMinimum,
            double YAxisMaximum,
            double YAxisMajorUnit)
        {
            public static ChartData Bar(string title, IReadOnlyList<string> categories, IReadOnlyList<int> counts, ChartAnchor anchor)
            {
                return new ChartData(
                    ChartKind.Bar,
                    title,
                    anchor,
                    categories,
                    counts,
                    Array.Empty<double>(),
                    Array.Empty<double>(),
                    Array.Empty<double>(),
                    Array.Empty<double>(),
                    Array.Empty<double>(),
                    0,
                    null,
                    0,
                    1,
                    0.2);
            }

            public static ChartData Scatter(string title, IReadOnlyList<MetricPoint> points, double lowerLineValue, double? upperLineValue, ChartAnchor anchor)
            {
                var okPoints = points.Where(point => point.IsPass).ToList();
                var ngPoints = points.Where(point => !point.IsPass).ToList();
                var allXValues = points.Select(point => (double)point.Index).ToList();
                var (minimum, maximum, majorUnit) = CalculateYAxisRange(points.Select(point => point.Value).ToList(), lowerLineValue, upperLineValue);

                return new ChartData(
                    ChartKind.Scatter,
                    title,
                    anchor,
                    Array.Empty<string>(),
                    Array.Empty<int>(),
                    allXValues,
                    okPoints.Select(point => (double)point.Index).ToList(),
                    okPoints.Select(point => point.Value).ToList(),
                    ngPoints.Select(point => (double)point.Index).ToList(),
                    ngPoints.Select(point => point.Value).ToList(),
                    lowerLineValue,
                    upperLineValue,
                    minimum,
                    maximum,
                    majorUnit);
            }
        }
    }
}
