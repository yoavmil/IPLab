using OpenCvSharp;

namespace IPLab.Core.Utilities;

/// <summary>Extension methods on <see cref="Mat"/> that fill gaps in the available OpenCvSharp API.</summary>
public static class MatExtensions
{
    /// <summary>
    /// Returns the pixel locations of all non-zero elements in a single-channel byte Mat as a
    /// managed <see cref="Point"/> array. Faster than <c>Cv2.FindNonZero</c> for byte masks because
    /// it scans the raw pixel buffer directly without allocating an intermediate <see cref="Mat"/>.
    /// </summary>
    public static unsafe Point[] FindNonZeroPoints(this Mat src)
    {
        if (src.Type() != MatType.CV_8UC1)
            throw new ArgumentException("FindNonZero requires a single-channel byte Mat (CV_8UC1).", nameof(src));

        var result = new List<Point>();
        byte* ptr = (byte*)src.Data;
        int stride = (int)src.Step();
        for (int r = 0; r < src.Rows; r++)
        {
            byte* row = ptr + r * stride;
            for (int c = 0; c < src.Cols; c++)
                if (row[c] != 0)
                    result.Add(new Point(c, r));
        }
        return [.. result];
    }
}
