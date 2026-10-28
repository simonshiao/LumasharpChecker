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
        public StationRunSummary(int ok, int ng, string status, string details = "")
        {
            Ok = ok;
            Ng = ng;
            Status = status;
            Details = details;
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
                    OnPropertyChanged(nameof(GlobalStatusBrush));
                    OnPropertyChanged(nameof(GlobalRunStatusText));
                }
            }
        }

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
        private string _multiStationTotal = "OK —   NG —   良率 —";
        public string MultiStationTotal
        {
            get => _multiStationTotal;
            private set => SetProperty(ref _multiStationTotal, value);
        }

        [RelayCommand]
        private async Task RunMultiStationInspectionAsync()
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
            MultiStationProgress = "準備多站離線檢測";
            MultiStationTotal = "OK —   NG —   良率 —";
            foreach (var slot in slots)
            {
                _multiStationResults.Remove((recipe.Id, slot.Number));
                slot.ApplyBatchResult(0, 0, slot.IncludeInBatch ? "待測" : "未選取");
            }

            int completed = 0, skipped = 0, totalOk = 0, totalNg = 0;
            try
            {
                foreach (var slot in selected)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    int stationNumber = slot.Number;
                    var station = recipe.Stations.FirstOrDefault(x => x.Number == stationNumber);
                    if (station == null || !station.IsConfigured)
                    {
                        StoreResult(slot, recipe.Id, new StationRunSummary(0, 0, "未設定", "無檢測 Profile，未執行判定"));
                        skipped++;
                        continue;
                    }

                    var stationDirectory = Path.Combine(recipeManager.GetProfileBasePath(recipe),
                        RecipeManager.GetStationDirectoryName(stationNumber));
                    if (!File.Exists(Path.Combine(stationDirectory, "profile.json"))
                        || !File.Exists(Path.Combine(stationDirectory, "pattern.jpg")))
                    {
                        StoreResult(slot, recipe.Id, new StationRunSummary(0, 0, "缺設定", "缺少 profile.json 或 pattern.jpg"));
                        skipped++;
                        continue;
                    }

                    List<string> imageFiles;
                    try
                    {
                        imageFiles = GetStationInspectionImages(station.OkDatabasePath, station.NgDatabasePath);
                    }
                    catch (Exception ex)
                    {
                        StoreResult(slot, recipe.Id, new StationRunSummary(0, 0, "讀圖失敗", ex.Message));
                        skipped++;
                        continue;
                    }
                    if (imageFiles.Count == 0)
                    {
                        StoreResult(slot, recipe.Id, new StationRunSummary(0, 0, "無圖片", "本站 OK／NG 圖庫都沒有圖片"));
                        skipped++;
                        continue;
                    }

                    ProfileManager.ProfileData profile;
                    try
                    {
                        profile = recipeManager.LoadStation(recipe, stationNumber)
                            ?? throw new InvalidOperationException("站別 Profile 無法載入");
                    }
                    catch (Exception ex)
                    {
                        StoreResult(slot, recipe.Id, new StationRunSummary(0, 0, "載入失敗", ex.Message));
                        skipped++;
                        continue;
                    }
                    bool sharpness = profile.DetectionMode == ProfileManager.InspectionMode.BrightnessSharpness;
                    double brightnessBase = profile.BrightnessBaseValue ?? profile.BrightnessMean
                        ?? profile.BrightnessMin ?? 0;
                    string profileName = RecipeManager.GetStationDirectoryName(stationNumber);
                    string profileBase = recipeManager.GetProfileBasePath(recipe);
                    slot.ApplyBatchResult(0, 0, "檢測中", $"共 {imageFiles.Count} 張");
                    MultiStationProgress = $"第 {stationNumber:00} 站 · 0 / {imageFiles.Count}";

                    IProgress<int> progress = new Progress<int>(done =>
                    {
                        if (IsMultiStationTesting && slot.BatchStatus == "檢測中")
                            MultiStationProgress = $"第 {stationNumber:00} 站 · {done} / {imageFiles.Count}";
                    });
                    StationRunSummary result;
                    try
                    {
                        result = await Task.Run(() =>
                        {
                            var processor = new ImageProcessor(profileBase);
                            int ok = 0, ng = 0, errors = 0;
                            for (int i = 0; i < imageFiles.Count; i++)
                            {
                                cancellation.Token.ThrowIfCancellationRequested();
                                var row = AnalyzeSingleTestImage(processor, imageFiles[i],
                                    profileName, sharpness, brightnessBase);
                                if (row.IsOverallPass) ok++; else ng++;
                                if (row.Message.StartsWith("處理失敗", StringComparison.Ordinal))
                                    errors++;
                                if ((i + 1) % 10 == 0 || i == imageFiles.Count - 1)
                                    progress.Report(i + 1);
                            }
                            return new StationRunSummary(ok, ng, "完成",
                                $"已判定 {ok + ng} 張（讀圖／處理失敗 {errors} 張計入 NG）");
                        }, cancellation.Token);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        StoreResult(slot, recipe.Id, new StationRunSummary(0, 0, "執行失敗", ex.Message));
                        skipped++;
                        continue;
                    }
                    StoreResult(slot, recipe.Id, result);
                    totalOk += result.Ok;
                    totalNg += result.Ng;
                    completed++;
                    MultiStationTotal = $"OK {totalOk:N0}   NG {totalNg:N0}   良率 {totalOk * 100.0 / (totalOk + totalNg):F1}%";
                }
                MultiStationProgress = $"檢測完成：{completed} 站，跳過 {skipped} 站";
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

        private List<string> GetStationInspectionImages(string? okPath, string? ngPath)
        {
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

        private void StoreResult(StationSlotViewModel slot, string recipeId, StationRunSummary result)
        {
            _multiStationResults[(recipeId, slot.Number)] = result;
            slot.ApplyBatchResult(result.Ok, result.Ng, result.Status, result.Details);
            // 使用者測試期間可能切入單站頁再返回，總覽集合會重建。
            // 結果同時同步目前畫面上的卡片，避免看板顯示舊狀態。
            if (CurrentRecipe?.Id == recipeId)
            {
                var visible = StationSlots.FirstOrDefault(item => item.Number == slot.Number);
                if (visible != null && !ReferenceEquals(visible, slot))
                    visible.ApplyBatchResult(result.Ok, result.Ng, result.Status, result.Details);
            }
        }
    }
}
