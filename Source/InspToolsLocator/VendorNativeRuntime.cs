using System;
using System.IO;

// CPU-only v2: no GPU DLL loading or machine-wide DLL search-path changes.
internal static class VendorNativeRuntime
{
    internal static void Initialize(string appBase)
    {
        if (string.IsNullOrWhiteSpace(appBase)) throw new ArgumentException("AppBase is required.", "appBase");
        if (!Directory.Exists(appBase)) throw new DirectoryNotFoundException(appBase);
    }
}
