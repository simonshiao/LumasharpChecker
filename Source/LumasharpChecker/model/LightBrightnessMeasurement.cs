using System;
using System.IO;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace LumasharpChecker.model;

internal static class LightBrightnessMeasurement
{
    internal static double Measure(NativeLocationService.Frame frame, LightInspectionSettings settings, bool fullImage = false)
    {
        var roi = fullImage ? settings.GetFullImageRoi(frame.Bounds) : settings.GetRoi(frame.Bounds);
        if (TryMeasureBitmap(frame.ImagePath, roi, out double brightness)) return brightness;
        using var image = new Mat();
        CvInvoke.Imdecode(File.ReadAllBytes(frame.ImagePath), ImreadModes.Color, image);
        if (roi.X < 0 || roi.Y < 0 || roi.Right > image.Width || roi.Bottom > image.Height)
            throw new InvalidOperationException("光源 ROI 超出定位後影像範圍。");
        using var crop = new Mat(image, roi);
        using var hsv = new Mat();
        CvInvoke.CvtColor(crop, hsv, ColorConversion.Bgr2Hsv);
        return CvInvoke.Mean(hsv).V2;
    }

    // The isolated worker publishes uncompressed BGR BMPs. HSV V is exactly max(B,G,R),
    // so read only the ROI rows without decoding/allocating another full-frame Mat.
    private static bool TryMeasureBitmap(string path, System.Drawing.Rectangle roi, out double brightness)
    {
        brightness = 0;
        using var input = File.OpenRead(path);
        using var reader = new BinaryReader(input);
        if (input.Length < 54 || reader.ReadUInt16() != 0x4D42) return false;
        input.Position = 10;
        uint offset = reader.ReadUInt32();
        uint headerSize = reader.ReadUInt32();
        int width = reader.ReadInt32(), signedHeight = reader.ReadInt32();
        ushort planes = reader.ReadUInt16(), bits = reader.ReadUInt16();
        uint compression = reader.ReadUInt32();
        if (headerSize < 40 || width <= 0 || signedHeight == 0 || signedHeight == int.MinValue ||
            planes != 1 || bits != 24 || compression != 0 || offset < 14L + headerSize) return false;
        int height = Math.Abs(signedHeight);
        if (roi.X < 0 || roi.Y < 0 || roi.Width <= 0 || roi.Height <= 0 ||
            (long)roi.X + roi.Width > width || (long)roi.Y + roi.Height > height)
            throw new InvalidOperationException("光源 ROI 超出定位後影像範圍。");
        long stride = ((long)width * 3 + 3) & ~3L;
        if (offset > input.Length || stride > (input.Length - offset) / height) return false;
        long length = stride * (roi.Height - 1L) + roi.Width * 3L;
        if (length > 16 * 1024 * 1024) return false;
        byte[] pixels = System.Buffers.ArrayPool<byte>.Shared.Rent((int)length);
        try
        {
            long storedRow = signedHeight > 0 ? height - (long)roi.Bottom : roi.Top;
            input.Position = offset + storedRow * stride + roi.X * 3L;
            int read = 0;
            while (read < length)
            {
                int count = input.Read(pixels, read, (int)length - read);
                if (count == 0) throw new EndOfStreamException("光源影像資料不完整。");
                read += count;
            }
            long sum = 0;
            for (int y = 0; y < roi.Height; y++)
            {
                int start = checked((int)(y * stride));
                int end = checked(start + roi.Width * 3);
                for (int x = start; x < end; x += 3)
                {
                    int value = pixels[x];
                    if (pixels[x + 1] > value) value = pixels[x + 1];
                    if (pixels[x + 2] > value) value = pixels[x + 2];
                    sum += value;
                }
            }
            brightness = sum / ((double)roi.Width * roi.Height);
            return true;
        }
        finally { System.Buffers.ArrayPool<byte>.Shared.Return(pixels); }
    }
}
