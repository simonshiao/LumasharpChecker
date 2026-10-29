using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using InspTools.Station;
using OpenCvSharp;

internal static class NativeDetection
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr AddDllDirectory(string path);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string path, IntPtr reserved, uint flags);

    internal static int Run(string[] args)
    {
        if (args.Length != 5) return 2;
        var output = new XElement("location");
        try
        {
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            foreach (string dir in new[] { "DeltaAIProcess", "CUDA11_118" })
                if (AddDllDirectory(Path.Combine(Environment.CurrentDirectory, "Runtime", dir)) == IntPtr.Zero)
                    throw new IOException("定位執行環境缺失：" + dir);
            if (LoadLibraryEx(Path.Combine(Environment.CurrentDirectory, "Runtime", "DeltaAIProcess", "DeltaAIProcessDll.dll"), IntPtr.Zero, 0x1100) == IntPtr.Zero)
                throw new IOException("無法載入定位執行庫。");
            int index = int.Parse(args[3]) - 1;
            if (index < 0 || index > 7) throw new ArgumentOutOfRangeException("station");
            string name = args[2];
            if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name || name.Contains("..")) throw new ArgumentException("Recipe 名稱無效。");
            if (!File.Exists(Path.Combine("Recipe", name, "station" + (index + 1) + ".ini"))) throw new FileNotFoundException("定位參數未部署。");
            var station = new CVInspection(); station.SetStationIndex(index);
            string error = ""; station.SetRecipe(name, index, ref error);
            if (error != "") throw new InvalidDataException(error);
            using (var image = Cv2.ImDecode(File.ReadAllBytes(args[1]), ImreadModes.Color))
            {
                if (image.Empty()) throw new InvalidDataException("圖片無法解碼。");
                station.SetBuffers(image);
                var timer = Stopwatch.StartNew();
                if (station.paramFile.LocateParam.EnableAILocate && !station.locateTool.UseAIPreLocate(image, true)) throw new InvalidOperationException("AI 前定位失敗。");
                bool success;
                switch (station.paramFile.LocateParam.LocateType)
                {
                    case 0: success = station.locateTool.SimpleLocate(); break;
                    case 1: success = station.locateTool.QuadLocate(); break;
                    case 2: success = station.locateTool.PadLocate(); break;
                    default: throw new InvalidDataException("不支援的定位類型。");
                }
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
