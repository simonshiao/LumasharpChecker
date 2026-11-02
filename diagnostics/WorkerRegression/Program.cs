using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Text;
using System.Web.Script.Serialization;

static class Program
{
    static Type Bridge; static object Recipe; static string Root;
    static object Get(object value, string property) => value.GetType().GetProperty(property).GetValue(value);
    static void Set(object value, string property, object data) => value.GetType().GetProperty(property).SetValue(value, data);
    static void Check(bool value, string name) { if (!value) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); }
    static object Inspect(string image, int station = 1, string selection = "111", string stop = "face")
    {
        var task = (Task)Bridge.GetMethod("RunPreLightAsync").Invoke(null, new object[] { Recipe, station, image, Root, stop, selection });
        task.GetAwaiter().GetResult(); return task.GetType().GetProperty("Result").GetValue(task);
    }
    static void Stop() => Bridge.GetMethod("ShutdownWorker").Invoke(null, null);
    static bool Throws(Action action) { try { action(); return false; } catch { return true; } }
    static double Median(IEnumerable<double> values) { var a = values.OrderBy(x => x).ToArray(); return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2; }
    static Dictionary<string, object> Baseline(string image, string output)
    {
        string native = Path.Combine(Root, "NativeR3");
        var start = new ProcessStartInfo(Path.Combine(native, "LegacyFlowHost.exe"), "LUMA-22222222222222222222222222222222 \"" + image + "\" \"" + output + "\" 1 face 111")
            { WorkingDirectory = native, UseShellExecute = false, CreateNoWindow = true };
        using (var p = Process.Start(start)) { if (!p.WaitForExit(15000)) { p.Kill(); throw new TimeoutException("Baseline timed out"); } }
        return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(output));
    }
    static (string Hash, double Brightness) Pixels(string file, Rectangle roi)
    {
        using (var image = new Bitmap(file))
        {
            var data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                byte[] pixels = new byte[image.Width * image.Height * 3]; int width = image.Width;
                for (int y = 0; y < image.Height; y++) Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * width * 3, width * 3);
                long total = 0;
                for (int y = roi.Top; y < roi.Bottom; y++) for (int x = roi.Left; x < roi.Right; x++)
                { int i = (y * width + x) * 3; total += Math.Max(pixels[i], Math.Max(pixels[i + 1], pixels[i + 2])); }
                using (var sha = SHA256.Create()) return (Convert.ToBase64String(sha.ComputeHash(pixels)), total / (double)(roi.Width * roi.Height));
            }
            finally { image.UnlockBits(data); }
        }
    }
    [STAThread]
    static int Main(string[] args)
    {
        try { Run(args); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex.GetBaseException()); return 1; }
        finally { if (Bridge != null) Stop(); }
    }
    static void Run(string[] args)
    {
        var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0])); Root = Path.GetFullPath(args[1]);
        Bridge = assembly.GetType("LumasharpChecker.model.NativeRecipeBridge", true);
        Recipe = Activator.CreateInstance(assembly.GetType("LumasharpChecker.model.RecipeManager+RecipeManifest", true));
        Set(Recipe, "Id", "22222222222222222222222222222222"); Set(Recipe, "Name", "worker regression"); Set(Recipe, "LocalNativeRecipeName", "LUMA-22222222222222222222222222222222");
        var files = Directory.GetFiles(args[2]).Where(p => Path.GetExtension(p).Equals(".jpg", StringComparison.OrdinalIgnoreCase)).OrderBy(p => p).ToArray();
        var results = new List<Dictionary<string, object>>(); int pid = 0;
        foreach (string image in files)
        {
            string output = Path.Combine(Root, "NativeR3", "LumaProbe", "baseline-" + Guid.NewGuid().ToString("N") + ".json");
            var watch = Stopwatch.StartNew(); var old = Baseline(image, output); watch.Stop(); double oldWall = watch.Elapsed.TotalMilliseconds;
            var report = Inspect(image); int current = (int)Get(report, "WorkerProcessId");
            if (pid != 0) Check(current == pid && (bool)Get(report, "RecipeReused"), "same process/Recipe reused " + Path.GetFileName(image));
            pid = current;
            foreach (var pair in new[] { ("exit", "Exit"), ("x", "X"), ("y", "Y"), ("width", "Width"), ("height", "Height"), ("faceIndex", "FaceIndex") })
            {
                if (!old.ContainsKey(pair.Item1)) continue;
                Check(Convert.ToInt32(old[pair.Item1]) == Convert.ToInt32(Get(report, pair.Item2)), "unchanged " + pair.Item2);
            }
            foreach (var pair in new[] { ("locate", "Locate"), ("selectWay", "SelectWay"), ("selectFace", "SelectFace"), ("selectWayDetail", "SelectWayDetail") })
                Check(Convert.ToString(old[pair.Item1]) == Convert.ToString(Get(report, pair.Item2)), "unchanged " + pair.Item2);
            var roi = new Rectangle((int)Get(report, "X") + 10, (int)Get(report, "Y") + 10, (int)Get(report, "Width") - 20, (int)Get(report, "Height") - 20);
            var before = Pixels((string)old["outputImage"], roi); var after = Pixels((string)Get(report, "OutputImage"), roi);
            Check(before.Hash == after.Hash && before.Brightness == after.Brightness, "identical decoded pixels / HSV V brightness");
            results.Add(new Dictionary<string, object> { ["image"] = Path.GetFileName(image), ["oldLocateMs"] = old["locateMs"], ["newLocateMs"] = Get(report, "LocateMs"), ["oldWallMs"] = oldWall,
                ["newTotalMs"] = Get(report, "TotalMs"), ["workerMs"] = Get(report, "WorkerMs"), ["brightness"] = after.Brightness, ["workerPid"] = pid, ["reused"] = Get(report, "RecipeReused") });
        }
        var full = Inspect(files[0]); var skipped = Inspect(files[0], selection: "100");
        Check((string)Get(skipped, "SelectWay") == "SKIPPED_RECIPE_OFF" && (string)Get(skipped, "SelectFace") == "SKIPPED_RECIPE_OFF", "tool overrides do not retain previous flags");
        var again = Inspect(files[0]); Check((string)Get(again, "SelectWay") == (string)Get(full, "SelectWay") && Equals(Get(again, "X"), Get(full, "X")), "flip geometry restored between requests");
        var station2 = Inspect(files[0], station: 2); Check((int)Get(station2, "Station") == 2 && !(bool)Get(station2, "RecipeReused"), "station switch reloads context");
        var station1 = Inspect(files[0]); Check(!(bool)Get(station1, "RecipeReused"), "switching back reloads correct station");
        string source = Path.Combine(Root, "recipes", "22222222222222222222222222222222", "vendor-source", "station1.ini");
        string native = Path.Combine(Root, "NativeR3", "Recipe", "LUMA-22222222222222222222222222222222", "station1.ini");
        byte[] original = File.ReadAllBytes(native); DateTime stamp = File.GetLastWriteTimeUtc(native);
        byte[] corrupt = (byte[])original.Clone(); corrupt[corrupt.Length - 1] ^= 1; File.WriteAllBytes(native, corrupt); File.SetLastWriteTimeUtc(native, stamp);
        Check(Throws(() => Inspect(files[0])), "content mismatch rejected despite identical length/timestamp"); File.WriteAllBytes(native, original);
        string text = File.ReadAllText(source);
        string changed = text.Replace("LocateWidthMax = 99999", "LocateWidthMax = 0"); Check(changed != text, "reload fixture changes a real inspection limit");
        var encoding = original[0] == 0xff && original[1] == 0xfe ? Encoding.Unicode : Encoding.UTF8;
        File.WriteAllText(source, changed, encoding); File.Copy(source, native, true);
        var revised = Inspect(files[0]); Check(!(bool)Get(revised, "RecipeReused") && (string)Get(revised, "Locate") == "FAIL", "changed recipe reloads new limit and fails instead of using stale parameters");
        File.WriteAllBytes(source, original); File.WriteAllBytes(native, original);
        var serialA = (Task)Bridge.GetMethod("RunPreLightAsync").Invoke(null, new object[] { Recipe, 1, files[0], Root, "face", "111" });
        var serialB = (Task)Bridge.GetMethod("RunPreLightAsync").Invoke(null, new object[] { Recipe, 1, files[1], Root, "face", "111" });
        Task.WhenAll(serialA, serialB).GetAwaiter().GetResult();
        var a = serialA.GetType().GetProperty("Result").GetValue(serialA); var b = serialB.GetType().GetProperty("Result").GetValue(serialB);
        Check((string)Get(a, "Image") == Path.GetFileName(files[0]) && (string)Get(b, "Image") == Path.GetFileName(files[1]), "concurrent requests remain serialized and correlated");
        string bad = Path.Combine(Root, "bad.jpg"); File.WriteAllText(bad, "not an image");
        Check(Throws(() => Inspect(bad)), "invalid frame reports failure without a successful stale result");
        var recovered = Inspect(files[0]); Check(!(bool)Get(recovered, "RecipeReused"), "worker restarts after native infrastructure failure");
        int killed = (int)Get(recovered, "WorkerProcessId"); using (var p = Process.GetProcessById(killed)) { p.Kill(); p.WaitForExit(); }
        var restarted = Inspect(files[0]); Check((int)Get(restarted, "WorkerProcessId") != killed && !(bool)Get(restarted, "RecipeReused"), "terminated worker recovers on next request");
        int finalPid = (int)Get(restarted, "WorkerProcessId");
        using (var p = Process.GetProcessById(finalPid))
        {
            p.Refresh(); long startMemory = p.PrivateMemorySize64, peak = startMemory;
            for (int i = 0; i < 100; i++)
            {
                var frame = Inspect(files[i % files.Length]); Check((int)Get(frame, "Exit") == 0, "stress frame " + i);
                p.Refresh(); peak = Math.Max(peak, p.PrivateMemorySize64);
                if (peak - startMemory > 512L * 1024 * 1024) throw new Exception("Native memory growth exceeds 512 MB in 100 frames");
            }
            File.WriteAllText(Path.Combine(Root, "memory.json"), new JavaScriptSerializer().Serialize(new { frames = 100, startPrivate = startMemory, peakPrivate = peak, endPrivate = p.PrivateMemorySize64 }));
            Console.WriteLine($"MEMORY start={startMemory/1024/1024} MB peak={peak/1024/1024} MB end={p.PrivateMemorySize64/1024/1024} MB");
            Stop(); Check(p.WaitForExit(3000), "explicit shutdown leaves no worker");
        }
        string host = Path.Combine(Root, "NativeR3", "LumaNativeFlowHost.exe"), savedHost = host + ".regression-backup";
        File.Copy(host, savedHost, true);
        try
        {
            File.Copy(args[3], host, true); File.WriteAllText(Path.Combine(Root, "NativeR3", "fault-mode.txt"), "stale");
            Check(Throws(() => Inspect(files[0])), "stale protocol response rejected");
            File.WriteAllText(Path.Combine(Root, "NativeR3", "fault-mode.txt"), "hang");
            var timeout = Stopwatch.StartNew(); bool timedOut = false;
            try { Inspect(files[0]); } catch (TimeoutException) { timedOut = true; }
            Check(timedOut && timeout.Elapsed.TotalSeconds < 30, "hung worker times out and is terminated");
        }
        finally { Stop(); File.Copy(savedHost, host, true); }
        Check((int)Get(Inspect(files[0]), "Exit") == 0, "native processing recovers after timeout and protocol rejection"); Stop();
        File.WriteAllText(Path.Combine(Root, "results.json"), new JavaScriptSerializer().Serialize(results));
        Console.WriteLine($"PERF old locate median={Median(results.Select(r => Convert.ToDouble(r["oldLocateMs"]))):F2} ms; warm new={Median(results.Skip(1).Select(r => Convert.ToDouble(r["newLocateMs"]))):F2} ms; old wall={Median(results.Select(r => Convert.ToDouble(r["oldWallMs"]))):F1} ms; warm total={Median(results.Skip(1).Select(r => Convert.ToDouble(r["newTotalMs"]))):F1} ms");
    }
}
