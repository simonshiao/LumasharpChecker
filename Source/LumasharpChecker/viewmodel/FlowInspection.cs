using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Emgu.CV;
using Emgu.CV.CvEnum;
using LumasharpChecker.model;

namespace LumasharpChecker.viewmodel;

partial class mainViewModel
{
    private sealed class FlowImageAnalysis
    {
        internal NativeRecipeBridge.StageReport Report { get; init; } = null!;
        internal TestImageResultRow Row { get; init; } = null!;
    }

    internal string LightFlowSelection { get; set; } = "111";
    internal Func<Task>? InspectCurrentFlow { get; set; }
    internal Func<bool>? IsNativeOperationBusy { get; set; }
    private List<TestImageResultRow>? _pendingFlowResults;
    private readonly Dictionary<(string RecipeId, int Station, string Image), TestImageResultRow> _singleFlowDetails = new();
    internal string LoadFlowSelection() => CurrentRecipe == null ? "000" : recipeManager.LoadFlowSelection(CurrentRecipe, CurrentStationNumber);
    internal void SaveFlowSelection(string selection)
    {
        if (CurrentRecipe != null) recipeManager.SaveFlowSelection(CurrentRecipe, CurrentStationNumber, selection);
    }
    internal bool LoadLightInspectionEnabled() => CurrentRecipe == null ||
        recipeManager.LoadLightInspectionEnabled(CurrentRecipe, CurrentStationNumber);
    internal void SaveLightInspectionEnabled(bool enabled)
    {
        if (CurrentRecipe != null) recipeManager.SaveLightInspectionEnabled(CurrentRecipe, CurrentStationNumber, enabled);
    }
    internal void SyncEditedFlowTool(string tool)
    {
        if (CurrentRecipe == null) return;
        int index = tool == "定位" ? 0 : tool == "選向" ? 1 : 2;
        char[] choices = LoadFlowSelection().ToCharArray();
        choices[index] = recipeManager.ReadNativeFlowSelection(CurrentRecipe, CurrentStationNumber)[index];
        SaveFlowSelection(new string(choices));
    }

    internal void BeginContinuousFlow()
    {
        TestImageResults.Clear();
        if (CurrentRecipe != null)
        {
            _multiStationResults.Remove((CurrentRecipe.Id, CurrentStationNumber));
            foreach (var key in _singleFlowDetails.Keys
                         .Where(key => key.RecipeId == CurrentRecipe.Id && key.Station == CurrentStationNumber).ToArray())
                _singleFlowDetails.Remove(key);
        }
        ResetTestImageSummary();
        IsTestImageAnalysisComplete = false;
        _pendingFlowResults = new List<TestImageResultRow>();
    }

    internal void StoreContinuousFlowResult(TestImageResultRow row)
    {
        if (_pendingFlowResults == null) throw new InvalidOperationException("尚未開始連續檢測。");
        _pendingFlowResults.Add(row);
    }

