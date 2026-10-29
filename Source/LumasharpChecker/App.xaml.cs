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
        // 獨立減肥版識別：避免與原 .NET 8 正式版共用單實例鎖。
        private const string MutexName = "Global\\AB542812-CCE0-47AD-9DEB-362FCE4F4720";
        private static Mutex? _mutex;
        public App()
        {
            // 部署包可從檔案總管或捷徑執行，所有 Recipe／快取路徑都固定相對 EXE 目錄。
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
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
            var viewModel = provider.GetRequiredService<mainViewModel>();
            mainWindow.DataContext = viewModel;
            // Explicit one-shot import for automated setup; normal startup is unchanged.
            var importArg = e.Args.FirstOrDefault(arg =>
                arg.StartsWith("--import-trainingimage=", StringComparison.OrdinalIgnoreCase));
            if (importArg != null)
            {
                try
                {
                    string folder = importArg.Substring("--import-trainingimage=".Length).Trim('"');
                    viewModel.ImportTrainingImageRecipeFromPath(folder);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("TrainingImage 自動匯入失敗：" + ex.Message,
                        "原廠影像匯入", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            mainWindow.Show();
            if (e.Args.Any(arg => string.Equals(arg, "--show-multi-appearance-preview", StringComparison.OrdinalIgnoreCase)))
            {
                mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                    viewModel.OpenMultiAppearanceCalibrationPreviewCommand.Execute(null)));
            }
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
