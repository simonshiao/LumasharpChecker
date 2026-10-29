using System.Diagnostics;
using System.IO;
using System.Xml.Linq;

namespace LumasharpChecker.model;

internal static class NativeLocationService
{
    internal sealed class Frame
    {
        public System.Drawing.Rectangle Bounds { get; set; }
        public string ImagePath { get; set; } = "";
    }
    internal static async Task<System.Drawing.Rectangle> LocateAsync(string snapshot, int station, string image) => (await LocateFrameAsync(snapshot, station, image)).Bounds;
    internal static async Task<Frame> LocateFrameAsync(string snapshot, int station, string image)
    {
        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NativeLocator");
        string exe = Path.Combine(root, "InspToolsLocator.exe");
        if (!File.Exists(exe)) throw new FileNotFoundException("尚未部署原廠定位引擎。", exe);
        string name = "luma_" + Guid.NewGuid().ToString("N");
        string recipeDir = Path.Combine(root, "Recipe", name);
        CopyTree(snapshot, recipeDir);
        string resultFile = Path.Combine(recipeDir, "location-result.xml");
        return await Task.Run(() =>
        {
            using var p = Process.Start(new ProcessStartInfo(exe, $"detect \"{image}\" {name} {station} \"{resultFile}\"") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true })
                ?? throw new IOException("無法啟動原廠定位引擎。");
            if (!p.WaitForExit(60000)) { try { p.Kill(); } catch { } throw new TimeoutException("原廠定位逾時。"); }
            if (!File.Exists(resultFile)) throw new IOException("原廠定位沒有回傳結果。");
            var data = XElement.Load(resultFile);
            if (p.ExitCode != 0 || (bool?)data.Attribute("success") != true)
                throw new InvalidOperationException((string?)data.Attribute("error") ?? "原廠定位失敗。");
            string aligned = Path.ChangeExtension(resultFile, ".png");
            if (!File.Exists(aligned)) throw new IOException("定位後影像缺失。");
            return new Frame { Bounds = new System.Drawing.Rectangle((int)data.Attribute("x")!, (int)data.Attribute("y")!, (int)data.Attribute("width")!, (int)data.Attribute("height")!), ImagePath = aligned };
        });
    }

    private static void CopyTree(string source, string destination)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new IOException("定位參數包含連結。");
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("定位參數包含連結。");
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), false);
        }
        foreach (string dir in Directory.EnumerateDirectories(source)) CopyTree(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }
}
