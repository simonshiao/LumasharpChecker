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
    public sealed record StationExcelReportData(
        int StationNumber,
        string RecipeName,
        List<string[]> SpecRows,
        List<TestImageResultRow> Results);

    public static class ExcelReportWriter
    {
        public static void WriteTestResults(
            string filePath,
            IEnumerable<string[]> specRows,
            IEnumerable<TestImageResultRow> results,
            double brightnessLowerLimit,
            double brightnessUpperLimit)
        {
            var resultRows = results.ToList();
            var sheets = new List<WorksheetData>
            {
                new("檢測規格", specRows.ToList(), new List<ChartData>()),
                new("總表", BuildSummaryRows(resultRows).ToList(), new List<ChartData>()),
                BuildMetricSheet(
                    "亮度",
                    resultRows.Select((result, index) => new { result, index })
                        .Where(item => item.result.BrightnessMeasured)
                        .Select(item => new MetricPoint(item.index + 1, item.result.FileName, item.result.Brightness, item.result.BrightnessSafe, item.result.BrightnessDeltaPercent)).ToList(),
                    brightnessLowerLimit,
                    brightnessUpperLimit)
            };

            WriteWorkbook(filePath, sheets);
        }

        public static void WriteMultiStationTestResults(string filePath, IEnumerable<StationExcelReportData> stations)
        {
            var stationRows = stations.Where(station => station.Results.Count > 0)
                .OrderBy(station => station.StationNumber).ToList();
            if (stationRows.Count == 0) throw new InvalidOperationException("沒有已完成的站別結果可匯出。");

            var sheets = new List<WorksheetData>
            {
                new("多站總覽", BuildMultiStationOverviewRows(stationRows), new List<ChartData>())
            };
            foreach (var station in stationRows)
            {
                sheets.Add(BuildStationBrightnessSheet(station));
            }

            WriteWorkbook(filePath, sheets);
        }

        private static WorksheetData BuildStationBrightnessSheet(StationExcelReportData station)
        {
            var measured = station.Results.Where(result => result.BrightnessMeasured).ToList();
            int total = station.Results.Count;
            int ok = station.Results.Count(result => result.IsOverallPass);
            int ng = total - ok;
            double minimum = measured.Count == 0 ? 0 : measured.Min(result => result.BrightnessMin ?? 0);
            double maximum = measured.Count == 0 ? 255 : measured.Max(result => result.BrightnessMax ?? 255);
            string SpecValue(string key) => station.SpecRows.FirstOrDefault(row => row.Length > 1 && row[0] == key)?[1] ?? string.Empty;

            var rows = new List<string[]>
            {
                new[] { "Recipe", station.RecipeName },
                new[] { "站別", station.StationNumber.ToString(CultureInfo.InvariantCulture) },
                new[] { "檢測計數", "張數" },
                new[] { "總數", total.ToString(CultureInfo.InvariantCulture) },
                new[] { "OK", ok.ToString(CultureInfo.InvariantCulture) },
                new[] { "NG", ng.ToString(CultureInfo.InvariantCulture) },
                new[] { "良率", total == 0 ? "0.00%" : $"{ok * 100.0 / total:F2}%" },
                Array.Empty<string>(),
                new[] { "亮度設定", "內容" },
                new[] { "光源檢測 ROI", SpecValue("光源檢測 ROI") },
                new[] { "判定方式", SpecValue("亮度判定方式") },
                new[] { "基準亮度", SpecValue("亮度基準值") },
                new[] { "下限", SpecValue("亮度有效下限") },
                new[] { "上限", SpecValue("亮度有效上限") }
            };

            var points = measured.Select((result, index) => new MetricPoint(index + 1, result.FileName,
                result.Brightness, result.BrightnessSafe, result.BrightnessDeltaPercent)).ToList();
            var charts = points.Count == 0
                ? new List<ChartData>()
                : new List<ChartData>
                {
                    ChartData.Scatter($"站{station.StationNumber:00}亮度點檢結果圖", points, minimum, maximum, ChartAnchor.Compact)
                };
            return new WorksheetData($"站{station.StationNumber:00}亮度", rows, charts);
        }

        private static List<string[]> BuildMultiStationOverviewRows(IReadOnlyList<StationExcelReportData> stations)
        {
            var rows = new List<string[]>
            {
                new[] { "Recipe", stations[0].RecipeName },
                new[] { "匯出時間", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) },
                Array.Empty<string>(),
                new[] { "站別", "圖片數", "判定 OK", "判定 NG", "良率" }
            };
            foreach (var station in stations)
            {
                int ok = station.Results.Count(result => result.IsOverallPass);
                int ng = station.Results.Count - ok;
                rows.Add(new[]
                {
                    station.StationNumber.ToString(CultureInfo.InvariantCulture),
                    station.Results.Count.ToString(CultureInfo.InvariantCulture),
                    ok.ToString(CultureInfo.InvariantCulture),
                    ng.ToString(CultureInfo.InvariantCulture),
                    station.Results.Count == 0 ? "0.00%" : $"{ok * 100.0 / station.Results.Count:F2}%"
                });
            }
            int totalImages = stations.Sum(station => station.Results.Count);
            int totalOk = stations.Sum(station => station.Results.Count(result => result.IsOverallPass));
            int totalNg = totalImages - totalOk;
            rows.Add(new[]
            {
                "全部已測站",
                totalImages.ToString(CultureInfo.InvariantCulture),
                totalOk.ToString(CultureInfo.InvariantCulture),
                totalNg.ToString(CultureInfo.InvariantCulture),
                totalImages == 0 ? "0.00%" : $"{totalOk * 100.0 / totalImages:F2}%"
            });
            return rows;
        }

        private static void WriteWorkbook(string filePath, IReadOnlyList<WorksheetData> sheets)
        {
            using var archive = ZipFile.Open(filePath, ZipArchiveMode.Create);
            int drawingCount = sheets.Count(sheet => sheet.Charts.Count > 0);
            int chartCount = sheets.Sum(sheet => sheet.Charts.Count);

            AddText(archive, "[Content_Types].xml", BuildContentTypes(sheets.Count, drawingCount, chartCount));
            AddText(archive, "_rels/.rels", BuildPackageRelationships());
            AddText(archive, "xl/workbook.xml", BuildWorkbook(sheets));
            AddText(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships(sheets.Count));
            AddText(archive, "xl/styles.xml", BuildStyles());

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
            yield return new[] { "檔名", "完整路徑", "流程影像路徑", "定位ROI_X", "定位ROI_Y", "定位ROI_W", "定位ROI_H", "亮度", "亮度差異(%)", "光源判定", "總判定", "亮度ROI_X", "亮度ROI_Y", "亮度ROI_W", "亮度ROI_H" };
            foreach (var result in results)
            {
                yield return new[]
                {
                    result.FileName,
                    result.FilePath,
                    result.ProcessedImagePath,
                    result.RoiX.ToString(CultureInfo.InvariantCulture),
                    result.RoiY.ToString(CultureInfo.InvariantCulture),
                    result.RoiW.ToString(CultureInfo.InvariantCulture),
                    result.RoiH.ToString(CultureInfo.InvariantCulture),
                    FormatNumber(result.Brightness),
                    FormatPercent(result.BrightnessDeltaPercent),
                    result.BrightnessResult,
                    result.OverallResult,
                    result.BrightnessRoiX.ToString(CultureInfo.InvariantCulture),
                    result.BrightnessRoiY.ToString(CultureInfo.InvariantCulture),
                    result.BrightnessRoiW.ToString(CultureInfo.InvariantCulture),
                    result.BrightnessRoiH.ToString(CultureInfo.InvariantCulture)
                };
            }
        }

        private static WorksheetData BuildMetricSheet(string metricName, IReadOnlyList<MetricPoint> points, double lowerLimit, double? upperLimit)
        {
            bool hasDeltaPercent = points.Any(point => point.DeltaPercent.HasValue);
            var rows = new List<string[]>
            {
                new[] { $"{metricName}圖表" },
                new[] { "規格下限", FormatNumber(lowerLimit) },
                new[] { "規格上限", upperLimit.HasValue ? FormatNumber(upperLimit.Value) : "N/A" },
                Array.Empty<string>()
            };

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
                ChartData.Scatter($"{metricName}點檢結果圖", points, lowerLimit, upperLimit, ChartAnchor.Compact)
            };

            return new WorksheetData(metricName, rows, charts);
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
            sb.Append("""<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>""");
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
            sb.Append($"""<Relationship Id="rId{sheetCount + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>""");
            sb.Append("</Relationships>");
            return sb.ToString();
        }

        private static string BuildStyles()
        {
            return """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <fonts count="3">
    <font><sz val="10"/><name val="Aptos"/><family val="2"/></font>
    <font><b/><sz val="10"/><name val="Aptos"/><family val="2"/><color rgb="FF17365D"/></font>
    <font><b/><sz val="13"/><name val="Aptos Display"/><family val="2"/><color rgb="FF17365D"/></font>
  </fonts>
  <fills count="4">
    <fill><patternFill patternType="none"/></fill>
    <fill><patternFill patternType="gray125"/></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFEAF2F8"/><bgColor indexed="64"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFDDEBF7"/><bgColor indexed="64"/></patternFill></fill>
  </fills>
  <borders count="2">
    <border><left/><right/><top/><bottom/><diagonal/></border>
    <border><left style="thin"><color rgb="FFB7C9D6"/></left><right style="thin"><color rgb="FFB7C9D6"/></right><top style="thin"><color rgb="FFB7C9D6"/></top><bottom style="thin"><color rgb="FFB7C9D6"/></bottom><diagonal/></border>
  </borders>
  <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
  <cellXfs count="4">
    <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1"/>
    <xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment vertical="center" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="2" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment vertical="center" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="1" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment vertical="center" wrapText="1"/></xf>
  </cellXfs>
  <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
</styleSheet>
""";
        }

        private static string BuildWorksheet(IEnumerable<string[]> rows, bool hasCharts)
        {
            var rowList = rows.ToList();
            var sb = new StringBuilder();
            sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
            sb.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheetViews><sheetView showGridLines="0" workbookViewId="0"/></sheetViews><sheetFormatPr defaultRowHeight="20"/>""");
            sb.Append(BuildColumnDefinitions(rowList));
            sb.Append("<sheetData>");
            int rowIndex = 1;
            foreach (var row in rowList)
            {
                int rowStyle = GetRowStyle(row);
                sb.Append($"""<row r="{rowIndex}" ht="{(rowStyle == 2 ? "24" : "20")}" customHeight="1">""");
                for (int columnIndex = 0; columnIndex < row.Length; columnIndex++)
                {
                    string cell = row[columnIndex];
                    int style = rowStyle == 0 ? 0 : rowStyle;
                    sb.Append($"""<c r="{CellAddress(columnIndex + 1, rowIndex)}" s="{style}" t="inlineStr"><is><t>{Escape(cell)}</t></is></c>""");
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

        private static string BuildColumnDefinitions(IReadOnlyList<string[]> rows)
        {
            int maxColumns = rows.Count == 0 ? 0 : rows.Max(row => row.Length);
            if (maxColumns == 0) return string.Empty;
            var sb = new StringBuilder("<cols>");
            for (int column = 0; column < maxColumns; column++)
            {
                int maxLength = rows.Where(row => row.Length > column)
                    .Select(row => row[column]?.Length ?? 0).DefaultIfEmpty(0).Max();
                double width = Math.Max(12, Math.Min(48, maxLength + 2));
                sb.Append($"""<col min="{column + 1}" max="{column + 1}" width="{width.ToString("F1", CultureInfo.InvariantCulture)}" customWidth="1"/>""");
            }
            return sb.Append("</cols>").ToString();
        }

        private static int GetRowStyle(string[] row)
        {
            string first = row.FirstOrDefault() ?? string.Empty;
            if (first == "全部已測站") return 3;
            if (row.Length == 1 && !string.IsNullOrWhiteSpace(first)) return 2;
            if (first is "項目" or "檔名" or "站別" or "分布區間" or "圖片序號") return 1;
            return 0;
        }

        private static string CellAddress(int column, int row)
        {
            var name = new StringBuilder();
            while (column > 0)
            {
                column--;
                name.Insert(0, (char)('A' + column % 26));
                column /= 26;
            }
            return name.Append(row.ToString(CultureInfo.InvariantCulture)).ToString();
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
                int fromRow = anchor == ChartAnchor.Bottom ? 24 : 1;
                int toRow = anchor == ChartAnchor.Top ? 21 : anchor == ChartAnchor.Bottom ? 49 : 16;
                int fromColumn = anchor == ChartAnchor.Compact ? 2 : 3;
                int toColumn = anchor == ChartAnchor.Compact ? 12 : 18;
                int relId = i + 1;
                sb.Append($"""<xdr:twoCellAnchor><xdr:from><xdr:col>{fromColumn}</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>{fromRow}</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from><xdr:to><xdr:col>{toColumn}</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>{toRow}</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:to><xdr:graphicFrame macro=""><xdr:nvGraphicFramePr><xdr:cNvPr id="{chartIndices[i] + 1}" name="Chart {chartIndices[i]}"/><xdr:cNvGraphicFramePr/></xdr:nvGraphicFramePr><xdr:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/></xdr:xfrm><a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/chart"><c:chart xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" r:id="rId{relId}"/></a:graphicData></a:graphic></xdr:graphicFrame><xdr:clientData/></xdr:twoCellAnchor>""");
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
            string yScaling = BuildAutoAxisScaling();
            string xScaling = BuildAxisScaling(1, Math.Max(1, chart.AllXValues.Count));
            string xMajorUnitValue = chart.AllXValues.Count > 30
                ? Math.Ceiling(chart.AllXValues.Count / 10.0).ToString("G", CultureInfo.InvariantCulture)
                : "1";

            return $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><c:chartSpace xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><c:chart><c:title><c:tx><c:rich><a:bodyPr/><a:lstStyle/><a:p><a:r><a:rPr sz="1400"/><a:t>{Escape(chart.Title)}</a:t></a:r></a:p></c:rich></c:tx><c:layout/></c:title><c:plotArea><c:layout/><c:scatterChart><c:scatterStyle val="lineMarker"/>{okSeries}{ngSeries}{lowerSeries}{upperSeries}<c:axId val="{xAxisId}"/><c:axId val="{yAxisId}"/></c:scatterChart><c:valAx><c:axId val="{xAxisId}"/><c:delete val="0"/>{xScaling}<c:axPos val="b"/>{BuildAxisTitle("圖片序號")}<c:numFmt formatCode="0" sourceLinked="0"/><c:majorTickMark val="out"/><c:minorTickMark val="none"/><c:tickLblPos val="nextTo"/><c:spPr><a:ln w="9525"><a:solidFill><a:srgbClr val="595959"/></a:solidFill></a:ln></c:spPr><c:txPr>{BuildAxisTextProperties()}</c:txPr><c:crossAx val="{yAxisId}"/><c:crosses val="min"/><c:majorUnit val="{xMajorUnitValue}"/></c:valAx><c:valAx><c:axId val="{yAxisId}"/><c:delete val="0"/>{yScaling}<c:axPos val="l"/>{BuildMajorGridlines()}<c:numFmt formatCode="0.00" sourceLinked="0"/><c:majorTickMark val="out"/><c:minorTickMark val="none"/><c:tickLblPos val="nextTo"/><c:spPr><a:ln w="9525"><a:solidFill><a:srgbClr val="595959"/></a:solidFill></a:ln></c:spPr><c:txPr>{BuildAxisTextProperties()}</c:txPr><c:crossAx val="{xAxisId}"/><c:crosses val="min"/></c:valAx></c:plotArea><c:legend><c:legendPos val="r"/><c:layout/></c:legend><c:plotVisOnly val="1"/></c:chart></c:chartSpace>""";
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

        private static string BuildAutoAxisScaling() => """<c:scaling><c:orientation val="minMax"/></c:scaling>""";

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
        private sealed record MetricPoint(int Index, string FileName, double Value, bool IsPass, double? DeltaPercent);

        private enum ChartKind
        {
            Bar,
            Scatter
        }

        private enum ChartAnchor
        {
            Top,
            Bottom,
            Compact
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
            double? UpperLineValue)
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
                    null);
            }

            public static ChartData Scatter(string title, IReadOnlyList<MetricPoint> points, double lowerLineValue, double? upperLineValue, ChartAnchor anchor)
            {
                var okPoints = points.Where(point => point.IsPass).ToList();
                var ngPoints = points.Where(point => !point.IsPass).ToList();
                var allXValues = points.Select(point => (double)point.Index).ToList();
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
                    upperLineValue);
            }
        }
    }
}
