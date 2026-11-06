using System.Drawing;

namespace LumasharpChecker.model;

public sealed class LightInspectionSettings
{
    public int Version { get; set; } = 1;
    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }
    public bool HasRoi { get; set; }
    // Independent, resolution-relative ROI for images used without localization.
    public double FullImageX { get; set; }
    public double FullImageY { get; set; }
    public double FullImageWidth { get; set; }
    public double FullImageHeight { get; set; }
    public bool HasFullImageRoi { get; set; }
    public double Minimum { get; set; }
    public double Maximum { get; set; } = 255;
    public string LimitMode { get; set; } = "百分比偏差";
    public double BaseValue { get; set; }
    public double FullImageBaseValue { get; set; }
    public double PercentLower { get; set; } = 10;
    public double PercentUpper { get; set; } = 10;

    public (double Min, double Max) EffectiveLimits(bool fullImage = false)
    {
        Validate();
        if (LimitMode != "百分比偏差") return (Minimum, Maximum);
        double baseline = fullImage ? FullImageBaseValue : BaseValue;
        if (baseline <= 0) throw new InvalidOperationException("百分比模式必須先設定有效的基準亮度。");
        return (System.Math.Max(0, baseline * (1 - PercentLower / 100.0)),
                System.Math.Min(255, baseline * (1 + PercentUpper / 100.0)));
    }

    public void Validate()
    {
        if (Version != 1 || Left < 0 || Top < 0 || Right < 0 || Bottom < 0 ||
            double.IsNaN(Minimum) || double.IsNaN(Maximum) || Minimum < 0 || Maximum > 255 || Minimum > Maximum ||
            (LimitMode != "固定上下限" && LimitMode != "百分比偏差") ||
            double.IsNaN(FullImageX) || double.IsInfinity(FullImageX) ||
            double.IsNaN(FullImageY) || double.IsInfinity(FullImageY) ||
            double.IsNaN(FullImageWidth) || double.IsInfinity(FullImageWidth) ||
            double.IsNaN(FullImageHeight) || double.IsInfinity(FullImageHeight) ||
            FullImageX < 0 || FullImageY < 0 || FullImageWidth < 0 || FullImageHeight < 0 ||
            FullImageX + FullImageWidth > 1 || FullImageY + FullImageHeight > 1 ||
            (HasFullImageRoi && (FullImageWidth <= 0 || FullImageHeight <= 0)) ||
            double.IsNaN(BaseValue) || BaseValue < 0 || BaseValue > 255 ||
            double.IsNaN(FullImageBaseValue) || FullImageBaseValue < 0 || FullImageBaseValue > 255 ||
            double.IsNaN(PercentLower) || double.IsNaN(PercentUpper) ||
            PercentLower < 0 || PercentLower > 100 || PercentUpper < 0 || PercentUpper > 100)
            throw new InvalidOperationException("光源設定無效：ROI 邊距不可為負數，亮度範圍須為 0～255 且下限不大於上限。");
    }

    public Rectangle GetRoi(Rectangle located)
    {
        Validate();
        long w = (long)located.Width - Left - Right, h = (long)located.Height - Top - Bottom;
        if (!HasRoi || w <= 0 || h <= 0) throw new InvalidOperationException("尚未設定 ROI，或定位範圍不足以容納光源 ROI。");
        return new Rectangle(checked(located.X + Left), checked(located.Y + Top), (int)w, (int)h);
    }

    public Rectangle GetFullImageRoi(Rectangle image)
    {
        Validate();
        if (!HasFullImageRoi || image.Width < 2 || image.Height < 2)
            throw new InvalidOperationException("尚未設定整張圖 ROI，或影像尺寸不足。請在「光源檢測」設定 ROI 並儲存。");
        int left = (int)System.Math.Round(FullImageX * image.Width);
        int top = (int)System.Math.Round(FullImageY * image.Height);
        int right = (int)System.Math.Round((FullImageX + FullImageWidth) * image.Width);
        int bottom = (int)System.Math.Round((FullImageY + FullImageHeight) * image.Height);
        int x = image.X + left, y = image.Y + top;
        int width = System.Math.Max(2, right - left);
        int height = System.Math.Max(2, bottom - top);
        if (x < image.X || y < image.Y || (long)x + width > image.Right || (long)y + height > image.Bottom)
            throw new InvalidOperationException("整張圖 ROI 超出影像範圍，請重新調整並儲存。");
        return new Rectangle(x, y, width, height);
    }

    public bool EnsureFullImageRoi(Rectangle image)
    {
        Validate();
        if (HasFullImageRoi)
        {
            try { GetFullImageRoi(image); return false; }
            catch (InvalidOperationException) { }
        }
        ResetFullImageRoi(image);
        return true;
    }

    public void ResetFullImageRoi(Rectangle image)
    {
        if (image.Width < 2 || image.Height < 2)
            throw new InvalidOperationException("整張影像小於 2 × 2 像素，無法設定 ROI。");
        FullImageX = 0.25; FullImageY = 0.25;
        FullImageWidth = 0.5; FullImageHeight = 0.5;
        HasFullImageRoi = true;
    }

    public void SetFullImageRoi(Rectangle roi, Rectangle image)
    {
        if (image.Width < 2 || image.Height < 2 || roi.Width < 2 || roi.Height < 2 ||
            roi.X < image.X || roi.Y < image.Y || roi.Right > image.Right || roi.Bottom > image.Bottom)
            throw new InvalidOperationException("整張圖 ROI 必須在影像範圍內且至少 2 × 2 像素。");
        FullImageX = (double)(roi.X - image.X) / image.Width;
        FullImageY = (double)(roi.Y - image.Y) / image.Height;
        FullImageWidth = (double)roi.Width / image.Width;
        FullImageHeight = (double)roi.Height / image.Height;
        HasFullImageRoi = true;
        Validate();
    }

    // Editing may start from another face's pixel margins. Recover only in the editor;
    // inspection keeps GetRoi's strict validation and never silently changes a saved ROI.
    public bool EnsureEditableRoi(Rectangle located)
    {
        Validate();
        if (HasRoi && (long)located.Width - Left - Right >= 2 &&
            (long)located.Height - Top - Bottom >= 2) return false;
        ResetRoi(located);
        return true;
    }

    public void ResetRoi(Rectangle located)
    {
        if (located.Width < 2 || located.Height < 2)
            throw new InvalidOperationException("定位範圍小於 2 × 2 像素，請先確認定位結果。");
        int width = System.Math.Max(2, located.Width / 2);
        int height = System.Math.Max(2, located.Height / 2);
        Left = (located.Width - width) / 2;
        Top = (located.Height - height) / 2;
        Right = located.Width - Left - width;
        Bottom = located.Height - Top - height;
        HasRoi = true;
    }
}
