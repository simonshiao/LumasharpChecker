using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using LumasharpChecker.model;

internal static class RecipeManager
{
    public static string GetStationDirectoryName(int station) => "station" + station.ToString("D2");
}

internal static class Program
{
    private static string Hash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(bytes));
    }

    private static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception ex)
        {
            Console.Error.WriteLine("DIAGNOSTIC_ERROR=" + ex.GetType().Name + " " + ex.Message);
            Environment.ExitCode = 1;
        }
    }

    private static void Run(string[] args)
    {
        bool commitVerified = args.Length == 3 &&
            string.Equals(args[0], "--commit-verified", StringComparison.Ordinal);
        if ((commitVerified && args.Length != 3) || (!commitVerified && args.Length != 2))
        {
            Console.Error.WriteLine("Usage: Net472StandardRecoveryChecks.exe [--commit-verified] <existing recipe path> <station>");
            Environment.ExitCode = 2;
            return;
        }

        string sourceRoot = Path.GetFullPath(args[commitVerified ? 1 : 0]);
        int station = int.Parse(args[commitVerified ? 2 : 1]);
        string id = Path.GetFileName(sourceRoot.TrimEnd(Path.DirectorySeparatorChar));
        // net472 檔案 API 仍會受 MAX_PATH 影響；使用短測試根目錄，不改動正式 Recipe。
        string fixture = Path.Combine(Path.GetTempPath(), "lr-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        string clone = Path.Combine(fixture, "App", "recipes", id);
        string sourceStation = Path.Combine(sourceRoot, RecipeManager.GetStationDirectoryName(station));
        string cloneStation = Path.Combine(clone, RecipeManager.GetStationDirectoryName(station));
        Directory.CreateDirectory(cloneStation);
        string originalActive = Path.Combine(sourceStation, "_light-standard-fullimage.png");
        string originalSettings = Path.Combine(sourceStation, "light-inspection.json");
        string beforeActiveHash = Hash(File.ReadAllBytes(originalActive));
        string beforeSettingsHash = Hash(File.ReadAllBytes(originalSettings));
        Console.WriteLine("SOURCE_READONLY=" + sourceRoot);
        Console.WriteLine("SOURCE_STATION_EXISTS=" + Directory.Exists(sourceStation));

        foreach (string file in Directory.GetFiles(sourceStation))
        {
            string name = Path.GetFileName(file);
            if (name.StartsWith("_light-standard", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("light-inspection.json", StringComparison.OrdinalIgnoreCase))
                File.Copy(file, Path.Combine(cloneStation, name));
        }
        foreach (bool mode in new[] { false, true })
        {
            string history = LightSampleHistory.Folder(sourceRoot, station, mode);
            if (!Directory.Exists(history)) continue;
            string cloneHistory = LightSampleHistory.Folder(clone, station, mode);
            Directory.CreateDirectory(cloneHistory);
            foreach (string file in Directory.GetFiles(history, "sample-*.zip"))
                File.Copy(file, Path.Combine(cloneHistory, Path.GetFileName(file)));
            foreach (string apply in Directory.GetDirectories(history, ".apply-*"))
            {
                string dest = Path.Combine(cloneHistory, Path.GetFileName(apply));
                Directory.CreateDirectory(dest);
                foreach (string file in Directory.GetFiles(apply))
                    File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
            }
        }
        Console.WriteLine("RUNTIME=" + Environment.Version + " FRAMEWORK=net472");
        Console.WriteLine("PRODUCTION_RESIDUES_READONLY=" + StandardSampleRecovery.FindResidues(sourceRoot, station).Count);
        Console.WriteLine("CLONE_RESIDUES_BEFORE=" + StandardSampleRecovery.FindResidues(clone, station).Count);
        string[] standard = Directory.GetFiles(cloneStation, "_light-standard-fullimage.*")
            .Where(f => new[] { ".png", ".bmp", ".jpg", ".jpeg", ".tif", ".tiff" }
                .Contains(Path.GetExtension(f).ToLowerInvariant())).ToArray();
        string activeHash = standard.Length == 1 ? Hash(File.ReadAllBytes(standard[0])) : "NONE";
        var currentSettings = JsonSerializer.Deserialize<LightInspectionSettings>(
            File.ReadAllText(Path.Combine(cloneStation, "light-inspection.json")))!;
        string currentHash = Hash(JsonSerializer.SerializeToUtf8Bytes(currentSettings));
        string archiveFolder = LightSampleHistory.Folder(clone, station, true);
        foreach (string archive in Directory.GetFiles(archiveFolder, "sample-*.zip"))
        {
            try
            {
                var sample = LightSampleHistory.Read(archive, id, station, true);
                Console.WriteLine("VALID_ZIP=" + Path.GetFileName(archive) +
                    " IMAGE_MATCH=" + (sample.ImageHash == activeHash) +
                    " SETTINGS_MATCH=" + (sample.SettingsHash == currentHash));
                var differences = typeof(LightInspectionSettings).GetProperties()
                    .Where(property =>
                    {
                        var currentValue = property.GetValue(currentSettings);
                        var savedValue = property.GetValue(sample.Settings);
                        return !Equals(currentValue, savedValue);
                    })
                    .Select(property => property.Name + "=" + property.GetValue(currentSettings) +
                        " (history=" + property.GetValue(sample.Settings) + ")")
                    .ToArray();
                Console.WriteLine("SETTINGS_DIFFERENCES=" +
                    (differences.Length == 0 ? "NONE" : string.Join("; ", differences)));
            }
            catch (Exception ex)
            {
                Console.WriteLine("ZIP_REJECTED=" + Path.GetFileName(archive) +
                    " REASON=" + ex.GetBaseException().Message);
            }
        }
        var result = StandardSampleRecovery.RepairVerifiedOrphans(clone, id, station);
        Console.WriteLine("CLONE_RECOVERED=" + result.RecoveredFiles);
        foreach (string reason in result.Issues) Console.WriteLine("CLONE_ISSUE=" + reason);
        Console.WriteLine("CLONE_RESIDUES_AFTER=" + StandardSampleRecovery.FindResidues(clone, station).Count);
        Console.WriteLine("CLONE_PATH=" + fixture);
        if (commitVerified)
        {
            var originalResidues = StandardSampleRecovery.FindResidues(sourceRoot, station);
            if (result.Issues.Count > 0 || result.RecoveredFiles != originalResidues.Count ||
                originalResidues.Count != 3 ||
                Enumerable.Range(1, station - 1).Any(s =>
                    StandardSampleRecovery.FindResidues(sourceRoot, s).Count > 0) ||
                StandardSampleRecovery.FindResidues(clone, station).Count != 0 ||
                beforeActiveHash != Hash(File.ReadAllBytes(originalActive)) ||
                beforeSettingsHash != Hash(File.ReadAllBytes(originalSettings)))
            {
                Console.Error.WriteLine("REAL_RECOVERY_ABORTED: clone validation or source hashes changed.");
                Environment.ExitCode = 4;
                return;
            }
            // 只走正式 Recovery 已有的 SHA-256 備份再移除流程，禁止略過或降級任何檢查。
            var actual = StandardSampleRecovery.RepairVerifiedOrphans(sourceRoot, id, station);
            Console.WriteLine("REAL_RECOVERED=" + actual.RecoveredFiles);
            foreach (string issue in actual.Issues)
                Console.WriteLine("REAL_ISSUE=" + issue);
            Console.WriteLine("REAL_RESIDUES_LEFT=" +
                StandardSampleRecovery.FindResidues(sourceRoot, station).Count);
            Console.WriteLine("REAL_ACTIVE_UNCHANGED=" +
                (beforeActiveHash == Hash(File.ReadAllBytes(originalActive))));
            Console.WriteLine("REAL_SETTINGS_UNCHANGED=" +
                (beforeSettingsHash == Hash(File.ReadAllBytes(originalSettings))));
            if (actual.Issues.Count > 0) Environment.ExitCode = 5;
        }
    }
}
