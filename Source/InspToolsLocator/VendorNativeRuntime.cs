using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

/// <summary>
/// Initializes the same app-local native search path used by the Chao8Side host
/// before either the locator UI or the command-line detector touches InspTools.
/// </summary>
internal static class VendorNativeRuntime
{
    private const uint LoadLibrarySearchDllLoadDir = 0x00000100;
    private const uint LoadLibrarySearchDefaultDirectories = 0x00001000;

    private static readonly object Sync = new object();
    private static readonly List<IntPtr> DirectoryCookies = new List<IntPtr>();
    private static IntPtr _deltaAiProcessModule;
    private static string _initializedBaseDirectory;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDefaultDllDirectories(uint directoryFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr AddDllDirectory(string newDirectory);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string fileName, IntPtr file, uint flags);

    internal static void Initialize(string appBase)
    {
        if (string.IsNullOrWhiteSpace(appBase)) throw new ArgumentException("AppBase 不可為空。", "appBase");
        string baseDirectory = Path.GetFullPath(appBase);

        lock (Sync)
        {
            if (_deltaAiProcessModule != IntPtr.Zero)
            {
                if (!string.Equals(_initializedBaseDirectory, baseDirectory, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("原廠 AI Runtime 已在另一個 AppBase 初始化。" );
                return;
            }

            if (!SetDefaultDllDirectories(LoadLibrarySearchDefaultDirectories))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "無法設定原廠 AI Runtime 的 DLL 搜尋路徑。" );

            string runtimeDirectory = Path.Combine(baseDirectory, "Runtime");
            foreach (string relativeDirectory in new[] { "DeltaAIProcess", "CUDA11_118" })
            {
                string directory = Path.Combine(runtimeDirectory, relativeDirectory);
                if (!Directory.Exists(directory))
                    throw new DirectoryNotFoundException("缺少原廠 AI Runtime 目錄：" + directory);

                IntPtr cookie = AddDllDirectory(directory);
                if (cookie == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "無法加入原廠 AI Runtime 搜尋目錄：" + directory);
                DirectoryCookies.Add(cookie);
            }

            // InspTools imports this compatibility entry by its AppBase name.
            // Chao8Side keeps it byte-identical to Runtime\DeltaAIProcess's copy.
            string compatibilityEntry = Path.Combine(baseDirectory, "DeltaAIProcessDll.dll");
            if (!File.Exists(compatibilityEntry))
                throw new FileNotFoundException("缺少原廠 AI Runtime 的 AppBase 相容 DLL。", compatibilityEntry);
            string canonicalRuntimeEntry = Path.Combine(runtimeDirectory, "DeltaAIProcess", "DeltaAIProcessDll.dll");
            if (!File.Exists(canonicalRuntimeEntry) || !FilesMatch(compatibilityEntry, canonicalRuntimeEntry))
                throw new InvalidDataException("AppBase 與 Runtime 的 DeltaAIProcessDll.dll 不一致。請重新部署原廠 Runtime。");

            _deltaAiProcessModule = LoadLibraryEx(
                compatibilityEntry,
                IntPtr.Zero,
                LoadLibrarySearchDllLoadDir | LoadLibrarySearchDefaultDirectories);
            if (_deltaAiProcessModule == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "DeltaAIProcess 載入錯誤：" + compatibilityEntry);

            _initializedBaseDirectory = baseDirectory;
        }
    }

    private static bool FilesMatch(string firstPath, string secondPath)
    {
        var first = new FileInfo(firstPath);
        var second = new FileInfo(secondPath);
        if (first.Length != second.Length) return false;
        using (SHA256 sha256 = SHA256.Create())
        using (FileStream firstStream = File.OpenRead(firstPath))
        using (FileStream secondStream = File.OpenRead(secondPath))
        {
            byte[] firstHash = sha256.ComputeHash(firstStream);
            byte[] secondHash = sha256.ComputeHash(secondStream);
            for (int i = 0; i < firstHash.Length; i++)
                if (firstHash[i] != secondHash[i]) return false;
        }
        return true;
    }
}
