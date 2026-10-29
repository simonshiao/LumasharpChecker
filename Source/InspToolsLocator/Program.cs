using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InspTools.AOI;
using InspTools.Station;
using OpenCvSharp;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "detect")
        {
            Environment.Exit(NativeDetection.Run(args));
            return;
        }
        try
        {
            if (args.Length != 4 || args[0] != "locate" || !File.Exists(args[1]))
                throw new FileNotFoundException("無法讀取 Luma 目前的圖片。", args.Length > 1 ? args[1] : "");
            int stationNumber;
            if (!int.TryParse(args[3], out stationNumber) || stationNumber < 1 || stationNumber > 8)
                throw new ArgumentOutOfRangeException("station", "原廠定位目前僅支援第 1 至 8 站。");
            string source = Path.GetFullPath(args[2]);
            if (!File.Exists(Path.Combine(source, "station" + stationNumber + ".ini")))
                throw new FileNotFoundException("此站缺少原廠定位參數。");
            string isolatedName = "luma_preview_" + Guid.NewGuid().ToString("N");
            string isolatedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Recipe", isolatedName);
            CopySnapshot(source, isolatedPath);
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var app = new Application();
            using (var image = Cv2.ImDecode(File.ReadAllBytes(args[1]), ImreadModes.Color))
            {
                if (image.Empty()) throw new InvalidDataException("圖片解碼失敗。");
                var station = new CVInspection();
                station.SetStationIndex(stationNumber - 1);
                string error = "";
                station.SetRecipe(isolatedName, stationNumber - 1, ref error);
                if (!string.IsNullOrWhiteSpace(error)) throw new InvalidDataException(error);
                station.SetBuffers(image);
                var dialog = new LocateWindow();
                dialog.SetInspection(station);
                // The vendor SetBuffer automatically runs AI pre-location. The AI
                // runtime is not attached in this preview: suppress only that call,
                // restore the recipe setting, and refresh the displayed controls.
                bool ai = station.paramFile.LocateParam.EnableAILocate;
                station.paramFile.LocateParam.EnableAILocate = false;
                try { dialog.SetBuffer(image); }
                finally { station.paramFile.LocateParam.EnableAILocate = ai; }
                dialog.SetPreviewMode(true);
                dialog.Title = "原廠定位設定預覽（副本，不保存）";
                dialog.Loaded += (_, __) =>
                {
                    MakeReadOnly(dialog);
                    // The vendor window defaults to OK before any inspection.
                    // This is a read-only preview, so never present that as a result.
                    var result = dialog.FindName("Label_Result") as Label;
                    if (result != null) result.Content = "未測試";
                };
                app.Run(dialog);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, "InspTools 定位", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        // Some vendor components own worker threads; terminate this isolated host on close.
        Environment.Exit(0);
    }

    private static void CopySnapshot(string source, string destination)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("原廠資料包含連結，無法開啟。");
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("原廠資料包含連結，無法開啟。");
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), false);
        }
        foreach (string dir in Directory.GetDirectories(source))
            CopySnapshot(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    private static void MakeReadOnly(DependencyObject node)
    {
        // Tabs remain selectable, but the vendor buttons can train models,
        // change files or launch tests against paths stored in the source INI.
        var button = node as Button;
        if (button != null) button.IsEnabled = false;
        var check = node as CheckBox;
        if (check != null) check.IsEnabled = false;
        var radio = node as RadioButton;
        if (radio != null) radio.IsEnabled = false;
        var combo = node as ComboBox;
        if (combo != null) combo.IsEnabled = false;
        var text = node as TextBox;
        if (text != null) text.IsReadOnly = true;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            MakeReadOnly(VisualTreeHelper.GetChild(node, i));
    }
}