    internal double FinishContinuousFlow(bool presentResults = true)
    {
        var rows = _pendingFlowResults; _pendingFlowResults = null;
        if (!presentResults || rows == null || rows.Count == 0) return 0;
        var timer = Stopwatch.StartNew();
        // Publish one collection and one summary, then decode only the last completed preview.
        TestImageResults = new ObservableCollection<TestImageResultRow>(rows.GroupBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase).Select(g => g.Last()));
        UpdateTestImageSummary();
        IsTestImageAnalysisComplete = true;
        var completedResults = TestImageResults.ToList();
        int completedOk = completedResults.Count(row => row.IsOverallPass);
        int completedNg = completedResults.Count - completedOk;
        int classifiedOk = completedResults.Count(row => row.ReferenceClass == "OK");
        int classifiedNg = completedResults.Count(row => row.ReferenceClass == "NG");
        var stationSummary = new StationRunSummary(completedOk, completedNg, "完成",
            $"來源 OK {classifiedOk}／NG {classifiedNg}；新版流程判定 OK {completedOk}／NG {completedNg}", completedResults);
        var currentStationSlot = StationSlots.FirstOrDefault(slot => slot.Number == CurrentStationNumber);
        if (CurrentRecipe != null && currentStationSlot != null)
            StoreResult(currentStationSlot, CurrentRecipe.Id, stationSummary);
        var last = rows[rows.Count - 1];
        SelectFlowImage(last.FilePath);
        var labeled = rows.Where(row => row.ReferenceClass is "OK" or "NG").ToArray();
        if (labeled.Length > 0)
        {
            int sourceOk = labeled.Count(row => row.ReferenceClass == "OK");
            int sourceNg = labeled.Count(row => row.ReferenceClass == "NG");
            int detectedOk = rows.Count(row => row.IsOverallPass);
            int detectedNg = rows.Count - detectedOk;
            int ngMissed = labeled.Count(row => row.ReferenceClass == "NG" && row.IsOverallPass);
            int okFalseReject = labeled.Count(row => row.ReferenceClass == "OK" && !row.IsOverallPass);
            SaveSettingsMessage = $"來源 OK {sourceOk}／NG {sourceNg}；流程判定 OK {detectedOk}／NG {detectedNg}；NG 誤判 OK {ngMissed} 張、OK 誤判 NG {okFalseReject} 張。最後：{last.Message}";
        }
        else SaveSettingsMessage = last.Message;
        timer.Stop(); return timer.Elapsed.TotalMilliseconds;
    }

    internal void RecordFlowStatistics(TestImageResultRow row, bool accumulate)
    {
        if (CurrentRecipe == null) return;
        var key = (CurrentRecipe.Id, CurrentStationNumber, row.FilePath);
        if (accumulate)
        {
            _singleFlowDetails.Remove(key);
            UpsertTestImageResult(row);
        }
        else
        {
            // Keep single-image outcomes available in the detail window without
            // changing the batch OK/NG/yield counters.
            _singleFlowDetails[key] = row;
        }
    }

    internal async Task<NativeLocationService.Frame> PrepareLightFrameAsync(string image)
    {
        var recipe = CurrentRecipe ?? throw new InvalidOperationException("請先載入配方。");
        var report = await NativeRecipeBridge.RunPreLightAsync(recipe, CurrentStationNumber, image,
            AppDomain.CurrentDomain.BaseDirectory, "way", LightFlowSelection);
        if (report.Exit != 0 || report.Locate != "PASS")
            throw new InvalidOperationException(report.Summary + "；" + report.Error);
        return CheckedFrame(report);
    }

    private static NativeLocationService.Frame CheckedFrame(NativeRecipeBridge.StageReport report)
    {
        string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NativeR3", "LumaProbe")) + Path.DirectorySeparatorChar;
        if (string.IsNullOrEmpty(report.OutputImage) || !Path.GetFullPath(report.OutputImage).StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(report.OutputImage))
            throw new InvalidDataException("流程未回傳有效影像。");
        return new NativeLocationService.Frame { ImagePath = report.OutputImage,
            Bounds = new System.Drawing.Rectangle(report.X, report.Y, report.Width, report.Height),
            OriginX = report.ImageX, OriginY = report.ImageY };
    }

    internal void SelectFlowImage(string image)
    {
        int index = TestImageFiles.IndexOf(image);
        if (index < 0) throw new InvalidOperationException("圖片清單已變更。");
        CurrentTestImageIndex = index;
        ProcessCurrentTestImage();
    }

    internal async Task<NativeRecipeBridge.StageReport> InspectFlowAsync(string image, string selection, bool accumulate = false, bool deferPresentation = false, bool lightInspectionEnabled = true)
    {
        var total = Stopwatch.StartNew();
        var recipe = CurrentRecipe ?? throw new InvalidOperationException("請先載入配方。");
        int station = CurrentStationNumber;
        FlowImageAnalysis analysis = await AnalyzeFlowImageAsync(recipe, station, image, selection, lightInspectionEnabled);
        var report = analysis.Report;
        var row = analysis.Row;
        if (CurrentRecipe?.Id != recipe.Id || CurrentStationNumber != station || (!deferPresentation && CurrentTestImagePath != image))
            throw new InvalidOperationException("配方、站別或圖片已變更，結果作廢。");
        if (deferPresentation)
        {
            if (!accumulate) throw new InvalidOperationException("延後顯示只適用於連續檢測。");
            StoreContinuousFlowResult(row);
            report.PresentationDeferred = true;
            total.Stop(); report.TotalMs = total.Elapsed.TotalMilliseconds;
            return report;
        }
        // Browsing/retesting is a preview. Batch results and yield belong only to a continuous run.
        var presentation = Stopwatch.StartNew();
        RecordFlowStatistics(row, accumulate);
        ApplyTestImageResult(row);
        presentation.Stop(); report.StatisticsMs = presentation.Elapsed.TotalMilliseconds;
        presentation.Restart();
        LoadCurrentPreviewIfNeeded(row);
        LastFlowPreviewPath = image;
        UpdateDisplayRoiProperties();
        presentation.Stop(); report.PreviewMs = presentation.Elapsed.TotalMilliseconds;
        presentation.Restart();
        SaveSettingsMessage = row.Message;
        presentation.Stop(); report.MessageMs = presentation.Elapsed.TotalMilliseconds;
        total.Stop(); report.TotalMs = total.Elapsed.TotalMilliseconds;
        return report;
    }

    internal async Task<TestImageResultRow> InspectFlowForStationAsync(
        RecipeManager.RecipeManifest recipe, int station, string image, string selection, bool lightInspectionEnabled = true)
    {
        FlowImageAnalysis analysis = await AnalyzeFlowImageAsync(recipe, station, image, selection, lightInspectionEnabled);
        return analysis.Row;
    }

    private async Task<FlowImageAnalysis> AnalyzeFlowImageAsync(
        RecipeManager.RecipeManifest recipe, int station, string image, string selection, bool lightInspectionEnabled)
    {
        var analysis = await AnalyzeFlowImageCoreAsync(recipe, station, image, selection, lightInspectionEnabled);
        SaveInspectionOutputImage(recipe, station, image, analysis);
        return analysis;
    }

    private async Task<FlowImageAnalysis> AnalyzeFlowImageCoreAsync(
        RecipeManager.RecipeManifest recipe, int station, string image, string selection, bool lightInspectionEnabled)
    {
        var total = Stopwatch.StartNew();
        LightInspectionSettings? settings = null;
        (double Min, double Max) limits = (0, 255);
        if (lightInspectionEnabled)
        {
            settings = LoadLightSettings(recipe, station);
            if (!settings.HasRoi) throw new InvalidOperationException("請先在右側「光源檢測」拖曳 ROI 並儲存本站光源設定。");
            limits = settings.EffectiveLimits();
        }
        var report = await NativeRecipeBridge.RunPreLightAsync(recipe, station, image,
            AppDomain.CurrentDomain.BaseDirectory, "face", selection).ConfigureAwait(false);
        if (report.IsConfigurationError)
        {
            report.Light = "NOT_RUN";
            throw new FlowConfigurationException(report);
        }
        var row = new TestImageResultRow { FilePath = image, FileName = Path.GetFileName(image), InspectionPipeline = "新版流程", BrightnessMeasured = false, IncludeSharpness = false,
            ReferenceClass = GetReferenceClassForImage(image),
            BrightnessMin = limits.Min, BrightnessMax = limits.Max, BrightnessDeltaPercent = double.NaN,
            IsMatched = report.Exit == 0, Message = report.Summary + " " + report.Error };
        if (report.Exit != 0)
        {
            row.IsMatched = false;
            row.Message = BuildFlowFailureMessage(report);
            report.Light = "NOT_RUN";
            return new FlowImageAnalysis { Report = report, Row = row };
        }

        NativeLocationService.Frame frame;
        try
        {
            frame = CheckedFrame(report);
        }
        catch (Exception ex)
        {
            row.IsMatched = false;
            row.Message = $"導正影像讀取失敗：{ex.GetBaseException().Message}";
            report.Error = row.Message;
            report.Light = "NOT_RUN";
            return new FlowImageAnalysis { Report = report, Row = row };
        }

        // The native host exports the full Dst frame, following LocateWindow:
        // the corrected locate crop is copied back into the full image and its
        // local bounds are offset by LocateImageRect before publishing.
        row.ProcessedImagePath = frame.ImagePath;
        row.FrameOriginX = frame.OriginX; row.FrameOriginY = frame.OriginY;
        // Every preview uses the same full-frame coordinate space, regardless
        // of whether SelectWay changed this image.
        row.ShowProcessedPreview = true;
        row.RoiX = frame.Bounds.X + frame.OriginX; row.RoiY = frame.Bounds.Y + frame.OriginY;
        row.RoiW = frame.Bounds.Width; row.RoiH = frame.Bounds.Height;
        if (!lightInspectionEnabled)
        {
            report.Light = "SKIPPED_RECIPE_OFF";
            row.LightInspectionSkipped = true;
            row.Message += "；光源檢測略過";
            total.Stop(); report.TotalMs = total.Elapsed.TotalMilliseconds;
            return new FlowImageAnalysis { Report = report, Row = row };
        }

        System.Drawing.Rectangle roi;
        try
        {
            roi = settings!.GetRoi(frame.Bounds);
        }
        catch (Exception ex)
        {
            row.IsMatched = false;
            row.Message = $"定位／光源 ROI 失敗：{ex.GetBaseException().Message}";
            report.Error = row.Message;
            report.Light = "NOT_RUN";
            return new FlowImageAnalysis { Report = report, Row = row };
        }
        row.BrightnessRoiX = roi.X + frame.OriginX; row.BrightnessRoiY = roi.Y + frame.OriginY;
        row.BrightnessRoiW = roi.Width; row.BrightnessRoiH = roi.Height;
        var timer = Stopwatch.StartNew();
        try
        {
            using var mat = new Mat();
            CvInvoke.Imdecode(File.ReadAllBytes(frame.ImagePath), ImreadModes.Color, mat);
            if (roi.X < 0 || roi.Y < 0 || roi.Right > mat.Width || roi.Bottom > mat.Height)
                throw new InvalidOperationException($"ROI 超出定位影像範圍（影像 {mat.Width}×{mat.Height}，ROI {roi.X},{roi.Y},{roi.Width},{roi.Height}）。");
            using var crop = new Mat(mat, roi);
            using var hsv = new Mat();
            CvInvoke.CvtColor(crop, hsv, ColorConversion.Bgr2Hsv);
            row.Brightness = CvInvoke.Mean(hsv).V2;
            row.BrightnessMeasured = true;
            row.BrightnessSafe = row.Brightness >= limits.Min && row.Brightness <= limits.Max;
            row.BrightnessDeltaPercent = settings.BaseValue > 0 ? (row.Brightness / settings.BaseValue - 1) * 100 : double.NaN;
        }
        catch (Exception ex)
        {
            row.IsMatched = false;
            row.Message = $"光源 ROI 檢測失敗：{ex.GetBaseException().Message}";
            report.Error = row.Message;
            report.Light = "NOT_RUN";
            return new FlowImageAnalysis { Report = report, Row = row };
        }
        timer.Stop(); report.LightMs = timer.Elapsed.TotalMilliseconds;
        report.Light = row.BrightnessSafe ? "PASS" : "FAIL";
        row.Message += $"；光源 {(row.BrightnessSafe ? "OK" : "NG")} {row.Brightness:F2}（{limits.Min:F2}～{limits.Max:F2}）";
        total.Stop(); report.TotalMs = total.Elapsed.TotalMilliseconds;
        return new FlowImageAnalysis { Report = report, Row = row };
    }

    private static void SaveInspectionOutputImage(RecipeManager.RecipeManifest recipe, int station,
        string sourceImage, FlowImageAnalysis analysis)
    {
        if (string.IsNullOrWhiteSpace(recipe.InspectionImageOutputFolder)) return;

        try
        {
            string root = Path.GetFullPath(recipe.InspectionImageOutputFolder);
            DateTime now = DateTime.Now;
            string category = analysis.Row.IsOverallPass ? "OK" : "NG";
            string outputFolder = Path.Combine(root, now.ToString("MM"), now.ToString("dd"),
                station.ToString(), category);
            Directory.CreateDirectory(outputFolder);

            string probeRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "NativeR3", "LumaProbe")) + Path.DirectorySeparatorChar;
            string outputImage = analysis.Report.OutputImage;
            bool isTrustedProcessedImage = !string.IsNullOrWhiteSpace(outputImage) && File.Exists(outputImage) &&
                Path.GetFullPath(outputImage).StartsWith(probeRoot, StringComparison.OrdinalIgnoreCase);
            string imageToSave = isTrustedProcessedImage ? outputImage : sourceImage;
            if (!File.Exists(imageToSave)) throw new FileNotFoundException("找不到要輸出的檢測圖片。", imageToSave);

            string extension = Path.GetExtension(imageToSave);
            string baseName = now.ToString("HHmmssfff") + "_" + Path.GetFileNameWithoutExtension(sourceImage);
            string destination = Path.Combine(outputFolder, baseName + extension);
            for (int suffix = 1; File.Exists(destination); suffix++)
                destination = Path.Combine(outputFolder, baseName + "_" + suffix + extension);
            File.Copy(imageToSave, destination, overwrite: false);
        }
        catch (Exception ex)
        {
            string warning = "檢測圖檔輸出失敗：" + ex.GetBaseException().Message;
            analysis.Row.Message += "；" + warning;
            analysis.Report.Error = string.IsNullOrWhiteSpace(analysis.Report.Error)
                ? warning : analysis.Report.Error + "；" + warning;
        }
    }

    private static string BuildFlowFailureMessage(NativeRecipeBridge.StageReport report)
    {
        string reason = string.IsNullOrWhiteSpace(report.Error) ? report.Summary : report.Error.Trim();
        if (report.Locate != "PASS") return $"定位失敗：{reason}（{report.Summary}）";
        if (report.SelectWay == "FAIL") return $"選向失敗：{reason}（{report.Summary}）";
        if (report.SelectFace == "FAIL") return $"選面失敗：{reason}（{report.Summary}）";
        return $"流程失敗：{reason}（{report.Summary}）";
    }
}

internal sealed class FlowConfigurationException : InvalidOperationException
{
    internal NativeRecipeBridge.StageReport Report { get; }
    internal FlowConfigurationException(NativeRecipeBridge.StageReport report)
        : base(report.Summary + "。流程設定尚未完成，光源未執行，也未計入產品 NG；請設定勾選的工具，或取消使用該工具。") { Report = report; }
}
