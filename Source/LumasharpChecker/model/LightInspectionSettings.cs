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
    public double Minimum { get; set; }
    public double Maximum { get; set; } = 255;

    public void Validate()
    {
        if (Version != 1 || Left < 0 || Top < 0 || Right < 0 || Bottom < 0 ||
            double.IsNaN(Minimum) || double.IsNaN(Maximum) || Minimum < 0 || Maximum > 255 || Minimum > Maximum)
            throw new InvalidOperationException("光源設定無效：ROI 邊距不可為負數，亮度範圍須為 0～255 且下限不大於上限。");
    }

    public Rectangle GetRoi(Rectangle located)
    {
        Validate();
        long w = (long)located.Width - Left - Right, h = (long)located.Height - Top - Bottom;
        if (!HasRoi || w <= 0 || h <= 0) throw new InvalidOperationException("尚未設定 ROI，或定位範圍不足以容納光源 ROI。");
        return new Rectangle(checked(located.X + Left), checked(located.Y + Top), (int)w, (int)h);
    }
}
