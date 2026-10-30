using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LumasharpChecker.view
{
    /// <summary>Windows Explorer-style folder picker for .NET Framework 4.7.2.
    /// Uses Windows' native IFileOpenDialog (FOS_PICKFOLDERS), not the legacy tree dialog.
    /// </summary>
    internal static class ExplorerFolderPicker
    {
        private const uint FosPickFolders = 0x00000020;
        private const uint FosForceFileSystem = 0x00000040;
        private const uint FosPathMustExist = 0x00000800;
        private const uint SigdnFileSysPath = 0x80058000;
        private const int HResultCancelled = unchecked((int)0x800704C7);

        public static string? PickFolder(Window? owner, string title, string? initialFolder)
        {
            var type = Type.GetTypeFromCLSID(new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7"), true)
                ?? throw new InvalidOperationException("Windows folder picker is unavailable.");
            var dialog = (IFileOpenDialog)Activator.CreateInstance(type)!;
            try
            {
                dialog.GetOptions(out uint options);
                dialog.SetOptions(options | FosPickFolders | FosForceFileSystem | FosPathMustExist);
                dialog.SetTitle(title);
                if (!string.IsNullOrWhiteSpace(initialFolder))
                {
                    string path = initialFolder;
                    while (!Directory.Exists(path) && !string.IsNullOrEmpty(path))
                        path = Path.GetDirectoryName(path) ?? string.Empty;
                    if (Directory.Exists(path))
                    {
                        Guid iid = typeof(IShellItem).GUID;
                        int result = SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out IShellItem shell);
                        if (result >= 0)
                        {
                            try { dialog.SetFolder(shell); }
                            finally { Marshal.ReleaseComObject(shell); }
                        }
                    }
                }
                IntPtr hwnd = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;
                int hr = dialog.Show(hwnd);
                if (hr == HResultCancelled) return null;
                Marshal.ThrowExceptionForHR(hr);
                dialog.GetResult(out IShellItem item);
                try
                {
                    item.GetDisplayName(SigdnFileSysPath, out IntPtr pointer);
                    try { return Marshal.PtrToStringUni(pointer); }
                    finally { Marshal.FreeCoTaskMem(pointer); }
                }
                finally { Marshal.ReleaseComObject(item); }
            }
            finally { Marshal.ReleaseComObject(dialog); }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr bindContext,
            ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr bindContext, ref Guid bhid, ref Guid iid, out IntPtr result);
            void GetParent(out IShellItem parent);
            void GetDisplayName(uint nameFormat, out IntPtr name);
            void GetAttributes(uint mask, out uint attributes);
            void Compare(IShellItem other, uint hint, out int result);
        }

        [ComImport, Guid("D57C7288-D4AD-4768-BE02-9D969532D960"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes(uint count, IntPtr filters);
            void SetFileTypeIndex(uint index);
            void GetFileTypeIndex(out uint index);
            void Advise(IntPtr events, out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(uint options);
            void GetOptions(out uint options);
            void SetDefaultFolder(IShellItem folder);
            void SetFolder(IShellItem folder);
            void GetFolder(out IShellItem folder);
            void GetCurrentSelection(out IShellItem item);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName(out IntPtr name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void GetResult(out IShellItem item);
            void AddPlace(IShellItem item, uint placement);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
            void Close(int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr filter);
            void GetResults(out IntPtr items);
            void GetSelectedItems(out IntPtr items);
        }
    }
}
