using System;
using InspTools.Station;

internal sealed class VendorAiLocateAccessResult
{
    internal bool Authorized { get; set; }
    internal string Message { get; set; }
}

// AI recipes fail explicitly; never report an AI result from CPU-only v2.
internal static class VendorAiLocateAccess
{
    internal static VendorAiLocateAccessResult CheckAuthorization(CVInspection station)
    {
        if (station == null) throw new ArgumentNullException("station");
        station.paramFile.AllowEnableAILocate = false;
        return new VendorAiLocateAccessResult { Authorized = false, Message = "v2 為非 AI 版本，未包含 CUDA／AI Runtime；請使用非 AI 定位 Recipe。" };
    }
    internal static string LoadLocateModel(CVInspection station)
    {
        throw new NotSupportedException("v2 未提供 AI 模型載入。");
    }
}
