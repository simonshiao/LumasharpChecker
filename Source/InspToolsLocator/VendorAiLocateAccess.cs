using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using InspTools.Station;

internal sealed class VendorAiLocateAccessResult
{
    internal bool Authorized { get; set; }
    internal string Message { get; set; }
}

/// <summary>
/// Applies the original InspTools authorization result without fabricating its
/// feature flags, then uses the vendor model loader for the selected Recipe.
/// </summary>
internal static class VendorAiLocateAccess
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    internal static VendorAiLocateAccessResult CheckAuthorization(CVInspection station)
    {
        if (station == null) throw new ArgumentNullException("station");
        station.paramFile.AllowEnableAILocate = false;

        string appBase = AppDomain.CurrentDomain.BaseDirectory;
        if (!File.Exists(Path.Combine(appBase, "OnnxRuntimeEnv.dll")) ||
            !File.Exists(Path.Combine(appBase, "ThoughtWorks.QRCode.dll")))
            return Denied("原廠 AI 授權元件缺失");

        string authData = Path.Combine(@"C:\Temp", "onnxruntime.dat");
        if (!Directory.Exists(@"C:\Temp") || !File.Exists(authData))
            return Denied("原廠 AI 授權資料未就緒");

        object authWindow = null;
        try
        {
            if (Application.Current == null)
                new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

            // InspTools creates AuthWindow from the OnnxRuntimeEnv dependency
            // (namespace OnnxRuntimeEnv), not from the InspTools assembly.
            // Resolve the vendor type from the already loaded dependency so
            // this check follows the same implementation as ToolsWindow.
            Assembly authAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => string.Equals(
                    assembly.GetName().Name,
                    "OnnxRuntimeEnv",
                    StringComparison.OrdinalIgnoreCase));
            if (authAssembly == null)
                authAssembly = Assembly.Load("OnnxRuntimeEnv");
            Type assemblyType = authAssembly.GetType("OnnxRuntimeEnv.AuthWindow", false);
            if (assemblyType == null)
                return Denied("找不到原廠授權檢查器");

            authWindow = Activator.CreateInstance(assemblyType, true);
            MethodInfo check = assemblyType.GetMethods(InstanceFlags).FirstOrDefault(method =>
            {
                if (method.Name != "CheckAuth") return false;
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 2 &&
                    parameters[0].ParameterType == typeof(string).MakeByRefType() &&
                    parameters[1].ParameterType == typeof(string);
            });
            if (check == null) return Denied("原廠授權檢查介面不相容");

            object[] arguments = { string.Empty, authData };
            bool checkPassed = Convert.ToBoolean(check.Invoke(authWindow, arguments));
            // Match the call sequence used by InspTools.ToolsWindow.CheckAuthNoDLG.
            InvokeNoArguments(assemblyType, authWindow, "GetLimitDateStr");
            InvokeNoArguments(assemblyType, authWindow, "GetOverTime");
            object authedValue = InvokeNoArguments(assemblyType, authWindow, "GetAuthed");
            InvokeNoArguments(assemblyType, authWindow, "GetLimitDate");
            object featureValue = InvokeNoArguments(assemblyType, authWindow, "GetFunctionChar");
            bool authenticated = checkPassed && authedValue != null && Convert.ToBoolean(authedValue);
            string featureTier = Convert.ToString(featureValue) ?? string.Empty;
            if (!checkPassed)
                return Denied("原廠授權檢查未通過（CheckAuth 未通過）");
            if (authedValue == null || !Convert.ToBoolean(authedValue))
                return Denied("原廠授權檢查未通過（授權狀態未認證）");
            if (!string.Equals(featureTier, "C", StringComparison.OrdinalIgnoreCase))
                return Denied("原廠授權未包含 AI 定位功能");

            // This permission is set only after the vendor's own license check
            // accepted a tier that includes AI Locate.
            object level = InvokeNoArguments(assemblyType, authWindow, "GetLevel");
            SetAuthLevel(station.paramFile, level);
            station.paramFile.AllowEnableAILocate = true;
            return new VendorAiLocateAccessResult { Authorized = true, Message = "原廠授權允許 AI 定位" };
        }
        catch (Exception ex)
        {
            station.paramFile.AllowEnableAILocate = false;
            return Denied("原廠授權檢查失敗：" + ex.GetBaseException().Message);
        }
        finally
        {
            if (authWindow != null)
            {
                try
                {
                    MethodInfo close = authWindow.GetType().GetMethod("Close", InstanceFlags);
                    if (close != null) close.Invoke(authWindow, null);
                }
                catch { }
            }
        }
    }

    internal static string LoadLocateModel(CVInspection station)
    {
        if (station == null) throw new ArgumentNullException("station");
        if (!station.paramFile.LocateParam.EnableAILocate)
            return "Recipe 未啟用 AI 定位";
        if (!station.paramFile.AllowEnableAILocate)
            return "原廠授權未開放 AI 定位";

        string modelName = station.paramFile.LocateParam.AILocateModelName ?? string.Empty;
        string modelBase = Path.GetFileNameWithoutExtension(modelName.Trim());
        if (string.IsNullOrWhiteSpace(modelBase) || modelBase != modelName.Trim())
            return "Recipe 的 AI 定位模型名稱無效";

        string appBase = AppDomain.CurrentDomain.BaseDirectory;
        if (!File.Exists(Path.Combine(appBase, "Runtime", "DeltaAIProcess", "DeltaAIProcessDll.dll")) ||
            !File.Exists(Path.Combine(appBase, "Runtime", "CUDA11_118", "cudart64_110.dll")))
            return "AI 定位 Runtime 未部署完整";

        string modelPath = Path.Combine(appBase, "Model", modelBase + ".lh5d");
        if (!File.Exists(modelPath))
            return "AI 定位模型缺失：" + modelBase + ".lh5d";

        if (station.paramFile.LocateParam.EnableTensorRT &&
            !File.Exists(Path.Combine(appBase, "Model", modelBase + ".ltrt")))
            return "AI 定位 TensorRT 模型缺失：" + modelBase + ".ltrt";

        try
        {
            station.locateTool.ReLoadAIModel(false);
            FieldInfo toolField = station.locateTool.GetType().GetField("locateAIEnhanceTool", InstanceFlags);
            object aiTool = toolField != null ? toolField.GetValue(station.locateTool) : null;
            FieldInfo loadedField = aiTool != null ? aiTool.GetType().GetField("ModelLoaded", InstanceFlags) : null;
            bool loaded = loadedField != null && Convert.ToBoolean(loadedField.GetValue(aiTool));
            if (loaded) return "AI 定位模型已載入：" + modelBase;

            FieldInfo resultField = aiTool != null ? aiTool.GetType().GetField("ResultText", InstanceFlags) : null;
            string detail = resultField != null ? Convert.ToString(resultField.GetValue(aiTool)) : string.Empty;
            return string.IsNullOrWhiteSpace(detail) ? "AI 定位模型載入失敗" : "AI 定位模型載入失敗：" + detail.Trim();
        }
        catch (Exception ex)
        {
            return "AI 定位模型載入失敗：" + ex.GetBaseException().Message;
        }
    }

    private static VendorAiLocateAccessResult Denied(string message)
    {
        return new VendorAiLocateAccessResult { Authorized = false, Message = message };
    }

    private static object InvokeNoArguments(Type type, object instance, string methodName)
    {
        MethodInfo method = type.GetMethod(methodName, InstanceFlags, null, Type.EmptyTypes, null);
        return method != null ? method.Invoke(instance, null) : null;
    }

    private static void SetAuthLevel(object paramFile, object level)
    {
        if (paramFile == null || level == null) return;
        FieldInfo field = paramFile.GetType().GetField("AuthLevel", InstanceFlags);
        if (field == null) return;
        try
        {
            object value = field.FieldType.IsInstanceOfType(level)
                ? level
                : Convert.ChangeType(level, field.FieldType);
            field.SetValue(paramFile, value);
        }
        catch { }
    }
}
