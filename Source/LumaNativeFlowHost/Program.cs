using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Diagnostics;
using System.Text;
using System.Runtime.CompilerServices;

// Isolated, non-AI, native-only bridge. It calls the recovered InspTools
// CVInspection/LocateTool/SelectWayTool/SelectFaceTool directly; no algorithms
// are recreated in Luma. Never save or modify the source/vendor Recipe.
internal static class Program
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly Dictionary<string, object> State = new Dictionary<string, object>();
    private static bool WorkerMode;
    private static object LoadedInspection;
    private static string LoadedKey;
    private static readonly Dictionary<string, object> OriginalParams = new Dictionary<string, object>();

    public sealed class WorkerRequest
    {
        public string Id { get; set; }
        public string Revision { get; set; }
        public string[] Args { get; set; }
    }

    private static int Main(string[] args)
    {
        if (args.Length != 1 || args[0] != "--worker") return Run(args, "");
        WorkerMode = true;
        Console.InputEncoding = new UTF8Encoding(false);
        using (var protocol = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true })
        {
            // Vendor diagnostic output must never corrupt the JSON-line protocol.
            Console.SetOut(TextWriter.Null);
            var serializer = new JavaScriptSerializer();
            string line;
            while ((line = Console.ReadLine()) != null)
            {
                State.Clear(); string id = ""; var total = Stopwatch.StartNew();
                try
                {
                    var request = serializer.Deserialize<WorkerRequest>(line);
                    if (request == null || string.IsNullOrEmpty(request.Id) || string.IsNullOrEmpty(request.Revision) || request.Args == null)
                        throw new InvalidDataException("Invalid worker request");
                    id = request.Id;
                    State["exit"] = Run(request.Args, request.Revision);
                }
                catch (Exception ex) { State["error"] = ex.GetBaseException().Message; State["exit"] = 3; LoadedInspection = null; }
                total.Stop(); State["requestId"] = id; State["workerMs"] = total.Elapsed.TotalMilliseconds;
                State["workerProcessId"] = Process.GetCurrentProcess().Id;
                protocol.WriteLine(serializer.Serialize(State));
            }
        }
        return 0; // stdin EOF also cleans up after the parent closes/crashes.
    }

    private static object LoadInspection(string root, string name, int station, string revision)
    {
        string key = name + "|" + station + "|" + revision;
        bool reused = WorkerMode && LoadedInspection != null && key == LoadedKey;
        State["recipeReused"] = reused;
        if (!reused)
        {
            var insp = Assembly.LoadFrom(Path.Combine(root, "InspTools.dll"));
            var stationType = insp.GetType("InspTools.Station.CVInspection", true);
            object inspection = Activator.CreateInstance(stationType);
            object[] load = { name, station - 1, "" };
            stationType.GetMethod("SetRecipe", Flags).Invoke(inspection, load);
            if (!string.IsNullOrWhiteSpace(Convert.ToString(load[2])))
                throw new InvalidDataException("Station Recipe warning: " + load[2]);
            var param = Field(inspection, "paramFile"); OriginalParams.Clear();
            foreach (string field in new[] { "StationON", "LocateToolON", "SelectWayON", "SelectFaceON", "LocateParam", "SelectWayParam", "SelectFaceParam" })
            {
                object value = Field(param, field);
                OriginalParams[field] = value is Array array ? array.Clone() : RuntimeHelpers.GetObjectValue(value);
            }
            LoadedInspection = inspection; LoadedKey = key;
        }
        // Native tools mutate geometry/flags. Restore the loaded baseline before each frame.
        var file = Field(LoadedInspection, "paramFile");
        foreach (var pair in OriginalParams)
            SetField(file, pair.Key, pair.Value is Array array ? array.Clone() : RuntimeHelpers.GetObjectValue(pair.Value));
        var face = Field(LoadedInspection, "selectfaceTool");
        foreach (string field in new[] { "FACEResult", "MaxFaceResult" })
        { var values = (Array)Field(face, field); Array.Clear(values, 0, values.Length); }
        SetField(face, "FaceEnableCount", 0); SetField(face, "RowMin", 0);
        return LoadedInspection;
    }

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
    private static object Member(object instance, string name)
    {
        var type = instance.GetType();
        var prop = type.GetProperty(name, Flags);
        return prop != null ? prop.GetValue(instance) : Field(instance, name);
    }

    private static int Run(string[] args, string revision)
    {
        State.Clear();
        if (args.Length < 4 || args.Length > 6) return 12;
        string name = args[0], input = args[1], resultFile = args[2];
        if (!int.TryParse(args[3], out int station) || station < 1 || station > 8)
            return 12;
        string stopAfter = args.Length >= 5 ? args[4] : "light";
        string selection = args.Length == 6 ? args[5] : null;
        if (selection != null && (selection.Length != 3 || selection.Any(c => c != '0' && c != '1'))) return 12;
        if (!new[] { "locate", "way", "face", "light" }.Contains(stopAfter))
            return 12;
        try
        {
            if (name != Path.GetFileName(name) || string.IsNullOrWhiteSpace(name))
                throw new InvalidDataException("Invalid Recipe name");
            var root = AppDomain.CurrentDomain.BaseDirectory;
            if (!Path.GetFullPath(resultFile).StartsWith(Path.GetFullPath(root).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Result path outside runtime.");
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

            var inspection = LoadInspection(root, name, station, revision);
            var stationType = inspection.GetType();
            object param = Field(inspection, "paramFile");
            if (selection != null)
            {
                SetField(param, "StationON", true);
                SetField(param, "LocateToolON", selection[0] == '1');
                SetField(param, "SelectWayON", selection[1] == '1');
                SetField(param, "SelectFaceON", selection[2] == '1');
                State["selectionOverride"] = true;
            }
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
                if (locateParam.GetType().IsValueType) SetField(param, "LocateParam", locateParam);
                State["aiExecuted"] = false;
                if (!Bool(param, "LocateToolON"))
                {
                    State["locate"] = "NOT_CONFIGURED";
                    return Finish(resultFile, 2);
                }
                object locate = Field(inspection, "locateTool");
                var timer = Stopwatch.StartNew();
                bool located = Convert.ToBoolean(locate.GetType().GetMethod("DoLocate",
                    new[] { typeof(bool) }).Invoke(locate, new object[] { true }));
                timer.Stop(); State["locateMs"] = timer.Elapsed.TotalMilliseconds;
                State["locate"] = located ? "PASS" : "FAIL";
                if (!located) State["error"] = Convert.ToString(Member(locate, "ErrorMsg"));
                if (!located) return Finish(resultFile, 2);
                if (stopAfter == "locate") { SaveFrame(resultFile, Field(inspection, "buffers")); return Finish(resultFile, 0); }

                if (!Bool(param, "SelectWayON"))
                {
                    // A disabled native stage is explicitly skipped, never
                    // mislabeled as a successful flip.
                    State["selectWay"] = "SKIPPED_RECIPE_OFF";
                }
                else
                {
                    var way = Field(inspection, "selectwayTool");
                    timer.Restart();
                    var waySettings = Field(param, "SelectWayParam");
                    if (Convert.ToInt32(Field(waySettings, "SelelctWayFunction")) != 0)
                    {
                        int index = Convert.ToInt32(Field(waySettings, "PatternSelect"));
                        var patterns = (Array)Field(param, "patternDB");
                        if (index < 0 || index >= patterns.Length || !Bool(patterns.GetValue(index), "PatternON") ||
                            Convert.ToBoolean(Member(patterns.GetValue(index), "PatternImage").GetType().GetMethod("Empty").Invoke(Member(patterns.GetValue(index), "PatternImage"), null)))
                        {
                            timer.Stop(); State["selectWayMs"] = timer.Elapsed.TotalMilliseconds;
                            State["selectWay"] = "NOT_CONFIGURED";
                            State["error"] = "圖形選向尚未選擇有效圖形樣板。";
                            return Finish(resultFile, 2);
                        }
                    }
                    bool flipped = Convert.ToBoolean(way.GetType().GetMethod("Inspect",
                        new[] { typeof(bool) }).Invoke(way, new object[] { true }));
                    if (flipped) AdjustLocatedRect(inspection, locate, way, param);
                    timer.Stop(); State["selectWayMs"] = timer.Elapsed.TotalMilliseconds;
                    State["selectWay"] = flipped ? "FLIPPED" : "UNCHANGED";
                    State["selectWayDetail"] = Convert.ToString(Member(way, "Tracker1ResultText"));
                    State["twoWayFlip"] = Bool(way, "TwoWayFlip");
                }
                SaveFrame(resultFile, Field(inspection, "buffers"));
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
                timer.Restart();
                bool rowMode = Bool(faceSettings.GetValue(0), "RowSelectFace");
                bool minMode = Bool(faceSettings.GetValue(0), "RowMinSelectFace");
                int foundIndex = 0;
                for (int i = 0; i < Math.Min(4, faceSettings.Length); i++)
                {
                    if (!Bool(faceSettings.GetValue(i), "TrackerON")) continue;
                    hasEnabled = true;
                    if (Convert.ToBoolean(face.GetType().GetMethod("Inspect",
                        new[] { typeof(int), typeof(bool) })
                        .Invoke(face, new object[] { i, true })))
                    {
                        foundIndex = i + 1;
                        found = true;
                        if (!rowMode && !minMode) break;
                    }
                }
                if (hasEnabled && rowMode)
                {
                    var values = (double[])Field(face, "MaxFaceResult");
                    foundIndex = values[0] > values[1] ? 1 : 2;
                    found = true;
                }
                else if (hasEnabled && minMode)
                {
                    foundIndex = Convert.ToInt32(Field(face, "RowMin")) == 0 ? 1 : 2;
                    found = true;
                }
                timer.Stop(); State["selectFaceMs"] = timer.Elapsed.TotalMilliseconds;
                if (found) State["faceIndex"] = foundIndex;
                State["selectFace"] = !hasEnabled ? "NOT_CONFIGURED" : found ? "PASS" : "FAIL";
                return Finish(resultFile, found ? 0 : 2);
            }
            finally { (mat as IDisposable)?.Dispose(); }
        }
        catch (Exception ex)
        {
            LoadedInspection = null;
            State["error"] = ex.GetBaseException().Message;
            return Finish(resultFile, 3);
        }
    }

    private static void SaveFrame(string resultFile, object buffers)
    {
        string image = Path.ChangeExtension(resultFile, WorkerMode ? ".bmp" : ".png");
        object mat = Member(buffers, "LocatedSrc");
        if (!(bool)mat.GetType().GetMethod("SaveImage", new[] { typeof(string), typeof(int[]) }).Invoke(mat, new object[] { image, new int[0] }))
            throw new IOException("無法輸出流程影像。");
        object rect = Member(buffers, "LocatedRect");
        object imageRect = Member(buffers, "LocateImageRect");
        State["outputImage"] = image;
        State["x"] = Member(rect, "X"); State["y"] = Member(rect, "Y");
        State["width"] = Member(rect, "Width"); State["height"] = Member(rect, "Height");
        State["imageX"] = Member(imageRect, "X"); State["imageY"] = Member(imageRect, "Y");
    }

    private static void AdjustLocatedRect(object inspection, object locate, object way, object param)
    {
        object buffers = Field(inspection, "buffers");
        object rect = locate.GetType().GetProperty("LocateRect", Flags).GetValue(locate);
        object imageRect = buffers.GetType().GetProperty("LocateImageRect", Flags).GetValue(buffers);
        object settings = Field(param, "SelectWayParam");
        object foundRect = Field(way, "FoundTextRect");
        int mode = Convert.ToInt32(Field(settings, Bool(way, "TwoWayFlip") ? "RotateMethodB" : "RotateMethod"));
        Type rt = rect.GetType();
        if (mode == 0 || mode == 2)
        {
            rt.GetProperty("Left").SetValue(rect, Convert.ToInt32(Member(imageRect, "Width")) - Convert.ToInt32(Member(rect, "Right")) - 1);
            rt.GetProperty("Left").SetValue(foundRect, Convert.ToInt32(Member(imageRect, "Width")) - Convert.ToInt32(Field(settings, "FoundTextRectL")) - Convert.ToInt32(Field(settings, "FoundTextRectW")) - 1);
        }
        if (mode == 1 || mode == 2)
        {
            rt.GetProperty("Top").SetValue(rect, Convert.ToInt32(Member(imageRect, "Height")) - Convert.ToInt32(Member(rect, "Bottom")) - 1);
            rt.GetProperty("Top").SetValue(foundRect, Convert.ToInt32(Member(imageRect, "Height")) - Convert.ToInt32(Field(settings, "FoundTextRectT")) - Convert.ToInt32(Field(settings, "FoundTextRectH")) - 1);
        }
        SetField(settings, "FoundTextRectL", Convert.ToInt32(Member(foundRect, "Left")));
        SetField(settings, "FoundTextRectT", Convert.ToInt32(Member(foundRect, "Top")));
        SetField(param, "SelectWayParam", settings);
        SetField(way, "FoundTextRect", foundRect);
        object dst = buffers.GetType().GetProperty("Dst", Flags).GetValue(buffers);
        object locatedSrc = buffers.GetType().GetProperty("LocatedSrc", Flags).GetValue(buffers);
        Type matType = dst.GetType();
        using (var roi = (IDisposable)Activator.CreateInstance(matType, new[] { dst, imageRect }))
            matType.GetMethod("CopyTo", new[] { matType }).Invoke(locatedSrc, new object[] { roi });
        locate.GetType().GetProperty("LocateRect", Flags).SetValue(locate, rect);
        inspection.GetType().GetProperty("LocateRect", Flags).SetValue(inspection, rect);
        buffers.GetType().GetProperty("LocatedRect", Flags).SetValue(buffers, rect);
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
