using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InspTools.AOI;
using InspTools.Station;
using OpenCvSharp;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "detect")
        {
            Environment.Exit(NativeDetection.Run(args));
            return;
        }
        try
        {
            if (args.Length != 4 || args[0] != "locate" || !File.Exists(args[1]))
                throw new FileNotFoundException("無法讀取 Luma 目前的圖片。", args.Length > 1 ? args[1] : "");
            int stationNumber;
            if (!int.TryParse(args[3], out stationNumber) || stationNumber < 1 || stationNumber > 8)
                throw new ArgumentOutOfRangeException("station", "原廠定位目前僅支援第 1 至 8 站。");
            string source = Path.GetFullPath(args[2]);
            if (!File.Exists(Path.Combine(source, "station" + stationNumber + ".ini")))
                throw new FileNotFoundException("此站缺少原廠定位參數。");
            string isolatedName = "luma_preview_" + Guid.NewGuid().ToString("N");
            string isolatedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Recipe", isolatedName);
            CopySnapshot(source, isolatedPath);
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            VendorNativeRuntime.Initialize(AppDomain.CurrentDomain.BaseDirectory);
            // 原廠 CheckAuth 會建立並關閉臨時 AuthWindow。WPF 預設
            // OnLastWindowClose 會在關閉那個授權窗時提前 Shutdown，
            // 導致後續定位視窗一閃即逝，整個程序以 exit code 0 結束。
            // GUI host 需由真正的 LocateWindow 關閉事件控制生命週期。
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            using (var image = Cv2.ImDecode(File.ReadAllBytes(args[1]), ImreadModes.Color))
            {
                if (image.Empty()) throw new InvalidDataException("圖片解碼失敗。");
                var station = new CVInspection();
                station.SetStationIndex(stationNumber - 1);
                string error = "";
                station.SetRecipe(isolatedName, stationNumber - 1, ref error);
                if (!string.IsNullOrWhiteSpace(error)) throw new InvalidDataException(error);
                VendorAiLocateAccessResult authorization = VendorAiLocateAccess.CheckAuthorization(station);
                station.SetBuffers(image);
                bool recipeAiEnabled = station.paramFile.LocateParam.EnableAILocate;
                string aiStatus = recipeAiEnabled
                    ? authorization.Authorized
                        ? VendorAiLocateAccess.LoadLocateModel(station)
                        : authorization.Message
                    : "此 Recipe 未啟用 AI 定位";
                bool aiModelReady = aiStatus.StartsWith("AI 定位模型已載入：", StringComparison.Ordinal);
                var dialog = new LocateWindow();
                dialog.Closed += (_, __) => app.Shutdown();
                dialog.SetInspection(station);
                // LocateWindow.SetBuffer runs AI pre-location whenever the Recipe
                // enables it; suppress that call unless the real vendor license
                // and model loader both confirmed readiness.
                bool runAiPreview = recipeAiEnabled && authorization.Authorized && aiModelReady;
                bool savedAiSetting = station.paramFile.LocateParam.EnableAILocate;
                if (recipeAiEnabled && !runAiPreview)
                    station.paramFile.LocateParam.EnableAILocate = false;
                try { dialog.SetBuffer(image); }
                finally { station.paramFile.LocateParam.EnableAILocate = savedAiSetting; }
                dialog.SetPreviewMode(true);
                dialog.Title = "原廠定位預覽（不保存）｜" + aiStatus;
                dialog.Loaded += (_, __) =>
                {
                    MakeReadOnly(dialog);
                    // Chao8Side 的原廠 LocateWindow 有 AI 定位 UI，但授權
                    // 不足時會隱藏。不可擅改 AllowEnableAILocate 或 CheckAuth；
                    // 在同一個定位工具內顯示唯讀的 AI 定位狀態頁即可。
                    ApplyChao8SideBluePreview(dialog);
                    AddReadOnlyAiLocateTab(dialog, station, recipeAiEnabled,
                        authorization.Authorized, aiModelReady, aiStatus);
                    HideRemotePreviewActions(dialog);
                    // The vendor window defaults to OK before any inspection.
                    // Use its visible result field for an explicit AI readiness
                    // badge so users can see why the gated native controls are
                    // unavailable without changing the vendor permission flag.
                    var result = dialog.FindName("Label_Result") as Label;
                    if (result != null)
                    {
                        result.Content = GetAiDisplayStatus(recipeAiEnabled, authorization.Authorized, aiModelReady);
                        result.ToolTip = aiStatus;
                    }
                };
                app.Run(dialog);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, "InspTools 定位", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        // Some vendor components own worker threads; terminate this isolated host on close.
        Environment.Exit(0);
    }

    private static void CopySnapshot(string source, string destination)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("原廠資料包含連結，無法開啟。");
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("原廠資料包含連結，無法開啟。");
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), false);
        }
        foreach (string dir in Directory.GetDirectories(source))
            CopySnapshot(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    private static void MakeReadOnly(DependencyObject node)
    {
        // Tabs remain selectable, but the vendor buttons can train models,
        // change files or launch tests against paths stored in the source INI.
        var button = node as Button;
        if (button != null) button.IsEnabled = false;
        var check = node as CheckBox;
        if (check != null) check.IsEnabled = false;
        var radio = node as RadioButton;
        if (radio != null) radio.IsEnabled = false;
        var combo = node as ComboBox;
        if (combo != null) combo.IsEnabled = false;
        var text = node as TextBox;
        if (text != null) text.IsReadOnly = true;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            MakeReadOnly(VisualTreeHelper.GetChild(node, i));
    }

    private static readonly Brush PreviewBlue =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078B8"));
    private static readonly Brush PreviewBackground =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAF6FC"));
    private static readonly Brush PreviewLine =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B6DAEA"));

    private static void ApplyChao8SideBluePreview(LocateWindow window)
    {
        // 原廠 Chao8Side 的亮色工作站主題以 #0078B8 與 #18C7F4 為主。
        // 只調整預覽視窗外觀，原廠定位參數與影像控制項維持不變。
        window.Background = PreviewBackground;
        window.BorderBrush = PreviewBlue;
        window.BorderThickness = new Thickness(1);
        var tabs = FindLocateTabs(window);
        if (tabs != null)
        {
            tabs.BorderBrush = PreviewLine;
            foreach (object item in tabs.Items)
                if (item is TabItem tab)
                    tab.Foreground = PreviewBlue;
        }
    }

    private static TabControl FindLocateTabs(DependencyObject node)
    {
        var tabs = node as TabControl;
        if (tabs != null)
        {
            bool locateTabs = false;
            foreach (object item in tabs.Items)
            {
                var tab = item as TabItem;
                if (tab != null && (Convert.ToString(tab.Header) == "簡易定位" ||
                                    Convert.ToString(tab.Header) == "四邊定位"))
                    locateTabs = true;
            }
            if (locateTabs) return tabs;
        }
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            TabControl child = FindLocateTabs(VisualTreeHelper.GetChild(node, i));
            if (child != null) return child;
        }
        return null;
    }

    private static void AddReadOnlyAiLocateTab(
        LocateWindow window, CVInspection station, bool recipeEnabled,
        bool authorized, bool modelReady, string detail)
    {
        TabControl tabs = FindLocateTabs(window);
        if (tabs == null) return;
        foreach (object item in tabs.Items)
            if (item is TabItem tab && Convert.ToString(tab.Header) == "AI 定位")
                return;

        var page = new StackPanel { Margin = new Thickness(20, 18, 20, 12) };
        page.Children.Add(new TextBlock
        {
            Text = "AI 定位", FontSize = 20, FontWeight = FontWeights.SemiBold,
            Foreground = PreviewBlue, Margin = new Thickness(0, 0, 0, 16)
        });
        var details = new Border
        {
            Background = Brushes.White, BorderBrush = PreviewLine,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(18, 15, 18, 15)
        };
        var content = new StackPanel();
        AddAiInfoRow(content, "Recipe", recipeEnabled ? "已啟用 AI 定位" : "未啟用 AI 定位");
        AddAiInfoRow(content, "模型", station.paramFile.LocateParam.AILocateModelName);
        AddAiInfoRow(content, "原廠授權", authorized ? "已通過" : "未通過");
        AddAiInfoRow(content, "模型載入", modelReady ? "已就緒" : "尚未就緒");
        details.Child = content;
        page.Children.Add(details);
        page.Children.Add(new TextBlock
        {
            Text = detail, Margin = new Thickness(0, 14, 0, 0),
            TextWrapping = TextWrapping.Wrap, Foreground = PreviewBlue
        });
        page.Children.Add(new TextBlock
        {
            Text = "此頁僅顯示原廠 AI 定位狀態。原廠授權或模型未就緒時，不執行 AI 推論、訓練或儲存。",
            Margin = new Thickness(0, 12, 0, 0),
            Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        });

        var aiTab = new TabItem { Header = "AI 定位", Foreground = PreviewBlue };
        aiTab.Content = new ScrollViewer
        {
            Content = page, Background = PreviewBackground,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        tabs.Items.Add(aiTab);
    }

    private static void AddAiInfoRow(Panel parent, string label, string value)
    {
        var row = new DockPanel { Margin = new Thickness(0, 5, 0, 5) };
        var name = new TextBlock
        {
            Text = label, Width = 115, Foreground = Brushes.SlateGray
        };
        DockPanel.SetDock(name, Dock.Left);
        row.Children.Add(name);
        row.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(value) ? "未指定" : value,
            Foreground = new SolidColorBrush(Color.FromRgb(24, 52, 73)),
            FontWeight = FontWeights.Medium, TextWrapping = TextWrapping.Wrap
        });
        parent.Children.Add(row);
    }

    private static void HideRemotePreviewActions(DependencyObject node)
    {
        // Chao8Side 的定位工作區並非遠端訓練入口。僅隱藏遠端訓練選項，
        // 不更動原廠的 AI 定位參數、站別資訊或授權狀態。
        string text = null;
        if (node is ContentControl control)
            text = control.Content as string;
        else if (node is TextBlock block)
            text = block.Text;
        if (text != null && (text == "遠端模式" || text == "遠端訓練模式" ||
                             text == "本地+遠端訓練開始" || text == "遠端訓練開始"))
        {
            var element = node as UIElement;
            if (element != null) element.Visibility = Visibility.Collapsed;
        }
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            HideRemotePreviewActions(VisualTreeHelper.GetChild(node, i));
    }

    private static string GetAiDisplayStatus(bool recipeEnabled, bool authorized, bool modelReady)
    {
        if (!recipeEnabled) return "AI 定位：Recipe 未啟用";
        if (!authorized) return "AI 定位：授權未通過";
        return modelReady ? "AI 定位：模型已就緒" : "AI 定位：模型未就緒";
    }
}
