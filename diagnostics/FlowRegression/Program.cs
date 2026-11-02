// Headless checks against the compiled view model. No windows or live recipes are opened.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Drawing;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;

internal static class Program
{
    static Type vmType, rowType;
    const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static void Set(object obj, string property, object value) => obj.GetType().GetProperty(property, Members).SetValue(obj, value);
    static object Get(object obj, string property) => obj.GetType().GetProperty(property, Members).GetValue(obj);
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Members).Invoke(obj, args);
    static void Field(object obj, string name, object value)
    {
        foreach (var field in obj.GetType().GetFields(Members))
            if (string.Equals(field.Name.TrimStart('_'), name, StringComparison.OrdinalIgnoreCase)) { field.SetValue(obj, value); return; }
        throw new Exception("Missing field " + name);
    }
    static void Assert(bool condition, string label)
    {
        if (!condition) throw new Exception("FAIL " + label);
        Console.WriteLine("PASS " + label);
    }
    static object Row(string file, bool pass)
    {
        var row = Activator.CreateInstance(rowType); Set(row, "FilePath", file); Set(row, "IsMatched", true); Set(row, "BrightnessSafe", pass); Set(row, "IncludeSharpness", false); return row;
    }
    [STAThread]
    static void Main(string[] args)
    {
        string exe = Path.GetFullPath(args[0]), image = Path.GetFullPath(args[1]);
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            string file = Path.Combine(Path.GetDirectoryName(exe), new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(file) ? Assembly.LoadFrom(file) : null;
        };
        var assembly = Assembly.LoadFrom(exe);
        vmType = assembly.GetType("LumasharpChecker.viewmodel.mainViewModel", true); rowType = assembly.GetType("LumasharpChecker.viewmodel.TestImageResultRow", true);
        // Isolate the model from application startup, preferences, and recipe I/O.
        var vm = FormatterServices.GetUninitializedObject(vmType);
        Field(vm, "testImageResults", Activator.CreateInstance(typeof(ObservableCollection<>).MakeGenericType(rowType)));
        Field(vm, "testImageResultIndexes", new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        Field(vm, "detectionMode", "亮度");
        Call(vm, "BeginContinuousFlow");
        Call(vm, "RecordFlowStatistics", Row("preview", true), false);
        Assert((int)Get(vm, "TotalTestImageCount") == 0, "single preview does not increment totals");
        var ok = Row("a", true); Call(vm, "RecordFlowStatistics", ok, true); Call(vm, "RecordFlowStatistics", Row("b", false), true);
        Assert((int)Get(vm, "OverallOkCount") == 1 && (int)Get(vm, "OverallNgCount") == 1 && (string)Get(vm, "OverallOkRateText") == "50.00%", "continuous batch computes OK/NG/yield");
        Call(vm, "RecordFlowStatistics", Row("a", false), false);
        Assert((int)Get(vm, "OverallOkCount") == 1 && (int)Get(vm, "TotalTestImageCount") == 2 && ReferenceEquals(((IList)Get(vm, "TestImageResults"))[0], ok), "retesting a batch image does not overwrite batch results or counters");
        Call(vm, "RecordFlowStatistics", Row("a", false), true);
        Assert((int)Get(vm, "OverallOkCount") == 0 && (int)Get(vm, "OverallNgCount") == 2 && (int)Get(vm, "TotalTestImageCount") == 2, "batch replacement counts each file once");
        Call(vm, "BeginContinuousFlow");
        Assert((int)Get(vm, "TotalTestImageCount") == 0 && (string)Get(vm, "OverallOkRateText") == "0.00%", "new batch resets totals and yield");

        Field(vm, "currentRecipe", Activator.CreateInstance(assembly.GetType("LumasharpChecker.model.RecipeManager+RecipeManifest", true)));
        Field(vm, "testImageFiles", new ObservableCollection<string> { image, image, image });
        Field(vm, "currentTestImageIndex", 0); Field(vm, "canNavigateNext", true); Field(vm, "resultListFilter", "ALL");
        int tested = 0; Set(vm, "InspectCurrentFlow", new Func<Task>(() => { tested++; return Task.CompletedTask; }));
        ((Task)Call(vm, "NextTestImage")).GetAwaiter().GetResult();
        Assert(tested == 1 && (int)Get(vm, "CurrentTestImageIndex") == 1, "next image invokes inspection");
        ((Task)Call(vm, "PreviousTestImage")).GetAwaiter().GetResult();
        Assert(tested == 2 && (int)Get(vm, "CurrentTestImageIndex") == 0, "previous image invokes inspection");
        Set(vm, "IsContinuousTestRunning", true); ((Task)Call(vm, "NextTestImage")).GetAwaiter().GetResult();
        Assert(tested == 2 && (int)Get(vm, "CurrentTestImageIndex") == 0, "navigation cannot interrupt continuous inspection");
        Set(vm, "IsContinuousTestRunning", false); Set(vm, "IsNativeOperationBusy", new Func<bool>(() => true)); ((Task)Call(vm, "NextTestImage")).GetAwaiter().GetResult();
        Assert(tested == 2 && (int)Get(vm, "CurrentTestImageIndex") == 0, "navigation cannot interrupt an active native editor or inspection");
        Assert((int)Get(vm, "TotalTestImageCount") == 0, "navigation leaves continuous counters unchanged");
        CheckResultOnlyPreview(vm, assembly);
        CheckDeferredBatch(vm, image);
        CheckRoiGeometry(assembly);
    }

    static void CheckDeferredBatch(object vm, string image)
    {
        string second = Directory.GetFiles(Path.GetDirectoryName(image)).First(p => p != image);
        Field(vm, "testImageFiles", new ObservableCollection<string> { image, second });
        int previews = 0;
        PropertyChangedEventHandler changed = (_, e) => { if (e.PropertyName == "CurrentTestImageBitmap") previews++; };
        ((INotifyPropertyChanged)vm).PropertyChanged += changed;
        try
        {
            var frozen = Get(vm, "CurrentTestImageBitmap"); var frozenPath = Get(vm, "CurrentTestImagePath");
            Call(vm, "BeginContinuousFlow");
            Call(vm, "StoreContinuousFlowResult", Row(image, true)); Call(vm, "StoreContinuousFlowResult", Row(second, false));
            Assert(previews == 0 && ReferenceEquals(Get(vm, "CurrentTestImageBitmap"), frozen) && Equals(Get(vm, "CurrentTestImagePath"), frozenPath), "continuous processing leaves current image and preview untouched");
            Assert((int)Get(vm, "TotalTestImageCount") == 0 && ((IList)Get(vm, "TestImageResults")).Count == 0, "batch list and counters are deferred during processing");
            Call(vm, "FinishContinuousFlow", true);
            Assert((int)Get(vm, "TotalTestImageCount") == 2 && (int)Get(vm, "OverallOkCount") == 1 && (int)Get(vm, "OverallNgCount") == 1, "final batch publication retains every OK/NG result");
            Assert(previews == 1 && (string)Get(vm, "CurrentTestImagePath") == second && (int)Get(vm, "CurrentTestImageIndex") == 1, "batch completion decodes only the last completed image once");
            Call(vm, "BeginContinuousFlow"); Call(vm, "StoreContinuousFlowResult", Row(image, true));
            Call(vm, "FinishContinuousFlow", true);
            Assert(previews == 2 && (string)Get(vm, "CurrentTestImagePath") == image && (int)Get(vm, "TotalTestImageCount") == 1, "stopped partial batch displays last completed result with partial totals");
            Call(vm, "BeginContinuousFlow"); Call(vm, "StoreContinuousFlowResult", Row(second, false)); Call(vm, "FinishContinuousFlow", false);
            Assert(previews == 2 && ((IList)Get(vm, "TestImageResults")).Count == 0, "context changes discard old pending results without replacing the image");
            Call(vm, "BeginContinuousFlow"); Call(vm, "FinishContinuousFlow", true);
            Assert(previews == 2, "empty batch does not reload the current image");
        }
        finally { ((INotifyPropertyChanged)vm).PropertyChanged -= changed; }
    }

    static void CheckResultOnlyPreview(object vm, Assembly assembly)
    {
        Field(vm, "originalX", 100d); Field(vm, "originalY", 200d); Field(vm, "originalWidth", 300d); Field(vm, "originalHeight", 400d);
        Field(vm, "originalX2", 120d); Field(vm, "originalY2", 220d); Field(vm, "originalWidth2", 100d); Field(vm, "originalHeight2", 80d);
        Set(vm, "TestImageROI", Rectangle.Empty); Set(vm, "TestImageBrightnessROI", Rectangle.Empty);
        Assert(!(bool)Get(vm, "IsGreenBoxVisible") && !(bool)Get(vm, "IsBrightnessBoxVisible") && (double)Get(vm, "DisplayRectWidth") == 0 && (double)Get(vm, "DisplayRectWidth2") == 0, "missing results do not display legacy template rectangles");
        Set(vm, "TestImageROI", new Rectangle(7, 8, 90, 60)); Set(vm, "TestImageBrightnessROI", new Rectangle(12, 14, 50, 30));
        Assert((bool)Get(vm, "IsGreenBoxVisible") && (double)Get(vm, "DisplayRectX") == 7 && (double)Get(vm, "DisplayRectHeight") == 60, "main locator overlay uses measured result geometry");
        Assert((bool)Get(vm, "IsBrightnessBoxVisible") && (double)Get(vm, "DisplayRectX2") == 12 && (double)Get(vm, "DisplayRectWidth2") == 50, "main light overlay uses measured result geometry");
        var bitmap = Get(vm, "CurrentTestImageBitmap");
        var profile = Activator.CreateInstance(assembly.GetType("ProfileManager+ProfileData", true)); Set(profile, "Bitmap", bitmap); Field(vm, "currentProfile", profile);
        Field(vm, "currentTestImageBitmap", null); Call(vm, "UpdateDisplayImage");
        Assert(Get(vm, "DisplayImage") == null, "empty main preview does not fall back to legacy profile image");
        Field(vm, "currentTestImageBitmap", bitmap); Call(vm, "UpdateDisplayImage");
        Assert(bitmap != null && ReferenceEquals(Get(vm, "DisplayImage"), bitmap), "loaded inspection image remains available before inspection");
    }

    static void CheckRoiGeometry(Assembly assembly)
    {
        var geometry = assembly.GetType("LumasharpChecker.model.RoiEditorGeometry", true);
        var partType = geometry.GetNestedType("Part", BindingFlags.NonPublic);
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        object Part(int value) => Enum.ToObject(partType, value);
        var bounds = new Rectangle(100, 80, 400, 300); var roi = new Rectangle(180, 140, 100, 80);
        Rectangle Transform(int part, double x, double y) => (Rectangle)geometry.GetMethod("Transform", flags).Invoke(null, new object[] { roi, bounds, Part(part), x, y });
        int Hit(double x, double y, double tolerance) => Convert.ToInt32(geometry.GetMethod("HitTest", flags).Invoke(null, new object[] { roi, x, y, tolerance }));
        Assert(Hit(230, 180, 7) == 16, "ROI interior selects movement");
        Assert(Hit(180, 140, 7) == 3 && Hit(280, 220, 7) == 12, "ROI opposite corners select diagonal resize");
        Assert(Hit(180, 180, 7) == 1 && Hit(230, 140, 7) == 2 && Hit(280, 180, 7) == 4 && Hit(230, 220, 7) == 8, "ROI four edges resize independently");
        Assert(Hit(20, 20, 7) == 0, "clicking outside ROI does not move it");
        Assert(Hit(294, 220, 14) == 12, "scaled hit tolerance keeps corner accessible at half-size preview");
        Assert(Transform(16, 1000, -1000) == new Rectangle(400, 80, 100, 80), "ROI movement clamps to located bounds without changing size");
        Assert(Transform(3, -1000, -1000) == Rectangle.FromLTRB(100, 80, 280, 220), "top-left resize clamps both edges and anchors opposite corner");
        Assert(Transform(12, 1000, 1000) == Rectangle.FromLTRB(180, 140, 500, 380), "bottom-right resize clamps to bounds");
        Assert(Transform(1, 1000, 0).Width == 2 && Transform(2, 0, 1000).Height == 2, "resize cannot invert ROI or shrink below two pixels");
        Assert(Transform(4, 20, 99) == new Rectangle(180, 140, 120, 80), "edge resize changes only intended axis");
        var reverse = (Rectangle)geometry.GetMethod("Draw", flags).Invoke(null, new object[] { bounds, 350d, 300d, 50d, 20d });
        Assert(reverse == Rectangle.FromLTRB(100, 80, 350, 300), "reverse drawing clips to located bounds");
        var settingsType = assembly.GetType("LumasharpChecker.model.LightInspectionSettings", true);
        var settings = Activator.CreateInstance(settingsType); var moved = Transform(16, 25, 30);
        Set(settings, "HasRoi", true); Set(settings, "Left", moved.Left - bounds.Left); Set(settings, "Top", moved.Top - bounds.Top);
        Set(settings, "Right", bounds.Right - moved.Right); Set(settings, "Bottom", bounds.Bottom - moved.Bottom);
        Assert((Rectangle)Call(settings, "GetRoi", bounds) == moved, "edited rectangle round-trips through existing recipe margins");
        var nextBounds = new Rectangle(120, 100, 400, 300);
        Assert((Rectangle)Call(settings, "GetRoi", nextBounds) == new Rectangle(moved.X + 20, moved.Y + 20, moved.Width, moved.Height), "saved ROI follows next image locator offset");
        foreach (var part in new[] { 1, 2, 3, 4, 6, 8, 9, 12, 16 })
            foreach (int dx in new[] { -2000, -99, 0, 123, 2000 })
                foreach (int dy in new[] { -2000, -99, 0, 123, 2000 })
                {
                    var value = Transform(part, dx, dy);
                    if (!bounds.Contains(value) || value.Width < 2 || value.Height < 2) throw new Exception("ROI bounds invariant failed");
                }
        Assert(true, "225 drag combinations preserve ROI bounds and minimum size");
    }
}
