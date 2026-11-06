using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LumasharpChecker.model;

/// <summary>
/// 標準件中斷後的安全復原：只有正式影像及參數均符合已驗證還原點，
/// 且殘留檔案內容可逐一校驗時，才備份並移除純暫存殘留。
/// 永不自動覆蓋正式影像、參數或還原點；條件不符則保持唯讀。
/// </summary>
internal static class StandardSampleRecovery
{
    internal sealed class Result
    {
        public int RecoveredFiles { get; set; }
        public List<string> Issues { get; } = new();
        public bool RequiresAttention => Issues.Count > 0;
    }

    private static bool IsReplacementTemp(string name)
        => Regex.IsMatch(name,
            @"^_light-standard(?:-fullimage)?\.(?:png|bmp|jpg|jpeg|tif|tiff)\.[a-f0-9]{32}\.tmp$",
            RegexOptions.IgnoreCase);

    private static bool IsReplaceSidecar(string name)
        => Regex.IsMatch(name,
            @"^_light-standard(?:-fullimage)?\.(?:png|bmp|jpg|jpeg|tif|tiff)~RF[a-f0-9]+\.tmp$",
            RegexOptions.IgnoreCase);

    internal static IReadOnlyList<string> FindResidues(string recipeRoot, int station)
    {
        string stationFolder = Path.Combine(recipeRoot, RecipeManager.GetStationDirectoryName(station));
        var paths = new List<string>();
        if (Directory.Exists(stationFolder))
        {
            paths.AddRange(Directory.GetFiles(stationFolder, "*", SearchOption.TopDirectoryOnly)
                .Where(path => IsReplacementTemp(Path.GetFileName(path)) || IsReplaceSidecar(Path.GetFileName(path))));
        }
        string historyRoot = LightSampleHistory.Folder(recipeRoot, station);
        foreach (string folder in new[] { historyRoot,
            Path.Combine(historyRoot, "with-localization"), Path.Combine(historyRoot, "without-localization") })
        {
            if (!Directory.Exists(folder)) continue;
            paths.AddRange(Directory.GetFiles(folder, ".pending-*", SearchOption.TopDirectoryOnly));
            paths.AddRange(Directory.GetDirectories(folder, ".apply-*", SearchOption.TopDirectoryOnly));
        }
        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var algorithm = SHA256.Create();
        return Convert.ToBase64String(algorithm.ComputeHash(stream));
    }

    private static string HashSettings(LightInspectionSettings settings)
    {
        using var algorithm = SHA256.Create();
        return Convert.ToBase64String(algorithm.ComputeHash(JsonSerializer.SerializeToUtf8Bytes(settings)));
    }

