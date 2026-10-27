using LumasharpChecker.viewmodel;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using System.Runtime.InteropServices;
namespace LumasharpChecker
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const string MutexName = "Global\\7F57B2C-69C4-491D-8531-DD264CF3100D"; // 建议使用GUID
        private static Mutex? _mutex;
        public App()
        {
            Cyntec.Logger.Logmanager.init();
        }
        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                // 激活已有实例窗口
                ActivateExistingWindow();
                Shutdown();
                return;
            }
            // 註冊服務
            var services = new ServiceCollection();
            services.AddSingleton<mainViewModel.IWindowService, mainViewModel.WindowService>();
            services.AddTransient<mainViewModel>();


            var provider = services.BuildServiceProvider();
            // 設定主視窗 DataContext
            var mainWindow = new MainWindow();
            mainWindow.DataContext = provider.GetRequiredService<mainViewModel>();
            mainWindow.Show();
        }
        private void ActivateExistingWindow()
        {
            // 查找当前进程的同名进程
            var current = Process.GetCurrentProcess();
            foreach (var process in Process.GetProcessesByName(current.ProcessName))
            {
                if (process.Id == current.Id) continue;

                // 获取 WPF 主窗口句柄
                IntPtr hWnd = GetMainWindowHandle(process);
                if (hWnd != IntPtr.Zero)
                {
                    NativeMethods.SetForegroundWindow(hWnd);  // 置顶窗口
                    if (NativeMethods.IsIconic(hWnd))         // 如果最小化则恢复
                        NativeMethods.ShowWindow(hWnd, 9);     // SW_RESTORE = 9
                    break;
                }
            }
        }
        private IntPtr GetMainWindowHandle(Process process)
        {
            if (process.MainWindowHandle != IntPtr.Zero)
                return process.MainWindowHandle;

            // 遍历所有窗口查找 WPF 主窗口
            IntPtr hWnd = IntPtr.Zero;
            NativeMethods.EnumWindows((ptr, param) =>
            {
                uint processId;
                NativeMethods.GetWindowThreadProcessId(ptr, out processId);
                if (processId == process.Id && NativeMethods.IsWindowVisible(ptr))
                {
                    hWnd = ptr;
                    return false; // 终止枚举
                }
                return true;
            }, IntPtr.Zero);

            return hWnd;
        }

        internal static class NativeMethods
        {
            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll")]
            public static extern bool IsIconic(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool IsWindowVisible(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern int GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

            [DllImport("user32.dll")]
            public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
            public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        }

    }
}
