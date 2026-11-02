// Isolated reproduction of the vendor snapshot update using synthetic files only.
// Run only in an approved test environment with the endpoint protection enabled.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Linq;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length != 1 || args[0] != "--run")
        {
            Console.WriteLine("Supply --run to create and update synthetic test data in a new temporary directory.");
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), "LumasharpVendorRepro-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "source");
        string recipe = Path.Combine(root, "recipe");
        string active = Path.Combine(recipe, "vendor-source");
        string stage = Path.Combine(recipe, ".vendor-stage-test");
        string backup = Path.Combine(recipe, ".vendor-backup-test");
        Console.WriteLine("Test directory: " + root);
        Console.WriteLine("Only synthetic files within this directory are used.");

        CreateFixture(source, 21, 2);
        CreateFixture(active, 21, 1);
        File.WriteAllText(Path.Combine(recipe, "recipe.json"), "{\"synthetic\":true,\"version\":1}");
        CopyAndVerify(source, stage);
        Directory.Move(active, backup);
        Directory.Move(stage, active);
        File.WriteAllText(Path.Combine(recipe, "recipe.json.tmp"), "{\"synthetic\":true,\"version\":2}");
        File.Replace(Path.Combine(recipe, "recipe.json.tmp"), Path.Combine(recipe, "recipe.json"), null);
        Console.WriteLine("Deleting the synthetic old snapshot: " + backup);
        Directory.Delete(backup, true);
        Console.WriteLine("Completed. Test files remain at: " + root);
    }

    private static void CreateFixture(string root, int count, byte value)
    {
        Directory.CreateDirectory(Path.Combine(root, "Pattern"));
        for (int i = 0; i < count; i++)
        {
            string path = Path.Combine(root, "Pattern", "test-" + i.ToString("00") + ".bmp");
            byte[] data = new byte[4096];
            data[0] = (byte)'B'; data[1] = (byte)'M'; data[2] = value; data[3] = (byte)i;
            File.WriteAllBytes(path, data);
        }
    }

    private static void CopyAndVerify(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
        {
            string target = Path.Combine(destination, Path.GetFileName(file));
            File.Copy(file, target);
            using (var sha = SHA256.Create())
            using (var a = File.OpenRead(file))
            using (var b = File.OpenRead(target))
                if (!sha.ComputeHash(a).SequenceEqual(sha.ComputeHash(b)))
                    throw new IOException("Copy verification failed: " + file);
        }
        foreach (string dir in Directory.GetDirectories(source))
            CopyAndVerify(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }
}
