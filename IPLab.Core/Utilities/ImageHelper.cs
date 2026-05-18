using OpenCvSharp;

namespace IPLab.Core.Utilities;

public static class ImageHelper
{
    /// <summary>
    /// Returns PNG-encoded bytes if <paramref name="result"/> is a Mat, otherwise null.
    /// Lets callers (e.g. the UI) work with plain bytes without a direct OpenCvSharp reference.
    /// </summary>
    public static byte[]? TryGetPngBytes(object? result)
    {
        if (result is Mat mat && !mat.IsDisposed && !mat.Empty())
            return mat.ToBytes(".png");
        return null;
    }
}
