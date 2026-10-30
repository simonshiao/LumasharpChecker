using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LumasharpChecker.viewmodel;

public sealed class StationSlotViewModel : ObservableObject
{
    public int Number { get; }
    public string ButtonText => $"{Number:00} {(IsConfigured ? "✓" : "+")}";
    public string StatusText => IsConfigured ? "檢測設定完成" : "尚未建立檢測設定";
    public string OkPath { get; }
    public string NgPath { get; }
    public string OkStatus => string.IsNullOrWhiteSpace(OkPath) ? "OK 圖庫 · 未設定"
        : Directory.Exists(OkPath) ? "OK 圖庫 · 可用" : "OK 圖庫 · 目錄不存在";
    public string NgStatus => string.IsNullOrWhiteSpace(NgPath) ? "NG 圖庫 · 未設定"
        : Directory.Exists(NgPath) ? "NG 圖庫 · 可用" : "NG 圖庫 · 目錄不存在";
    public BitmapImage? PreviewImage { get; }
    public string PreviewLabel { get; }
    public string AiModelStatus { get; }
    public string AiModelPathHint { get; }
    private bool _includeInBatch = true;
    public bool IncludeInBatch
    {
        get => _includeInBatch;
        set => SetProperty(ref _includeInBatch, value);
    }

    private int _batchOkCount;
    public int BatchOkCount
    {
        get => _batchOkCount;
        private set => SetProperty(ref _batchOkCount, value);
    }

    private int _batchNgCount;
    public int BatchNgCount
    {
        get => _batchNgCount;
        private set => SetProperty(ref _batchNgCount, value);
    }

    private string _batchStatus = "尚未測試";
    public string BatchStatus
    {
        get => _batchStatus;
        private set
        {
            if (SetProperty(ref _batchStatus, value))
                OnPropertyChanged(nameof(BatchStatusBrush));
        }
    }
    public Brush BatchStatusBrush => BatchStatus switch
    {
        "完成" => Brushes.MediumSeaGreen,
        "檢測中" => Brushes.DeepSkyBlue,
        "執行失敗" or "載入失敗" or "讀圖失敗" => Brushes.IndianRed,
        "未設定" or "未校準" or "缺設定" or "無圖片" => Brushes.DarkOrange,
        _ => Brushes.SlateGray
    };

    private string _batchYield = "—";
    public string BatchYield
    {
        get => _batchYield;
        private set => SetProperty(ref _batchYield, value);
    }
    public string BatchOkText => BatchStatus == "完成" ? BatchOkCount.ToString("N0") : "—";
    public string BatchNgText => BatchStatus == "完成" ? BatchNgCount.ToString("N0") : "—";
    public double BatchYieldValue => BatchStatus == "完成" && BatchOkCount + BatchNgCount > 0
        ? BatchOkCount * 100.0 / (BatchOkCount + BatchNgCount) : 0;
    public string BatchDetails { get; private set; } = string.Empty;

    public void ApplyBatchResult(int okCount, int ngCount, string status, string details = "")
    {
        BatchOkCount = okCount;
        BatchNgCount = ngCount;
        BatchStatus = status;
        BatchDetails = details;
        BatchYield = status == "完成" && okCount + ngCount > 0
            ? $"{okCount * 100.0 / (okCount + ngCount):F1}%"
            : "—";
        OnPropertyChanged(nameof(BatchOkText));
        OnPropertyChanged(nameof(BatchNgText));
        OnPropertyChanged(nameof(BatchYieldValue));
        OnPropertyChanged(nameof(BatchDetails));
    }

    private bool _isConfigured;
    public bool IsConfigured
    {
        get => _isConfigured;
        set
        {
            if (SetProperty(ref _isConfigured, value))
            {
                OnPropertyChanged(nameof(ButtonText));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public StationSlotViewModel(int number, bool isConfigured, bool isSelected,
        string? patternPath = null, string? okPath = null, string? ngPath = null,
        string[]? vendorAiModels = null)
    {
        Number = number;
        _isConfigured = isConfigured;
        _isSelected = isSelected;
        _batchStatus = isConfigured ? "尚未測試" : "未校準";
        OkPath = okPath ?? string.Empty;
        NgPath = ngPath ?? string.Empty;
        PreviewLabel = IsConfigured ? "檢測基準圖" : "圖庫樣本 · 未校準";
        AiModelStatus = vendorAiModels?.Length > 0
            ? $"原廠 AI 模型 {vendorAiModels.Length} 個（未接入）"
            : "原廠 AI 模型 · 未找到";
        AiModelPathHint = vendorAiModels == null ? string.Empty : string.Join(Environment.NewLine, vendorAiModels);
        // 只解碼總覽所需的縮圖，避免一次載入八張高解析度原圖。
        string? previewPath = patternPath;
        if (string.IsNullOrWhiteSpace(previewPath) || !File.Exists(previewPath))
        {
            previewPath = Directory.Exists(OkPath)
                ? Directory.EnumerateFiles(OkPath).Where(IsPreviewImage).OrderBy(path => path).FirstOrDefault()
                : null;
        }
        if (!string.IsNullOrWhiteSpace(previewPath) && File.Exists(previewPath))
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 240;
                image.UriSource = new Uri(Path.GetFullPath(previewPath));
                image.EndInit();
                image.Freeze();
                PreviewImage = image;
            }
            catch (Exception)
            {
                // 無法讀取縮圖時不影響 Recipe 或單站檢測。
            }
        }
    }

    private static bool IsPreviewImage(string path)
    {
        string ext = Path.GetExtension(path);
        return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }
}
