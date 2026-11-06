using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace LumasharpChecker.model;

/// <summary>Only light-page standard samples are history. Native recipe copies are not.</summary>
internal static class LightSampleHistory
{
    internal const int Retention = 3;
    internal sealed class Sample
    {
        public int Version { get; set; } = 1;
        public string RecipeId { get; set; } = "";
        public int Station { get; set; }
        public string OriginalName { get; set; } = "";
        public bool? FullImageMode { get; set; }
        public string ImageHash { get; set; } = "";
        public string SettingsHash { get; set; } = "";
        public DateTime CreatedUtc { get; set; }
        public LightInspectionSettings Settings { get; set; } = new();
        [System.Text.Json.Serialization.JsonIgnore]
        public byte[] Image { get; set; } = Array.Empty<byte>();
    }

    internal static string Folder(string recipeDirectory, int station)
        => Path.Combine(recipeDirectory, "light-sample-history", RecipeManager.GetStationDirectoryName(station));

    internal static string Folder(string recipeDirectory, int station, bool fullImageMode)
        => Path.Combine(Folder(recipeDirectory, station), fullImageMode ? "without-localization" : "with-localization");

    internal static void ImportLegacyArchives(string directory, string recipeId, int station, bool fullImageMode)
    {
        string legacyDirectory = Path.GetDirectoryName(directory)!;
        if (!Directory.Exists(legacyDirectory)) return;
        foreach (string archive in Directory.GetFiles(legacyDirectory, "sample-*.zip"))
        {
            try
            {
                var sample = Read(archive, recipeId, station, fullImageMode);
                if (sample.FullImageMode != fullImageMode) continue;
                Directory.CreateDirectory(directory);
                string destination = Path.Combine(directory, Path.GetFileName(archive));
                if (!File.Exists(destination)) File.Copy(archive, destination);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException or ArgumentException)
            {
                // Keep legacy or invalid archives untouched; only compatible, verified samples are copied.
            }
        }
    }

