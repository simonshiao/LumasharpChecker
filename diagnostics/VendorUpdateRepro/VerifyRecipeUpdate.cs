using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using LumasharpChecker.model;

internal static class VerifyRecipeUpdate
{
    private static string appDirectory;

    private static void Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Provide the application directory.");
        appDirectory = Path.GetFullPath(args[0]);
        AppDomain.CurrentDomain.AssemblyResolve += ResolveApplicationAssembly;
        Run();
    }

    private static Assembly ResolveApplicationAssembly(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name;
        foreach (string extension in new[] { ".dll", ".exe" })
        {
            string path = Path.Combine(appDirectory, name + extension);
            if (File.Exists(path)) return Assembly.LoadFrom(path);
        }
        return null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "LumasharpUpdateVerify-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "source", "sample");
        string work = Path.Combine(root, "work");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "Setting.ini"), "new setting");
        File.WriteAllText(Path.Combine(source, "station1.ini"), "new station");
        var manager = new RecipeManager(work, Path.Combine(root, "library"));
        var recipe = manager.CreateRecipe("sample", 8);
        recipe.Stations[0].IsConfigured = true;
        manager.SaveManifest(recipe);
        string recipeDirectory = Path.Combine(work, "recipes", recipe.Id);
        string stationFile = Path.Combine(recipeDirectory, "station01", "profile.json");
        File.WriteAllText(stationFile, "synthetic station configuration");
        string active = Path.Combine(recipeDirectory, "vendor-source");
        Directory.CreateDirectory(active);
        File.WriteAllText(Path.Combine(active, "Setting.ini"), "old setting");
        File.WriteAllText(Path.Combine(active, "old.txt"), "old snapshot");
        manager.UpdateVendorSnapshot(recipe, source);
        string[] backups = Directory.GetDirectories(recipeDirectory, ".vendor-backup-*");
        bool ok = backups.Length == 1
            && File.ReadAllText(Path.Combine(backups[0], "old.txt")) == "old snapshot"
            && File.ReadAllText(Path.Combine(active, "Setting.ini")) == "new setting"
            && !File.Exists(Path.Combine(active, "old.txt"))
            && File.ReadAllText(stationFile) == "synthetic station configuration"
            && manager.LoadAllRecipes().Single().Stations[0].IsConfigured
            && !manager.CompareVendorSnapshot(source, recipe).HasChanges;
        Console.WriteLine("Test directory: " + root);
        Console.WriteLine(ok ? "PASS: snapshot updated; backup and station settings retained" : "FAIL");
        if (!ok) Environment.ExitCode = 1;
    }
}
