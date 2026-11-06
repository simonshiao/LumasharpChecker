using System;
using System.IO;
using System.Linq;
using LumasharpChecker.model;

internal static class Program
{
    static void Require(bool ok, string name)
    {
        if (!ok) throw new InvalidOperationException("FAIL " + name);
        Console.WriteLine("PASS " + name);
    }

    static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "lrc-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        try
        {
            string app = Path.Combine(root, "App");
            string library = Path.Combine(app, "profile");
            Directory.CreateDirectory(library);
            var manager = new RecipeManager(app, library);
            var recipe = manager.CreateRecipe("安全清理整合測試", 8);
            string folder = manager.GetProfileBasePath(recipe);
            string station = Path.Combine(folder, "station01");
            string original = Path.Combine(station, "marker.txt");
            File.WriteAllText(original, "ORIGINAL_TEST_DATA");
            string outside = Path.Combine(root, "external-image.png");
            File.WriteAllBytes(outside, new byte[] { 1, 2, 3, 4, 5 });
            Require(manager.LoadAllRecipes().Count == 1, "建立測試配方");
            string archived = manager.MoveRecipeToCleanupArchive(recipe);
            Require(!Directory.Exists(folder) && Directory.Exists(archived), "移出清單且保留全部檔案");
            Require(manager.LoadAllRecipes().Count == 0, "清理後一般配方清單不再列出");
            Require(File.Exists(outside) && File.ReadAllBytes(outside).Length == 5,
                "外部來源沒有被清除或更動");
            var archivedList = manager.ListCleanedRecipes();
            Require(archivedList.Count == 1 && archivedList[0].Id == recipe.Id,
                "保留區依 ID 正確列出");
            var restored = manager.RestoreCleanedRecipe(archivedList.Single());
            Require(restored.Id == recipe.Id && manager.LoadAllRecipes().Count == 1,
                "原 ID 成功還原並重新列出");
            Require(File.ReadAllText(original) == "ORIGINAL_TEST_DATA",
                "還原後內容完整");
            archived = manager.MoveRecipeToCleanupArchive(restored);
            // 同 ID 已存在不可覆蓋，即使目錄不是這個清理流程建立。
            Directory.CreateDirectory(folder);
            try
            {
                manager.RestoreCleanedRecipe(manager.ListCleanedRecipes().Single());
                throw new InvalidOperationException("FAIL 同 ID 被覆蓋");
            }
            catch (IOException)
            {
                Require(Directory.Exists(archived) && Directory.Exists(folder), "同 ID 衝突不覆寫");
            }
            Console.WriteLine("ALL RECIPE CLEANUP CHECKS PASSED");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }
}
