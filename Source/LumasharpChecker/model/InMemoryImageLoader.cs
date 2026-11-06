using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace LumasharpChecker.model;

/// <summary>
/// 影像預覽只從原圖讀取一次並在 RAM 解碼，不建立複本或磁碟暫存檔。
/// WPF OnLoad 完成後立即釋放來源檔案控制代碼；由 GC 管理影像記憶體。
/// </summary>
internal static class InMemoryImageLoader
{
    internal static BitmapImage Load(string imagePath, int decodePixelWidth = 0)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            throw new ArgumentException("缺少圖片路徑。", nameof(imagePath));

        using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        if (decodePixelWidth > 0) bitmap.DecodePixelWidth = decodePixelWidth;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
