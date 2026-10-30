using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using InspTools.Station;
using OpenCvSharp;

internal static class NativeDetection
{
    internal static int Run(string[] args)
    {
        if (args.Length != 5) return 2;
        var output = new XElement("location");
        try
        {
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            VendorNativeRuntime.Initialize(AppDomain.CurrentDomain.BaseDirectory);
            int index = int.Parse(args[3]) - 1;
            if (index < 0 || index > 7) throw new ArgumentOutOfRangeException("station");
            string name = args[2];
            if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name || name.Contains("..")) throw new ArgumentException("Recipe 名稱無效。");
            if (!File.Exists(Path.Combine("Recipe", name, "station" + (index + 1) + ".ini"))) throw new FileNotFoundException("定位參數未部署。");
            var station = new CVInspection(); station.SetStationIndex(index);
            string error = ""; station.SetRecipe(name, index, ref error);
            if (error != "") throw new InvalidDataException(error);
            VendorAiLocateAccessResult authorization = VendorAiLocateAccess.CheckAuthorization(station);
            bool recipeAiEnabled = station.paramFile.LocateParam.EnableAILocate;
            if (recipeAiEnabled && !authorization.Authorized)
                throw new InvalidOperationException("Recipe 已啟用 AI 定位，但" + authorization.Message + "；已停止本次定位。");
            using (var image = Cv2.ImDecode(File.ReadAllBytes(args[1]), ImreadModes.Color))
            {
                if (image.Empty()) throw new InvalidDataException("圖片無法解碼。");
                station.SetBuffers(image);
                if (recipeAiEnabled)
                {
                    string modelStatus = VendorAiLocateAccess.LoadLocateModel(station);
                    if (!modelStatus.StartsWith("AI 定位模型已載入：", StringComparison.Ordinal))
                        throw new InvalidOperationException(modelStatus);
                }
                var timer = Stopwatch.StartNew();
                // DoLocate follows the original InspTools path: AI pre-location
                // runs only when both Recipe and the vendor authorization allow it.
                bool success = station.locateTool.DoLocate(true);
                timer.Stop();
                if (!success) throw new InvalidOperationException(station.locateTool.ErrorMsg);
                var rect = station.locateTool.LocateRect;
                if (rect.Width <= 0 || rect.Height <= 0) throw new InvalidDataException("定位框無效。");
                string aligned = Path.ChangeExtension(args[4], ".png");
                byte[] bytes;
                if (!Cv2.ImEncode(".png", station.buffers.LocatedSrc, out bytes)) throw new IOException("無法輸出定位後影像。");
                File.WriteAllBytes(aligned, bytes);
                output.Add(new XAttribute("success", true), new XAttribute("x", rect.X), new XAttribute("y", rect.Y), new XAttribute("width", rect.Width), new XAttribute("height", rect.Height), new XAttribute("angle", station.locateTool.LocateAngle), new XAttribute("milliseconds", timer.Elapsed.TotalMilliseconds));
            }
            output.Save(args[4]); return 0;
        }
        catch (Exception ex)
        {
            output = new XElement("location", new XAttribute("success", false), new XAttribute("error", ex.GetBaseException().Message));
            output.Save(args[4]); return 1;
        }
    }
}
