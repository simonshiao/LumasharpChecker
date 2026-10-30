using System;
using System.Drawing;

namespace LumasharpChecker.model;

// Image-pixel geometry shared by mouse editing and keyboard nudges.
internal static class RoiEditorGeometry
{
    [Flags]
    internal enum Part { None = 0, Left = 1, Top = 2, Right = 4, Bottom = 8, Move = 16, Draw = 32 }
    internal static readonly Part[] Handles = { Part.Left | Part.Top, Part.Top, Part.Right | Part.Top, Part.Right,
        Part.Right | Part.Bottom, Part.Bottom, Part.Left | Part.Bottom, Part.Left };

    internal static PointF Anchor(Rectangle roi, Part part) => new(
        (part & Part.Left) != 0 ? roi.Left : (part & Part.Right) != 0 ? roi.Right : roi.Left + roi.Width / 2f,
        (part & Part.Top) != 0 ? roi.Top : (part & Part.Bottom) != 0 ? roi.Bottom : roi.Top + roi.Height / 2f);

    internal static Part HitTest(Rectangle roi, double x, double y, double tolerance)
    {
        Part nearest = Part.None; double best = tolerance * tolerance;
        foreach (var part in Handles)
        {
            var p = Anchor(roi, part); double distance = (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y);
            if (distance <= best) { nearest = part; best = distance; }
        }
        if (nearest != Part.None) return nearest;
        if (y >= roi.Top - tolerance && y <= roi.Bottom + tolerance)
        {
            if (Math.Abs(x - roi.Left) <= tolerance) return Part.Left;
            if (Math.Abs(x - roi.Right) <= tolerance) return Part.Right;
        }
        if (x >= roi.Left - tolerance && x <= roi.Right + tolerance)
        {
            if (Math.Abs(y - roi.Top) <= tolerance) return Part.Top;
            if (Math.Abs(y - roi.Bottom) <= tolerance) return Part.Bottom;
        }
        return x >= roi.Left && x <= roi.Right && y >= roi.Top && y <= roi.Bottom ? Part.Move : Part.None;
    }

    internal static Rectangle Transform(Rectangle original, Rectangle bounds, Part part, double dx, double dy)
    {
        int x = (int)Math.Round(dx), y = (int)Math.Round(dy);
        if (part == Part.Move)
            return new Rectangle(Clamp(original.X + x, bounds.Left, bounds.Right - original.Width),
                Clamp(original.Y + y, bounds.Top, bounds.Bottom - original.Height), original.Width, original.Height);
        int l = original.Left, t = original.Top, r = original.Right, b = original.Bottom;
        if ((part & Part.Left) != 0) l = Clamp(l + x, bounds.Left, r - 2);
        if ((part & Part.Right) != 0) r = Clamp(r + x, l + 2, bounds.Right);
        if ((part & Part.Top) != 0) t = Clamp(t + y, bounds.Top, b - 2);
        if ((part & Part.Bottom) != 0) b = Clamp(b + y, t + 2, bounds.Bottom);
        return Rectangle.FromLTRB(l, t, r, b);
    }

    internal static Rectangle Draw(Rectangle bounds, double startX, double startY, double endX, double endY)
    {
        int l = Clamp((int)Math.Min(startX, endX), bounds.Left, bounds.Right), r = Clamp((int)Math.Max(startX, endX), bounds.Left, bounds.Right);
        int t = Clamp((int)Math.Min(startY, endY), bounds.Top, bounds.Bottom), b = Clamp((int)Math.Max(startY, endY), bounds.Top, bounds.Bottom);
        return Rectangle.FromLTRB(l, t, r, b);
    }
    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(value, max));
}
