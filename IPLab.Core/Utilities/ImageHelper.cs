using OpenCvSharp;

namespace IPLab.Core.Utilities;

/// <summary>Utility methods for converting OpenCV <see cref="OpenCvSharp.Mat"/> objects for use outside the core library.</summary>
public static class ImageHelper
{
    /// <summary>
    /// Returns BMP-encoded bytes if <paramref name="result"/> is a Mat, otherwise null.
    /// BMP is used (not PNG) because it is uncompressed and encodes ~10x faster.
    /// Lets callers (e.g. the UI) work with plain bytes without a direct OpenCvSharp reference.
    /// </summary>
    public static byte[]? TryGetPngBytes(object? result)
    {
        if (result is Mat mat && !mat.IsDisposed && !mat.Empty())
            return mat.ToBytes(".bmp");
        return null;
    }
}
