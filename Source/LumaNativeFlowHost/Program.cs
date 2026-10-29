using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;

// Isolated, non-AI, native-only bridge. It calls the recovered InspTools
// CVInspection/LocateTool/SelectWayTool/SelectFaceTool directly; no algorithms
// are recreated in Luma. Never save or modify the source/vendor Recipe.
internal static class Program
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly Dictionary<string, object> State = new Dictionary<string, object>();

    private static object Field(object instance, string name)
    {
        var f = instance.GetType().GetField(name, Flags);
        if (f == null) throw new MissingFieldException(instance.GetType().FullName, name);
        return f.GetValue(instance);
    }
    private static void SetField(object instance, string name, object value)
    {
        var f = instance.GetType().GetField(name, Flags);
        if (f == null) throw new MissingFieldException(instance.GetType().FullName, name);
        f.SetValue(instance, value);
    }
    private static bool Bool(object instance, string name) => Convert.ToBoolean(Field(instance, name));

    private static int Main(string[] args)
    {
        if (args.Length != 4 && args.Length != 5) return 12;
        string name = args[0], input = args[1], resultFile = args[2];
        if (!int.TryParse(args[3], out int station) || station < 1 || station > 8)
            return 12;
        string stopAfter = args.Length == 5 ? args[4] : "light";
        if (!new[] { "locate", "way", "face", "light" }.Contains(stopAfter))
            return 12;
        try
        {
            if (name != Path.GetFileName(name) || string.IsNullOrWhiteSpace(name))
                throw new InvalidDataException("Invalid Recipe name");
            var root = AppDomain.CurrentDomain.BaseDirectory;
            string recipe = Path.Combine(root, "Recipe", name);
            if (!File.Exists(Path.Combine(recipe, "Setting.ini")) ||
                !Enumerable.Range(1, 8).All(i => File.Exists(Path.Combine(recipe, "station" + i + ".ini"))))
                throw new InvalidDataException("Native Recipe structure incomplete");
            if (!File.Exists(input)) throw new FileNotFoundException("OK DB image missing", input);
            string extension = Path.GetExtension(input).ToLowerInvariant();
            if (!new[] { ".jpg", ".jpeg", ".png", ".bmp" }.Contains(extension))
                throw new InvalidDataException("Unsupported image extension");
            State["recipe"] = name;
            State["station"] = station;
            State["image"] = Path.GetFileName(input);
            State["light"] = "LUMA"; // native LightMonitorTool is never invoked

            var insp = Assembly.LoadFrom(Path.Combine(root, "InspTools.dll"));
            var stationType = insp.GetType("InspTools.Station.CVInspection", true);
            var inspection = Activator.CreateInstance(stationType);
            object[] load = { name, station - 1, "" };
            stationType.GetMethod("SetRecipe", Flags).Invoke(inspection, load);
            if (!string.IsNullOrWhiteSpace(Convert.ToString(load[2])))
                throw new InvalidDataException("Station Recipe warning: " + load[2]);
            object param = Field(inspection, "paramFile");
            if (!Bool(param, "StationON"))
            {
                State["locate"] = "STATION_DISABLED";
                return Finish(resultFile, 2);
            }
            var cv = Assembly.LoadFrom(Path.Combine(root, "OpenCvSharp.dll"));
            var modes = cv.GetType("OpenCvSharp.ImreadModes", true);
            var matType = cv.GetType("OpenCvSharp.Mat", true);
            object color = Enum.Parse(modes, "Color");
            var imread = cv.GetType("OpenCvSharp.Cv2", true).GetMethod("ImRead",
                new[] { typeof(string), modes });
            var mat = imread.Invoke(null, new[] { (object)input, color });
            try
            {
                if ((int)matType.GetProperty("Width").GetValue(mat) <= 0)
                    throw new InvalidDataException("Unable to decode image");
                stationType.GetMethod("SetBuffers", new[] { matType })
                    .Invoke(inspection, new[] { mat });

                // Skip original AI locator even when vendor Recipe requests it.
                // This changes only the in-memory ParamFile in this subprocess.
                var locateParam = Field(param, "LocateParam");
                State["aiLocateInVendorRecipe"] = Bool(locateParam, "EnableAILocate");
                SetField(locateParam, "EnableAILocate", false);
                State["aiExecuted"] = false;
                if (!Bool(param, "LocateToolON"))
                {
                    State["locate"] = "NOT_CONFIGURED";
                    return Finish(resultFile, 2);
                }
                object locate = Field(inspection, "locateTool");
                bool located = Convert.ToBoolean(locate.GetType().GetMethod("DoLocate",
                    new[] { typeof(bool) }).Invoke(locate, new object[] { true }));
                State["locate"] = located ? "PASS" : "FAIL";
                if (!located) return Finish(resultFile, 2);
                if (stopAfter == "locate") return Finish(resultFile, 0);

                if (!Bool(param, "SelectWayON"))
                {
                    // A disabled native stage is explicitly skipped, never
                    // mislabeled as a successful flip.
                    State["selectWay"] = "SKIPPED_RECIPE_OFF";
                }
                else
                {
                    var way = Field(inspection, "selectwayTool");
                    bool flipped = Convert.ToBoolean(way.GetType().GetMethod("Inspect",
                        new[] { typeof(bool) }).Invoke(way, new object[] { true }));
                    State["selectWay"] = flipped ? "PASS" : "FAIL";
                    State["twoWayFlip"] = Bool(way, "TwoWayFlip");
                    if (!flipped) return Finish(resultFile, 2);
                }
                if (stopAfter == "way") return Finish(resultFile, 0);

                if (!Bool(param, "SelectFaceON"))
                {
                    State["selectFace"] = "SKIPPED_RECIPE_OFF";
                    return Finish(resultFile, 0);
                }
                var face = Field(inspection, "selectfaceTool");
                var faceSettings = Field(param, "SelectFaceParam") as Array;
                if (faceSettings == null) throw new InvalidDataException("Face parameters absent");
                bool hasEnabled = false, found = false;
                for (int i = 0; i < Math.Min(4, faceSettings.Length); i++)
                {
                    if (!Bool(faceSettings.GetValue(i), "TrackerON")) continue;
                    hasEnabled = true;
                    if (Convert.ToBoolean(face.GetType().GetMethod("Inspect",
                        new[] { typeof(int), typeof(bool) })
                        .Invoke(face, new object[] { i, true })))
                    {
                        State["faceIndex"] = i + 1;
                        found = true;
                        break;
                    }
                }
                State["selectFace"] = !hasEnabled ? "NOT_CONFIGURED" : found ? "PASS" : "FAIL";
                return Finish(resultFile, found ? 0 : 2);
            }
            finally { (mat as IDisposable)?.Dispose(); }
        }
        catch (Exception ex)
        {
            State["error"] = ex.GetBaseException().Message;
            return Finish(resultFile, 3);
        }
    }

    private static int Finish(string path, int code)
    {
        State["exit"] = code;
        try
        {
            if (!Path.GetFullPath(path).StartsWith(
                Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory), StringComparison.OrdinalIgnoreCase))
                return 13;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, new JavaScriptSerializer().Serialize(State));
        }
        catch { return 14; }
        return code;
    }
}