    // 舊版主畫面會將量測基準保存到小數第二位，還原點則保留量測原始精度。
    // 僅接受「還原點基準四捨五入至兩位後，其他全部設定與目前設定完全一致」；
    // 不放寬 ROI、允收範圍或還原 ZIP 本身的 SHA-256 校驗。
    private static bool MatchesRoundedDisplayedBaseline(
        LightInspectionSettings active, LightInspectionSettings archived, bool fullImage)
    {
        double original = fullImage ? archived.FullImageBaseValue : archived.BaseValue;
        double current = fullImage ? active.FullImageBaseValue : active.BaseValue;
        if (Math.Abs(current - Math.Round(original, 2, MidpointRounding.AwayFromZero)) > 1e-9)
            return false;
        var normalized = JsonSerializer.Deserialize<LightInspectionSettings>(
            JsonSerializer.SerializeToUtf8Bytes(archived))!;
        if (fullImage) normalized.FullImageBaseValue = current;
        else normalized.BaseValue = current;
        return JsonSerializer.SerializeToUtf8Bytes(normalized)
            .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(active));
    }

    // 比對正式檔案與最近有效的歷史還原點。校驗失敗不修復，避免把部分交易結果當成已完成。
    private static bool HasVerifiedActiveSample(string recipeRoot, string recipeId, int station, bool fullImage)
    {
        string stationFolder = Path.Combine(recipeRoot, RecipeManager.GetStationDirectoryName(station));
        string prefix = fullImage ? "_light-standard-fullimage." : "_light-standard.";
        string[] active = Directory.Exists(stationFolder)
            ? Directory.GetFiles(stationFolder).Where(path =>
                Path.GetFileName(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                new[] { ".png", ".bmp", ".jpg", ".jpeg", ".tif", ".tiff" }
                    .Contains(Path.GetExtension(path).ToLowerInvariant())).ToArray()
            : Array.Empty<string>();
        if (active.Length != 1) return false;

        string settingsPath = Path.Combine(stationFolder, "light-inspection.json");
        if (!File.Exists(settingsPath)) return false;
        var settings = JsonSerializer.Deserialize<LightInspectionSettings>(File.ReadAllText(settingsPath));
        if (settings == null) return false;
        settings.Validate();
        string imageHash = Sha256(active[0]);
        string settingsHash = HashSettings(settings);
        string historyPath = LightSampleHistory.Folder(recipeRoot, station, fullImage);
        if (!Directory.Exists(historyPath)) return false;
        foreach (string zip in Directory.GetFiles(historyPath, "sample-*.zip")
                     .OrderByDescending(Path.GetFileName, StringComparer.Ordinal))
        {
            try
            {
                var sample = LightSampleHistory.Read(zip, recipeId, station, fullImage);
                if (sample.FullImageMode == fullImage &&
                    sample.ImageHash == imageHash &&
                    (sample.SettingsHash == settingsHash ||
                     MatchesRoundedDisplayedBaseline(settings, sample.Settings, fullImage)))
                    return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or
                   InvalidOperationException or JsonException or ArgumentException)
            {
                // 壞掉的還原點不視為可信資料，也不自動刪除。
            }
        }
        return false;
    }

    private static bool CanRemoveResidue(string path, string activeImageHash)
    {
        if (File.Exists(path))
        {
            string name = Path.GetFileName(path);
            if (IsReplaceSidecar(name)) return new FileInfo(path).Length == 0;
            return IsReplacementTemp(name) && Sha256(path) == activeImageHash;
        }
        if (!Directory.Exists(path) || !Path.GetFileName(path).StartsWith(".apply-", StringComparison.OrdinalIgnoreCase))
            return false;
        string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
        return Directory.GetDirectories(path, "*", SearchOption.AllDirectories).Length == 0 &&
               files.Length == 1 &&
               Regex.IsMatch(Path.GetFileName(files[0]), @"^_light-standard\.(?:png|bmp|jpg|jpeg|tif|tiff)$",
                   RegexOptions.IgnoreCase) &&
               Sha256(files[0]) == activeImageHash;
    }

    private static void BackupThenRemove(string source, string destinationRoot)
    {
        // 先複製再校驗，確認備份完整後才清除來源；不觸及正式配方檔案。
        string destination = Path.Combine(destinationRoot, Path.GetFileName(source));
        if (File.Exists(source))
        {
            File.Copy(source, destination, false);
            if (Sha256(source) != Sha256(destination))
                throw new IOException("復原資料備份驗證失敗。");
            File.Delete(source);
        }
        else
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(destination, Path.GetFileName(file));
                File.Copy(file, target, false);
                if (Sha256(file) != Sha256(target))
                    throw new IOException("復原資料備份驗證失敗。");
            }
            Directory.Delete(source, true);
        }
    }

    internal static Result RepairVerifiedOrphans(string recipeRoot, string recipeId, int stationCount)
    {
        var result = new Result();
        // App/recovery 在 Recipe 之外，永不寫回正式圖或歷史 ZIP。
        string appRoot = Directory.GetParent(Directory.GetParent(recipeRoot)!.FullName)!.FullName;
        string evidenceRoot = Path.Combine(appRoot, "standard-sample-recovery", recipeId);
        for (int station = 1; station <= stationCount; station++)
        {
            var residues = FindResidues(recipeRoot, station);
            if (residues.Count == 0) continue;
            try
            {
                string stationFolder = Path.Combine(recipeRoot, RecipeManager.GetStationDirectoryName(station));
                bool fullImage = residues.Any(path =>
                    path.IndexOf("without-localization", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    Path.GetFileName(path).StartsWith("_light-standard-fullimage.", StringComparison.OrdinalIgnoreCase));
                if (residues.Any(path => Path.GetFileName(path).StartsWith(".pending-", StringComparison.OrdinalIgnoreCase)) ||
                    !HasVerifiedActiveSample(recipeRoot, recipeId, station, fullImage))
                {
                    result.Issues.Add($"第 {station:00} 站的標準件尚無法確認完整，已保留原資料。");
                    continue;
                }
                string prefix = fullImage ? "_light-standard-fullimage." : "_light-standard.";
                string[] active = Directory.GetFiles(stationFolder).Where(path =>
                    Path.GetFileName(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    new[] { ".png", ".bmp", ".jpg", ".jpeg", ".tif", ".tiff" }
                        .Contains(Path.GetExtension(path).ToLowerInvariant())).ToArray();
                string activeHash = Sha256(active[0]);
                if (residues.Any(path => !CanRemoveResidue(path, activeHash)))
                {
                    result.Issues.Add($"第 {station:00} 站有無法核對的暫存資料，已保留原資料。");
                    continue;
                }
                string destination = Path.Combine(evidenceRoot, "station" + station.ToString("D2"),
                    DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(destination);
                foreach (string residue in residues)
                {
                    BackupThenRemove(residue, destination);
                    result.RecoveredFiles++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or
                   JsonException or ArgumentException)
            {
                result.Issues.Add($"第 {station:00} 站暫時無法自動恢復：{ex.GetBaseException().Message}");
            }
        }
        return result;
    }
}