    private static string Hash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(bytes));
    }

    private static byte[] SettingsBytes(LightInspectionSettings settings) => JsonSerializer.SerializeToUtf8Bytes(settings);

    internal static Sample Capture(string recipeId, int station, string image, LightInspectionSettings settings, bool fullImageMode)
    {
        settings.Validate();
        if (fullImageMode ? !settings.HasFullImageRoi : !settings.HasRoi) throw new InvalidDataException("樣本缺少目前模式的光源 ROI。");
        string extension = Path.GetExtension(image).ToLowerInvariant();
        if (!new[] { ".bmp", ".png", ".jpg", ".jpeg", ".tif", ".tiff" }.Contains(extension))
            throw new InvalidDataException("不支援的樣本圖片格式。");
        var bytes = File.ReadAllBytes(image);
        if (bytes.Length == 0 || bytes.LongLength > 512L * 1024 * 1024) throw new InvalidDataException("樣本圖片大小無效。");
        settings.EffectiveLimits(fullImageMode);
        var copy = JsonSerializer.Deserialize<LightInspectionSettings>(SettingsBytes(settings))!;
        return new Sample { RecipeId = recipeId, Station = station, OriginalName = Path.GetFileName(image), FullImageMode = fullImageMode,
            Image = bytes, ImageHash = Hash(bytes), SettingsHash = Hash(SettingsBytes(copy)),
            Settings = copy, CreatedUtc = DateTime.UtcNow };
    }

    internal static Sample Read(string archive, string recipeId, int station, bool? legacyModeFallback = null)
    {
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count != 2) throw new InvalidDataException("樣本還原點內容不完整。");
        var metadata = zip.GetEntry("sample.json") ?? throw new InvalidDataException("缺少樣本資訊。");
        if (metadata.Length > 1024 * 1024) throw new InvalidDataException("樣本資訊過大。");
        Sample sample;
        using (var input = metadata.Open()) sample = JsonSerializer.Deserialize<Sample>(input) ?? throw new InvalidDataException("無法讀取樣本資訊。");
        bool fullImageMode = sample.FullImageMode
            ?? legacyModeFallback
            ?? (!sample.Settings.HasRoi && sample.Settings.HasFullImageRoi);
        sample.FullImageMode = fullImageMode;
        if (sample.Version != 1 || sample.RecipeId != recipeId || sample.Station != station ||
            string.IsNullOrWhiteSpace(sample.OriginalName) || sample.OriginalName != Path.GetFileName(sample.OriginalName))
            throw new InvalidDataException("還原點不屬於目前配方與站別。");
        if (!new[] { ".bmp", ".png", ".jpg", ".jpeg", ".tif", ".tiff" }.Contains(Path.GetExtension(sample.OriginalName).ToLowerInvariant()))
            throw new InvalidDataException("不支援的樣本圖片格式。");
        var image = zip.GetEntry("image" + Path.GetExtension(sample.OriginalName).ToLowerInvariant())
            ?? throw new InvalidDataException("缺少樣本原圖。");
        if (image.Length <= 0 || image.Length > 512L * 1024 * 1024) throw new InvalidDataException("樣本圖片大小無效。");
        using (var input = image.Open())
        using (var bytes = new MemoryStream()) { input.CopyTo(bytes); sample.Image = bytes.ToArray(); }
        sample.Settings.Validate();
        sample.Settings.EffectiveLimits(fullImageMode);
        bool hasModeRoi = fullImageMode ? sample.Settings.HasFullImageRoi : sample.Settings.HasRoi;
        if (!hasModeRoi || Hash(sample.Image) != sample.ImageHash || Hash(SettingsBytes(sample.Settings)) != sample.SettingsHash)
            throw new InvalidDataException("樣本還原點校驗失敗，未修改目前設定。");
        return sample;
    }

    // Call inside FileTransaction, so failed archive publication rolls back the active sample too.
    internal static bool Save(string directory, Sample sample, Action<Sample, string> apply)
    {
        ImportLegacyArchives(directory, sample.RecipeId, sample.Station, sample.FullImageMode == true);
        Directory.CreateDirectory(directory);
        string? latest = Directory.GetFiles(directory, "sample-*.zip").OrderByDescending(Path.GetFileName, StringComparer.Ordinal).FirstOrDefault();
        bool duplicate = false;
        if (latest != null)
        {
            var previous = Read(latest, sample.RecipeId, sample.Station, sample.FullImageMode);
            duplicate = previous.ImageHash == sample.ImageHash && previous.SettingsHash == sample.SettingsHash;
        }
        string pending = Path.Combine(directory, ".pending-" + Guid.NewGuid().ToString("N"));
        try
        {
            if (!duplicate)
            {
                using (var zip = ZipFile.Open(pending, ZipArchiveMode.Create))
                {
                    using (var output = zip.CreateEntry("sample.json").Open()) JsonSerializer.Serialize(output, sample);
                    using (var output = zip.CreateEntry("image" + Path.GetExtension(sample.OriginalName).ToLowerInvariant(), CompressionLevel.Optimal).Open())
                        output.Write(sample.Image, 0, sample.Image.Length);
                }
                Read(pending, sample.RecipeId, sample.Station, sample.FullImageMode);
            }
            Apply(sample, directory, apply);
            if (!duplicate) File.Move(pending, Path.Combine(directory, "sample-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N") + ".zip"));
            return !duplicate;
        }
        finally { if (File.Exists(pending)) File.Delete(pending); }
    }

    internal static void Apply(Sample sample, string directory, Action<Sample, string> apply)
    {
        string temp = Path.Combine(directory, ".apply-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        string path = Path.Combine(temp, "_light-standard" + Path.GetExtension(sample.OriginalName).ToLowerInvariant());
        try { File.WriteAllBytes(path, sample.Image); apply(sample, path); }
        finally { if (File.Exists(path)) File.Delete(path); Directory.Delete(temp); }
    }

    internal static string Prune(string directory)
    {
        try
        {
            foreach (string path in Directory.GetFiles(directory, "sample-*.zip").OrderByDescending(Path.GetFileName, StringComparer.Ordinal).Skip(Retention)) File.Delete(path);
            return "";
        }
        catch (IOException) { return "；較舊還原點目前被占用，將於下次儲存時清理。"; }
        catch (UnauthorizedAccessException) { return "；無法清理較舊還原點，請檢查資料夾權限。"; }
    }

    /// <summary>Short-lived rollback stays in RAM; only successful sample points persist.</summary>
    internal static void FileTransaction(IEnumerable<string> paths, Action action)
    {
        var before = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(p => p,
            p => File.Exists(p) ? File.ReadAllBytes(p) : null, StringComparer.OrdinalIgnoreCase);
        try { action(); }
        catch (Exception failure)
        {
            var errors = new List<Exception> { failure };
            foreach (var file in before)
            {
                try
                {
                    if (file.Value == null) { if (File.Exists(file.Key)) File.Delete(file.Key); }
                    else WriteAtomic(file.Key, file.Value);
                }
                catch (Exception rollback) { errors.Add(rollback); }
            }
            if (errors.Count > 1) throw new AggregateException("儲存失敗，且部分檔案無法回復。", errors);
            throw;
        }
    }

    internal static void WriteAtomic(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temp, bytes);
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
