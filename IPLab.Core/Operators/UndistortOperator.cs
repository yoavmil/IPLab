using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>
/// Applies bilinear warp correction to an image using a corner-correspondence calibration file
/// produced by <see cref="DistortionCalibrationOperator"/>. The output is rotated so the
/// checkerboard axes align with the image axes, with black fill where coverage is absent.
/// </summary>
/// <remarks>
/// The dense warp maps are expensive to build, so they are cached and rebuilt only when the
/// calibration file (path or last-write time) or the input image size changes.
/// <see cref="ICacheInvalidationProvider"/> surfaces the file's last-write time to
/// <see cref="Runtime.FlowEx"/> so the run-level cache re-executes when the file is overwritten.
/// </remarks>
/// <seealso href="https://github.com/yoavmil/IPLab/blob/master/docs/OPERATORS.md#undistort">Operator reference</seealso>
public class UndistortOperator : IOperatorType, ICacheInvalidationProvider
{
    /// <inheritdoc/>
    public string TypeName => "Undistort";
    /// <inheritdoc/>
    public string Category => "Calibration";
    /// <inheritdoc/>
    public string Icon     => "calibration";

    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",         Label = "Image",            ConnectableType = typeof(Mat) },
        new() { Name = "CalibFilePath", Label = "Calibration File", Type = ParameterType.String, ConnectableType = typeof(string) },
    ];

    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "Image", DataType = typeof(Mat), IsDisplayImage = true },
    ];

    private readonly object _gate = new();
    private string? _cachedPath;
    private long    _cachedMtime;
    private int     _cachedW, _cachedH;
    private Mat?    _map1, _map2;

    /// <inheritdoc/>
    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        if (parameters.GetValueOrDefault("Image") is not Mat image || image.Empty())
            throw new ArgumentException("Undistort requires an input image.");

        var path = parameters.GetValueOrDefault("CalibFilePath") as string;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new ArgumentException($"Undistort requires an existing calibration file. Path: '{path}'.");

        long mtime       = File.GetLastWriteTimeUtc(path).Ticks;
        var (map1, map2) = GetMaps(path!, mtime, image.Width, image.Height);

        var dst = new Mat();
        Cv2.Remap(image, dst, map1, map2, InterpolationFlags.Linear, BorderTypes.Constant);
        return new Dictionary<string, object?> { ["Image"] = dst };
    }

    private (Mat Map1, Mat Map2) GetMaps(string path, long mtime, int width, int height)
    {
        lock (_gate)
        {
            if (_map1 is not null && _map2 is not null &&
                _cachedPath == path && _cachedMtime == mtime &&
                _cachedW == width  && _cachedH == height)
                return (_map1, _map2);

            var data = CalibrationHelpers.Load(path);
            if (data.ImageWidth > 0 && data.ImageHeight > 0 &&
                (data.ImageWidth != width || data.ImageHeight != height))
                throw new ArgumentException(
                    $"Image size {width}×{height} does not match calibration size " +
                    $"{data.ImageWidth}×{data.ImageHeight}.");

            if (data.Corners.Count == 0)
                throw new InvalidDataException("Calibration file contains no corner data.");

            // Build corner map (col, row) → image position and fill sparse gaps.
            Dictionary<(int Col, int Row), Point2f> cornerMap;
            try
            {
                cornerMap = data.Corners.ToDictionary(
                    c => (c.Col, c.Row),
                    c => new Point2f((float)c.ImgX, (float)c.ImgY));
            }
            catch (ArgumentException ex)
            {
                throw new InvalidDataException(
                    "Calibration matrix must be non-empty: duplicate (col, row) corner entries found in calibration file.", ex);
            }

            // Need at least a 2×2 arrangement (4 corners) to form one bilinear cell.
            if (cornerMap.Count < 4)
                throw new InvalidDataException(
                    "Calibration matrix must be non-empty: at least 4 distinct grid corners are required " +
                    $"for bilinear interpolation (file contains {cornerMap.Count}).");

            if (data.Pitch <= 0f)
                throw new InvalidDataException(
                    "Calibration file is outdated (missing precomputed pitch). " +
                    "Re-run DistortionCalibration to regenerate it.");

            float pitch   = data.Pitch;
            float ax      = (float)(data.AnchorX * width);
            float ay      = (float)(data.AnchorY * height);
            float meanCol = data.MeanCol;
            float meanRow = data.MeanRow;

            // For each output pixel, compute the fractional grid coordinate, find the
            // enclosing grid cell, and bilinear-blend the four corner source positions.
            var map1 = new Mat(height, width, MatType.CV_32FC1);
            var map2 = new Mat(height, width, MatType.CV_32FC1);
            var idx1 = map1.GetGenericIndexer<float>();
            var idx2 = map2.GetGenericIndexer<float>();

            for (int oy = 0; oy < height; oy++)
            for (int ox = 0; ox < width; ox++)
            {
                // Map output pixel to fractional grid coordinate.
                // J is up (positive J = up in grid = decreasing image Y).
                float colF = (ox - ax) / pitch + meanCol;
                float rowF = (ay - oy) / pitch + meanRow;

                int   c0 = (int)Math.Floor(colF);
                int   r0 = (int)Math.Floor(rowF);
                float u  = colF - c0;
                float v  = rowF - r0;

                if (!cornerMap.TryGetValue((c0,     r0),     out var p00) ||
                    !cornerMap.TryGetValue((c0 + 1, r0),     out var p10) ||
                    !cornerMap.TryGetValue((c0,     r0 + 1), out var p01) ||
                    !cornerMap.TryGetValue((c0 + 1, r0 + 1), out var p11))
                {
                    // Sentinel: well outside image bounds → Remap + BorderTypes.Constant → black.
                    idx1[oy, ox] = -100f;
                    idx2[oy, ox] = -100f;
                    continue;
                }

                float wu = 1f - u, wv = 1f - v;
                idx1[oy, ox] = wu * wv * p00.X + u * wv * p10.X + wu * v * p01.X + u * v * p11.X;
                idx2[oy, ox] = wu * wv * p00.Y + u * wv * p10.Y + wu * v * p01.Y + u * v * p11.Y;
            }

            _map1?.Dispose();
            _map2?.Dispose();
            (_map1, _map2)       = (map1, map2);
            _cachedPath          = path;
            _cachedMtime         = mtime;
            (_cachedW, _cachedH) = (width, height);
            return (map1, map2);
        }
    }

    /// <inheritdoc/>
    public IEnumerable<KeyValuePair<string, object?>> GetCacheTokens(IReadOnlyDictionary<string, object?> parameters)
    {
        var path  = parameters.GetValueOrDefault("CalibFilePath") as string;
        long mtime = !string.IsNullOrWhiteSpace(path) && File.Exists(path)
            ? File.GetLastWriteTimeUtc(path).Ticks
            : 0L;
        yield return new KeyValuePair<string, object?>("__calibFileMtime", mtime);
    }
}
