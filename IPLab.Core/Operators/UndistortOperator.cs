using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>
/// Applies lens-distortion correction using a camera matrix and distortion coefficients loaded
/// from an <see cref="UndistortCalibrationData"/> JSON file. The input may be any type/channel
/// count; its dimensions must match the calibration's image size.
/// </summary>
/// <remarks>
/// The undistortion rectify maps are expensive to build, so they are cached and rebuilt only when
/// the calibration file (path or last-write time) or the input image size changes.
/// <see cref="ICacheInvalidationProvider"/> additionally surfaces the file's last-write time to
/// <see cref="Runtime.FlowEx"/> so its run-level cache re-executes when the file is overwritten in
/// place (same path, new contents).
/// </remarks>
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

    // Cached rectify maps. Rebuilt only when the file (path + mtime) or image size changes. The
    // operator type is a shared singleton, so access is guarded; keying on path/mtime/size keeps it
    // correct even if several Undistort nodes share this instance.
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

        long mtime = File.GetLastWriteTimeUtc(path).Ticks;
        var (map1, map2) = GetMaps(path, mtime, image.Width, image.Height);

        var dst = new Mat();
        Cv2.Remap(image, dst, map1, map2, InterpolationFlags.Linear);
        return new Dictionary<string, object?> { ["Image"] = dst };
    }

    private (Mat Map1, Mat Map2) GetMaps(string path, long mtime, int width, int height)
    {
        lock (_gate)
        {
            if (_map1 is not null && _map2 is not null &&
                _cachedPath == path && _cachedMtime == mtime && _cachedW == width && _cachedH == height)
                return (_map1, _map2);

            var data = UndistortCalibrationFile.Load(path);
            if (data.ImageWidth > 0 && data.ImageHeight > 0 &&
                (data.ImageWidth != width || data.ImageHeight != height))
                throw new ArgumentException(
                    $"Image size {width}×{height} does not match calibration size " +
                    $"{data.ImageWidth}×{data.ImageHeight}.");

            using var camera = Mat.FromArray(UndistortCalibrationFile.ToRectangular(data.CameraMatrix));
            using var dist   = Mat.FromArray(data.DistCoeffs);

            var map1 = new Mat();
            var map2 = new Mat();
            Cv2.InitUndistortRectifyMap(
                camera, dist, new Mat(), camera, new Size(width, height),
                MatType.CV_32FC1, map1, map2);

            _map1?.Dispose();
            _map2?.Dispose();
            (_map1, _map2)             = (map1, map2);
            _cachedPath                = path;
            _cachedMtime               = mtime;
            (_cachedW, _cachedH)       = (width, height);
            return (map1, map2);
        }
    }

    /// <inheritdoc/>
    public IEnumerable<KeyValuePair<string, object?>> GetCacheTokens(IReadOnlyDictionary<string, object?> parameters)
    {
        var path = parameters.GetValueOrDefault("CalibFilePath") as string;
        long mtime = !string.IsNullOrWhiteSpace(path) && File.Exists(path)
            ? File.GetLastWriteTimeUtc(path).Ticks
            : 0L;
        yield return new KeyValuePair<string, object?>("__calibFileMtime", mtime);
    }
}
