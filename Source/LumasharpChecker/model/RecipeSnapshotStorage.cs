using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace LumasharpChecker.model;

/// <summary>Verified directory replacement; rollback copies exist only during the transaction.</summary>
internal static class RecipeSnapshotStorage
{
    private static Dictionary<string, string> Inventory(string root)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Visit(root, root, files);
        return files;
    }

    private static void Visit(string root, string directory, Dictionary<string, string> files)
    {
        RejectLink(directory);
        foreach (string path in Directory.EnumerateFiles(directory))
        {
            RejectLink(path);
            using var sha = SHA256.Create();
            using var input = File.OpenRead(path);
            files.Add(path.Substring(root.Length + 1), Convert.ToBase64String(sha.ComputeHash(input)));
        }
        foreach (string child in Directory.EnumerateDirectories(directory)) Visit(root, child, files);
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("配方副本含連結，無法安全同步：" + path);
    }

    private static bool Equal(Dictionary<string, string> first, Dictionary<string, string> second)
        => first.Count == second.Count && first.All(file => second.TryGetValue(file.Key, out string? hash) && hash == file.Value);

    internal static bool Replace(string source, string destination, Action commit)
    {
        source = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar);
        destination = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar);
        if (source.Equals(destination, StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            destination.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("配方來源與目標不可互相包含。");
        var expected = Inventory(source);
        if (Directory.Exists(destination) && Equal(expected, Inventory(destination))) { commit(); return false; }
        string stage = destination + ".transaction-stage-" + Guid.NewGuid().ToString("N");
        string rollback = destination + ".transaction-rollback-" + Guid.NewGuid().ToString("N");
        bool movedOld = false, movedNew = false;
        try
        {
            Directory.CreateDirectory(stage);
            foreach (string relative in expected.Keys)
            {
                string target = Path.Combine(stage, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(source, relative), target);
            }
            if (!Equal(expected, Inventory(stage))) throw new IOException("配方暫存副本校驗失敗。");
            if (Directory.Exists(destination)) { Directory.Move(destination, rollback); movedOld = true; }
            Directory.Move(stage, destination); movedNew = true;
            if (!Equal(expected, Inventory(destination))) throw new IOException("配方同步後校驗失敗。");
            commit();
        }
        catch
        {
            if (movedNew && Directory.Exists(destination)) Directory.Move(destination, stage);
            if (movedOld && Directory.Exists(rollback)) Directory.Move(rollback, destination);
            throw;
        }
        finally { Cleanup(stage); }
        Cleanup(rollback);
        return true;
    }

    private static void Cleanup(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return;
            // Revalidate immediately before recursively removing only this transaction's own directory.
            Inventory(directory);
            Directory.Delete(directory, true);
        }
        catch (IOException ex) { System.Diagnostics.Trace.WriteLine("配方交易暫存尚未清除：" + ex.Message); }
        catch (UnauthorizedAccessException ex) { System.Diagnostics.Trace.WriteLine("配方交易暫存尚未清除：" + ex.Message); }
    }
}
