// Read-only measurement in a copied runtime/Recipe. Never point the executable at production AppBase.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;

static class Program
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static object Field(object value, string name) => value.GetType().GetField(name, F).GetValue(value);
    static object Member(object value, string name) => value.GetType().GetProperty(name, F)?.GetValue(value) ?? Field(value, name);
    static void Set(object value, string name, object data) => value.GetType().GetField(name, F).SetValue(value, data);
    static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception ex) { Console.Error.WriteLine(ex.GetBaseException()); Environment.ExitCode = 1; }
    }
    static void Run(string[] args)
    {
        if (args.Length != 5) throw new ArgumentException("recipe image offline repeats output.json");
        string root = AppDomain.CurrentDomain.BaseDirectory;
        if (!File.Exists(Path.Combine(root, "PERFORMANCE_TEST.marker"))) throw new InvalidOperationException("Isolated benchmark marker required");
        Environment.CurrentDirectory = root;
        bool offline = bool.Parse(args[2]); int repeats = int.Parse(args[3]);
        var output = new Dictionary<string, object> { ["recipe"] = args[0], ["image"] = Path.GetFullPath(args[1]), ["offline"] = offline, ["runtime"] = root };
        var entire = Stopwatch.StartNew(); var timer = Stopwatch.StartNew();
        var assembly = Assembly.LoadFrom(Path.Combine(root, "InspTools.dll"));
        var stationType = assembly.GetType("InspTools.Station.CVInspection", true);
        object station = Activator.CreateInstance(stationType); object[] recipeArgs = { args[0], 0, "" };
        stationType.GetMethod("SetRecipe", F).Invoke(station, recipeArgs);
        if (!string.IsNullOrWhiteSpace(Convert.ToString(recipeArgs[2]))) throw new Exception("Recipe warning: " + recipeArgs[2]);
        timer.Stop(); output["recipeLoadMs"] = timer.Elapsed.TotalMilliseconds;
        object param = Field(station, "paramFile"), locateSettings = Field(param, "LocateParam");
        var parameters = new SortedDictionary<string, object>();
        foreach (var field in locateSettings.GetType().GetFields(F))
        {
            var value = field.GetValue(locateSettings);
            parameters[field.Name] = value == null || value is string || value.GetType().IsPrimitive ? value : value.ToString();
        }
        output["locateParameters"] = parameters;
        // Match Luma's non-AI selection override; do not write any Recipe.
        Set(locateSettings, "EnableAILocate", false); if (locateSettings.GetType().IsValueType) Set(param, "LocateParam", locateSettings);
        Set(param, "StationON", true); Set(param, "LocateToolON", true); Set(param, "SelectWayON", true); Set(param, "SelectFaceON", true);
        var cv = Assembly.LoadFrom(Path.Combine(root, "OpenCvSharp.dll")); var cv2 = cv.GetType("OpenCvSharp.Cv2", true);
        var modes = cv.GetType("OpenCvSharp.ImreadModes", true); var matType = cv.GetType("OpenCvSharp.Mat", true);
        output["opencvThreads"] = cv2.GetMethod("GetNumThreads", Type.EmptyTypes)?.Invoke(null, null);
        string[] images = Directory.Exists(args[1]) ? Directory.GetFiles(args[1]).Where(p => new[] { ".jpg", ".jpeg", ".png", ".bmp" }.Contains(Path.GetExtension(p).ToLowerInvariant())).OrderBy(p => p).ToArray() : new[] { args[1] };
        var imread = cv2.GetMethod("ImRead", new[] { typeof(string), modes }); var color = Enum.Parse(modes, "Color");
        timer.Restart(); object mat = imread.Invoke(null, new object[] { images[0], color }); timer.Stop(); output["decodeMs"] = timer.Elapsed.TotalMilliseconds;
        output["imageWidth"] = Member(mat, "Width"); output["imageHeight"] = Member(mat, "Height");
        var buffersMethod = stationType.GetMethod("SetBuffers", new[] { matType }); var locate = Field(station, "locateTool");
        var locateMethod = locate.GetType().GetMethod("DoLocate", new[] { typeof(bool) });
        var times = new List<double>(); var setupTimes = new List<double>(); var bounds = new List<string>(); var pass = new List<bool>(); var imageNames = new List<string>();
        for (int i = 0; i < repeats; i++)
        {
            if (images.Length > 1 && i > 0) { (mat as IDisposable)?.Dispose(); mat = imread.Invoke(null, new object[] { images[i % images.Length], color }); }
            imageNames.Add(Path.GetFileName(images[i % images.Length]));
            timer.Restart(); buffersMethod.Invoke(station, new[] { mat }); timer.Stop(); setupTimes.Add(timer.Elapsed.TotalMilliseconds);
            timer.Restart(); bool ok = Convert.ToBoolean(locateMethod.Invoke(locate, new object[] { offline })); timer.Stop(); times.Add(timer.Elapsed.TotalMilliseconds);
            pass.Add(ok); bounds.Add(Convert.ToString(Member(locate, "LocateRect")));
            if (!ok) output["error"] = Member(locate, "ErrorMsg");
        }
        object located = Member(Field(station, "buffers"), "LocatedSrc");
        string export = Path.Combine(root, "benchmark-frame.png"); timer.Restart();
        located.GetType().GetMethod("SaveImage", new[] { typeof(string), typeof(int[]) }).Invoke(located, new object[] { export, new int[0] });
        timer.Stop(); output["pngExportMs"] = timer.Elapsed.TotalMilliseconds;
        (mat as IDisposable)?.Dispose(); entire.Stop();
        output["locateMs"] = times; output["setBuffersMs"] = setupTimes; output["bounds"] = bounds; output["pass"] = pass; output["images"] = imageNames; output["totalMs"] = entire.Elapsed.TotalMilliseconds;
        output["firstMs"] = times[0]; output["warmMedianMs"] = times.Skip(1).OrderBy(x => x).ElementAt((times.Count - 1) / 2);
        File.WriteAllText(args[4], new JavaScriptSerializer().Serialize(output));
        Console.WriteLine($"{args[0]} offline={offline} first={times[0]:F2} warmMedian={output["warmMedianMs"]:F2} total={entire.Elapsed.TotalMilliseconds:F1} pass={pass.Count(x => x)}/{pass.Count}");
    }
}
