using CommunityToolkit.Mvvm.Input;
using LumasharpChecker.model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace LumasharpChecker.viewmodel
{
    internal sealed class StationRunSummary
    {
        public int Ok { get; }
        public int Ng { get; }
        public string Status { get; }
        public string Details { get; }
        public IReadOnlyList<TestImageResultRow> Results { get; }
        public StationRunSummary(int ok, int ng, string status, string details = "", IReadOnlyList<TestImageResultRow>? results = null)
        {
            Ok = ok;
            Ng = ng;
            Status = status;
            Details = details;
            Results = results ?? Array.Empty<TestImageResultRow>();
        }
    }

    partial class mainViewModel
    {
        private readonly Dictionary<(string RecipeId, int Station), StationRunSummary> _multiStationResults = new();
        private CancellationTokenSource? _multiStationCancellation;
        private bool _isMultiStationTesting;
        public bool IsMultiStationTesting
        {
            get => _isMultiStationTesting;
            private set
            {
                if (SetProperty(ref _isMultiStationTesting, value))
                {
                    OnPropertyChanged(nameof(CanChangeMultiStationDatabaseSelection));
                    OnPropertyChanged(nameof(GlobalStatusBrush));
                    OnPropertyChanged(nameof(GlobalRunStatusText));
                }
            }
        }
        public bool CanChangeMultiStationDatabaseSelection => !IsMultiStationTesting;

        private string _multiStationProgress = "尚未執行檢測";
        public string MultiStationProgress
        {
            get => _multiStationProgress;
            private set
            {
                if (SetProperty(ref _multiStationProgress, value))
                {
                    OnPropertyChanged(nameof(GlobalStatusBrush));
                    OnPropertyChanged(nameof(GlobalRunStatusText));
                }
            }
        }
        private string _multiStationTotal = "判定 OK —   NG —   Retest —   良率 —";
        public string MultiStationTotal
        {
            get => _multiStationTotal;
            private set => SetProperty(ref _multiStationTotal, value);
        }

        private string _multiStationDatabaseSelection = "ALL";
        public string MultiStationDatabaseSelection
        {
            get => _multiStationDatabaseSelection;
            set
            {
                if (value is "OK" or "NG" or "ALL") SetProperty(ref _multiStationDatabaseSelection, value);
            }
        }

        [RelayCommand]
        private Task RunMultiStationInspectionAsync()
            => RunMultiStationInspectionCoreAsync(latestImagesOnly: false);

        [RelayCommand]
        private Task RunMultiStationRecentImagesAsync()
            => RunMultiStationInspectionCoreAsync(latestImagesOnly: true);

        private async Task RunMultiStationInspectionCoreAsync(bool latestImagesOnly)
        {
            if (IsMultiStationTesting) return;
            if (CurrentRecipe == null)
            {
                MultiStationProgress = "請先選擇 Recipe";
                return;
            }
            if (IsAnalyzingTestImages || IsContinuousTestRunning)
            {
                MultiStationProgress = "單站檢測尚未結束，請完成後再開始全站測試";
                return;
            }

            var recipe = CurrentRecipe;
            string databaseSelection = MultiStationDatabaseSelection;
            var slots = StationSlots.ToArray();
            var selected = slots.Where(slot => slot.IncludeInBatch).ToArray();
            if (selected.Length == 0)
            {
                MultiStationProgress = "請先勾選至少一個站別";
                return;
            }

            _multiStationCancellation?.Dispose();
            using var cancellation = new CancellationTokenSource();
            _multiStationCancellation = cancellation;
            IsMultiStationTesting = true;
            MultiStationProgress = latestImagesOnly
                ? "準備依各站來源與張數測試最新圖片"
                : $"準備 {databaseSelection} 多站離線檢測";
            MultiStationTotal = "判定 OK —   NG —   Retest —   良率 —";
            TestImageResults.Clear();
            ResetTestImageSummary();
            IsTestImageAnalysisComplete = false;
            foreach (var key in _singleFlowDetails.Keys
                         .Where(key => key.RecipeId == recipe.Id).ToArray())
                _singleFlowDetails.Remove(key);
            foreach (var slot in slots)
            {
                _multiStationResults.Remove((recipe.Id, slot.Number));
                slot.ApplyBatchResult(0, 0, slot.IncludeInBatch ? "待測" : "未選取");
            }

            int completed = 0, skipped = 0, totalOk = 0, totalNg = 0, totalRetest = 0;
            try
            {
                foreach (var slot in selected)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    int stationNumber = slot.Number;
                    var station = recipe.Stations.FirstOrDefault(x => x.Number == stationNumber);
                    if (station == null)
                    {
                        StoreResult(slot, recipe.Id, new StationRunSummary(0, 0, "未設定", "Recipe 沒有這個站別"));
                        skipped++;
                        continue;
                    }

                    string selection;
                    bool lightInspectionEnabled;
                    LightInspectionSettings lightSettings;
                    try
                    {
                        selection = recipeManager.LoadFlowSelection(recipe, stationNumber);
                        if (selection.Length != 3 || selection[0] != '1')
                            throw new InvalidOperationException("原生流程必須啟用定位。請在該站勾選定位並儲存。");
                        lightInspectionEnabled = recipeManager.LoadLightInspectionEnabled(recipe, stationNumber);
                        if (lightInspectionEnabled)
                        {
                            lightSettings = LoadLightSettings(recipe, stationNumber);
                            if (!lightSettings.HasRoi)
                                throw new InvalidOperationException("尚未設定該站光源 ROI；請先到單站設定並儲存。");
                            lightSettings.EffectiveLimits();
                        }
                    }
                    catch (Exception ex)
                    {
                        StoreResult(slot, recipe.Id, new StationRunSummary(0, 0, "未設定", ex.GetBaseException().Message));
                        skipped++;
                        continue;
                    }

                    List<string> imageFiles;
                    string requestedImageSource = latestImagesOnly ? slot.RecentImageSource : databaseSelection;
                    string effectiveImageSource = requestedImageSource;
                    bool usedAllImageFallback = false;
                    try
                    {
                        imageFiles = GetStationInspectionImages(effectiveImageSource,
                            station.OkDatabasePath, station.NgDatabasePath, station.AllDatabasePath);
                        if (latestImagesOnly && effectiveImageSource != "ALL" && imageFiles.Count == 0)
                        {
                            effectiveImageSource = "ALL";
                            usedAllImageFallback = true;
                            imageFiles = GetStationInspectionImages(effectiveImageSource,
                                station.OkDatabasePath, station.NgDatabasePath, station.AllDatabasePath);
                        }
                        imageFiles = imageFiles
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                    }
                    catch (Exception ex)
                    {
                        StoreResult(slot, recipe.Id, new StationRunSummary(0, 0, "讀圖失敗", ex.Message));
                        skipped++;
                        continue;
                    }
                    if (imageFiles.Count == 0)
                    {
                        StoreResult(slot, recipe.Id, new StationRunSummary(0, 0, "無圖片", $"本站 {effectiveImageSource} 沒有可用圖片"));
                        skipped++;
                        continue;
                    }

                    int availableImageCount = imageFiles.Count;
                    int sourceOkCount = imageFiles.Count(path => GetReferenceClassForImage(path) == "OK");
                    int sourceNgCount = imageFiles.Count(path => GetReferenceClassForImage(path) == "NG");
                    if (latestImagesOnly)
                    {
                        imageFiles = imageFiles
                            .OrderByDescending(GetImageLastWriteTimeUtc)
                            .ThenByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                            .Take(slot.RecentImageCount)
                            .OrderBy(GetImageLastWriteTimeUtc)
                            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                            .ToList();
                    }
                    slot.ApplyBatchResult(0, 0, "檢測中", latestImagesOnly
                        ? $"{GetRecentImageSourceLabel(effectiveImageSource)} · 最新 {imageFiles.Count} 張／共 {availableImageCount} 張"
                        : $"{databaseSelection} 共 {availableImageCount} 張");
                    MultiStationProgress = latestImagesOnly
                        ? $"第 {stationNumber:00} 站 · 最新 {imageFiles.Count} 張"
                        : $"第 {stationNumber:00} 站 · 0 / {imageFiles.Count}";

                    IProgress<int> progress = new Progress<int>(done =>
                    {
                        if (IsMultiStationTesting && slot.BatchStatus == "檢測中")
                            MultiStationProgress = $"第 {stationNumber:00} 站 · {done} / {imageFiles.Count}";
                    });
                    StationRunSummary result;
                    try
                    {
                        int ok = 0, ng = 0, errors = 0, ngMissed = 0, okFalseReject = 0;
                        var imageResults = new List<TestImageResultRow>(imageFiles.Count);
                        for (int i = 0; i < imageFiles.Count; i++)
                        {
                            cancellation.Token.ThrowIfCancellationRequested();
                            var row = await InspectFlowForStationAsync(recipe, stationNumber, imageFiles[i], selection, lightInspectionEnabled);
                            imageResults.Add(row);
                            if (row.IsOverallPass) ok++; else ng++;
                            if (row.ReferenceClass == "NG" && row.IsOverallPass) ngMissed++;
                            if (row.ReferenceClass == "OK" && !row.IsOverallPass) okFalseReject++;
                            if (row.Message.StartsWith("處理失敗", StringComparison.Ordinal)) errors++;
                            if ((i + 1) % 5 == 0 || i == imageFiles.Count - 1) progress.Report(i + 1);
                        }
                        string testScope = latestImagesOnly
                            ? $"來源：{GetRecentImageSourceLabel(effectiveImageSource)}"
                                + (usedAllImageFallback
                                    ? $"（所選 {GetRecentImageSourceLabel(requestedImageSource)} 沒有圖片，已退回全部）"
                                    : string.Empty)
                                + $"；測試最新 {imageFiles.Count} 張（圖庫共 {availableImageCount} 張；最後：{Path.GetFileName(imageFiles[imageFiles.Count - 1])}）"
                            : $"來源：{databaseSelection}；本次測試 {availableImageCount} 張";
                        result = new StationRunSummary(ok, ng, "完成",
                            $"{testScope}；來源 OK {sourceOkCount}／NG {sourceNgCount}；新版流程判定 OK {ok}／NG {ng}；NG 誤判 OK {ngMissed} 張、OK 誤判 NG {okFalseReject} 張；流程錯誤 {errors} 張",
                            imageResults);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (FlowConfigurationException ex)
                    {
                        StoreResult(slot, recipe.Id, new StationRunSummary(0, 0, "未設定", ex.Message));
                        skipped++;
                        continue;
                    }
                    catch (Exception ex)
                    {
                        StoreResult(slot, recipe.Id, new StationRunSummary(0, 0, "執行失敗", ex.Message));
                        skipped++;
                        continue;
                    }
                    StoreResult(slot, recipe.Id, result);
                    totalOk += result.Ok;
                    totalNg += result.Ng;
                    totalRetest += result.Results.Count(row => row.IsRetest);
                    completed++;
                    MultiStationTotal = $"判定 OK {totalOk:N0}   NG {totalNg:N0}（含 Retest）   Retest {totalRetest:N0}   良率 {totalOk * 100.0 / (totalOk + totalNg):F1}%";
                }
                MultiStationProgress = latestImagesOnly
                    ? $"最新圖片測試完成：{completed} 站，跳過 {skipped} 站"
                    : $"檢測完成：{completed} 站，跳過 {skipped} 站";
            }
            catch (OperationCanceledException)
            {
                foreach (var slot in selected.Where(slot => slot.BatchStatus == "檢測中"))
                    StoreResult(slot, recipe.Id, new StationRunSummary(0, 0, "已停止", "取消中的未完成站不計入良率"));
                MultiStationProgress = $"測試已停止：已完成 {completed} 站，跳過 {skipped} 站";
            }
            catch (Exception ex)
            {
                foreach (var slot in selected.Where(slot => slot.BatchStatus == "檢測中"))
                    StoreResult(slot, recipe.Id, new StationRunSummary(0, 0, "執行失敗", ex.Message));
                MultiStationProgress = "多站測試中斷：" + ex.Message;
            }
            finally
            {
                if (ReferenceEquals(_multiStationCancellation, cancellation))
                    _multiStationCancellation = null;
                IsMultiStationTesting = false;
            }
        }

        [RelayCommand]
        private void StopMultiStationInspection() => _multiStationCancellation?.Cancel();

        private List<string> GetStationInspectionImages(string kind, string? okPath, string? ngPath, string? allPath)
        {
            string? selectedPath = kind switch
            {
                "OK" => okPath,
                "NG" => ngPath,
                _ => null
            };
            if (kind is "OK" or "NG")
            {
                string folder = recipeManager.ResolveDatabasePath(selectedPath);
                return Directory.Exists(folder)
                    ? Directory.EnumerateFiles(folder)
                        .Where(path => BaselineImageExtensions.Contains(Path.GetExtension(path)))
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                    : new List<string>();
            }

            string allFolder = recipeManager.ResolveDatabasePath(allPath);
            if (Directory.Exists(allFolder))
                return Directory.EnumerateFiles(allFolder, "*", SearchOption.AllDirectories)
                    .Where(path => BaselineImageExtensions.Contains(Path.GetExtension(path)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList();

            return new[] { okPath, ngPath }
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => recipeManager.ResolveDatabasePath(path))
                .Where(Directory.Exists)
                .SelectMany(path => Directory.EnumerateFiles(path))
                .Where(path => BaselineImageExtensions.Contains(Path.GetExtension(path)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static DateTime GetImageLastWriteTimeUtc(string path)
        {
            try { return File.GetLastWriteTimeUtc(path); }
            catch { return DateTime.MinValue; }
        }

        private void StoreResult(StationSlotViewModel slot, string recipeId, StationRunSummary result)
        {
            _multiStationResults[(recipeId, slot.Number)] = result;
            int retestCount = result.Results.Count(row => row.IsRetest);
            slot.ApplyBatchResult(result.Ok, result.Ng, result.Status, result.Details, retestCount);
            // 使用者測試期間可能切入單站頁再返回，總覽集合會重建。
            // 結果同時同步目前畫面上的卡片，避免看板顯示舊狀態。
            if (CurrentRecipe?.Id == recipeId)
            {
                var visible = StationSlots.FirstOrDefault(item => item.Number == slot.Number);
                if (visible != null && !ReferenceEquals(visible, slot))
                    visible.ApplyBatchResult(result.Ok, result.Ng, result.Status, result.Details, retestCount);
            }
        }

        internal bool RestoreMultiStationResultsForCurrentStation()
        {
            if (CurrentRecipe == null ||
                !_multiStationResults.TryGetValue((CurrentRecipe.Id, CurrentStationNumber), out var summary) ||
                summary.Status != "完成" || summary.Results.Count == 0)
                return false;

            var rows = summary.Results
                .Where(row => File.Exists(row.FilePath))
                .GroupBy(row => row.FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToArray();
            if (rows.Length == 0) return false;

            string previousImage = CurrentTestImagePath;
            TestImageFiles.Clear();
            _allLoadedTestImageFiles.Clear();
            _imageReferenceClasses.Clear();
            foreach (var row in rows)
            {
                TestImageFiles.Add(row.FilePath);
                _allLoadedTestImageFiles.Add(row.FilePath);
                if (row.ReferenceClass is "OK" or "NG")
                    _imageReferenceClasses[row.FilePath] = row.ReferenceClass;
            }

            TestImageResults = new System.Collections.ObjectModel.ObservableCollection<TestImageResultRow>(rows);
            _resultListFilter = "ALL";
            TestImageTree = BuildTestImageTree(TestImageFiles);
            CurrentTestImageIndex = Math.Max(0, TestImageFiles.IndexOf(previousImage));
            UpdateTestImageSummary();
            IsTestImageAnalysisComplete = true;
            ProcessCurrentTestImage();
            SaveSettingsMessage = $"已載入總覽第 {CurrentStationNumber:00} 站的檢測結果：OK {summary.Ok:N0}、NG {summary.Ng:N0}（Retest {rows.Count(row => row.IsRetest):N0}）";
            return true;
        }
    }
}
