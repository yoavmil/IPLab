using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>
/// Carries the data needed to back-project crop-local coordinates to original-image coordinates.
/// <see cref="ClampedX"/>/<see cref="ClampedY"/> is the crop's top-left corner in warped-image space
/// (same as image space when <see cref="AngleRad"/> is zero).
/// </summary>
/// <param name="CX">X coordinate of the ROI center in the original image.</param>
/// <param name="CY">Y coordinate of the ROI center in the original image.</param>
/// <param name="AngleRad">ROI rotation angle in radians.</param>
/// <param name="ClampedX">X of the crop's top-left corner in warped-image space.</param>
/// <param name="ClampedY">Y of the crop's top-left corner in warped-image space.</param>
public record RoiTransform(double CX, double CY, double AngleRad, int ClampedX, int ClampedY);

/// <summary>Reusable ROI infrastructure shared by all ROI-supporting operators: schema, output ports, cropping, warping, and back-projection helpers.</summary>
public static class RoiParameters
{
    /// <summary>The five ROI parameter descriptors (CX, CY, W, H, Angle) to spread-include in an operator's <see cref="Interfaces.IOperatorType.ParameterSchema"/>.</summary>
    public static IReadOnlyList<ParameterDescriptor> Schema =>
    [
        new() { Name = "RoiCX",    Label = "ROI Center X", Type = ParameterType.Double, ConnectableType = typeof(double), DefaultValue = 0.0 },
        new() { Name = "RoiCY",    Label = "ROI Center Y", Type = ParameterType.Double, ConnectableType = typeof(double), DefaultValue = 0.0 },
        new() { Name = "RoiW",     Label = "ROI Width",    Type = ParameterType.Double, ConnectableType = typeof(double), DefaultValue = 0.0, Min = 0.0 },
        new() { Name = "RoiH",     Label = "ROI Height",   Type = ParameterType.Double, ConnectableType = typeof(double), DefaultValue = 0.0, Min = 0.0 },
        new() { Name = "RoiAngle", Label = "ROI Angle (°)",Type = ParameterType.Double, ConnectableType = typeof(double), DefaultValue = 0.0 },
    ];

    /// <summary>
    /// The five ROI output port descriptors to spread-include in an operator's <see cref="Interfaces.IOperatorType.OutputPorts"/>
    /// so downstream operators can wire their own ROI parameters to the upstream ROI values.
    /// </summary>
    public static IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "RoiCX",    DataType = typeof(double) },
        new() { Name = "RoiCY",    DataType = typeof(double) },
        new() { Name = "RoiW",     DataType = typeof(double) },
        new() { Name = "RoiH",     DataType = typeof(double) },
        new() { Name = "RoiAngle", DataType = typeof(double) },
    ];

    /// <summary>
    /// Extracts a <see cref="RoiDef"/> from the parameter dictionary.
    /// Returns <see langword="null"/> when W=0 or H=0, indicating the operator should run on the full image.
    /// </summary>
    public static RoiDef? Extract(IReadOnlyDictionary<string, object?> parameters)
    {
        var cx    = Convert.ToDouble(parameters.GetValueOrDefault("RoiCX")    ?? 0.0);
        var cy    = Convert.ToDouble(parameters.GetValueOrDefault("RoiCY")    ?? 0.0);
        var w     = Convert.ToDouble(parameters.GetValueOrDefault("RoiW")     ?? 0.0);
        var h     = Convert.ToDouble(parameters.GetValueOrDefault("RoiH")     ?? 0.0);
        var angle = Convert.ToDouble(parameters.GetValueOrDefault("RoiAngle") ?? 0.0);
        return (w > 0 && h > 0) ? new RoiDef(cx, cy, w, h, angle) : null;
    }

    /// <summary>
    /// Converts the ROI to an axis-aligned crop rectangle in warped-image space (same as original-image space when Angle=0).
    /// The caller must check <c>Width &lt;= 0 || Height &lt;= 0</c> to detect an ROI entirely outside the image.
    /// </summary>
    public static Rect Clamp(RoiDef roi, int imageWidth, int imageHeight)
    {
        int left   = (int)Math.Round(roi.CX - roi.Width  / 2.0);
        int top    = (int)Math.Round(roi.CY - roi.Height / 2.0);
        int right  = left + (int)Math.Round(roi.Width);
        int bottom = top  + (int)Math.Round(roi.Height);
        int cx     = Math.Max(0, left);
        int cy     = Math.Max(0, top);
        int cw     = Math.Min(right, imageWidth)  - cx;
        int ch     = Math.Min(bottom, imageHeight) - cy;
        return new Rect(cx, cy, cw, ch);
    }

    /// <summary>
    /// Convenience overload that extracts the <see cref="RoiDef"/> from <paramref name="parameters"/> first.
    /// Returns <see langword="null"/> when no ROI is set (W=0 or H=0).
    /// </summary>
    public static Rect? Clamp(IReadOnlyDictionary<string, object?> parameters, int imageWidth, int imageHeight)
    {
        var roi = Extract(parameters);
        return roi is null ? null : Clamp(roi, imageWidth, imageHeight);
    }

    /// <summary>
    /// Rotates the image so the ROI becomes axis-aligned. Rotation is around the ROI center.
    /// Always returns a new <see cref="Mat"/> (caller owns it).
    /// </summary>
    public static Mat WarpForRoi(Mat image, RoiDef roi)
    {
        if (roi.Angle == 0.0) return image.Clone();
        var center = new Point2f((float)roi.CX, (float)roi.CY);
        using var rotMat = Cv2.GetRotationMatrix2D(center, -roi.Angle, 1.0);
        var warped = new Mat();
        Cv2.WarpAffine(image, warped, rotMat, image.Size(), InterpolationFlags.Linear, BorderTypes.Replicate);
        return warped;
    }

    /// <summary>Builds the <see cref="RoiTransform"/> record needed to back-project crop-local coordinates to original-image space.</summary>
    public static RoiTransform BuildTransform(RoiDef roi, Rect clampedRect) =>
        new(roi.CX, roi.CY, roi.Angle * Math.PI / 180.0, clampedRect.X, clampedRect.Y);

    /// <summary>
    /// Back-projects a crop-local position (<paramref name="cropX"/>, <paramref name="cropY"/>) to original-image coordinates.
    /// Convention: WarpAffine used <c>GetRotationMatrix2D(center, -angleDeg, 1.0)</c>.
    /// Back-rotation uses +angle: <c>outX = CX + dx*cos + dy*sin</c>, <c>outY = CY - dx*sin + dy*cos</c>.
    /// </summary>
    public static Point2f BackProject(double cropX, double cropY, RoiTransform t)
    {
        double cosA = Math.Cos(t.AngleRad);
        double sinA = Math.Sin(t.AngleRad);
        double dx   = cropX + t.ClampedX - t.CX;
        double dy   = cropY + t.ClampedY - t.CY;
        return new Point2f(
            (float)(t.CX + dx * cosA + dy * sinA),
            (float)(t.CY - dx * sinA + dy * cosA));
    }

    /// <summary>Back-projects a <see cref="Point2f"/> from crop space to original-image space.</summary>
    public static Point2f BackProject(Point2f p, RoiTransform t)
        => BackProject(p.X, p.Y, t);

    /// <summary>Back-projects a <see cref="CircleSegment"/> center from crop space to original-image space, preserving the radius.</summary>
    public static CircleSegment BackProject(CircleSegment c, RoiTransform t)
    {
        var r = BackProject(c.Center.X, c.Center.Y, t);
        return new CircleSegment(r, c.Radius);
    }

    /// <summary>Back-projects a <see cref="KeyPoint"/> position from crop space to original-image space, preserving all other fields.</summary>
    public static KeyPoint BackProject(KeyPoint k, RoiTransform t)
    {
        var r = BackProject(k.Pt.X, k.Pt.Y, t);
        return new KeyPoint(r.X, r.Y, k.Size, k.Angle, k.Response, k.Octave, k.ClassId);
    }

    /// <summary>Back-projects an integer <see cref="OpenCvSharp.Point"/> from crop space to original-image space (rounded to nearest pixel).</summary>
    public static OpenCvSharp.Point BackProject(OpenCvSharp.Point p, RoiTransform t)
    {
        var r = BackProject(p.X, p.Y, t);
        return new OpenCvSharp.Point((int)Math.Round(r.X), (int)Math.Round(r.Y));
    }

    /// <summary>Back-projects every point in a contour array from crop space to original-image space.</summary>
    public static OpenCvSharp.Point[] BackProject(OpenCvSharp.Point[] contour, RoiTransform t)
        => contour.Select(p => BackProject(p, t)).ToArray();

    /// <summary>
    /// Applies <paramref name="process"/> to the (possibly rotated) ROI crop and composites the result back over a clone of the full image.
    /// <list type="bullet">
    ///   <item><description>No ROI set — calls <paramref name="process"/> on the full image directly.</description></item>
    ///   <item><description>ROI entirely outside image — returns a clone of the original unchanged.</description></item>
    ///   <item><description>Axis-aligned ROI — SubMat crop/paste composite.</description></item>
    ///   <item><description>Rotated ROI — WarpAffine → crop → filter → paste → WarpAffine back → mask composite.</description></item>
    /// </list>
    /// </summary>
    public static Mat ApplyImageFilter(Mat image,
                                       IReadOnlyDictionary<string, object?> parameters,
                                       Func<Mat, Mat> process)
    {
        var roi = Extract(parameters);
        if (roi is null) return process(image);

        var rect = Clamp(roi, image.Width, image.Height);
        if (rect.Width <= 0 || rect.Height <= 0) return image.Clone();

        if (roi.Angle == 0.0)
        {
            using var crop   = new Mat(image, rect);
            var       patch  = process(crop);
            var       output = image.Clone();
            using var dst    = new Mat(output, rect);
            patch.CopyTo(dst);
            patch.Dispose();
            return output;
        }

        // Rotated ROI path:
        // 1. Rotate image to align ROI with axes.
        // 2. Crop, filter, paste processed crop back into the rotated image.
        // 3. Rotate the modified image back to original orientation.
        // 4. Composite using a binary mask (only the ROI area is taken from the back-rotated result).
        var center = new Point2f((float)roi.CX, (float)roi.CY);
        using var fwdMat  = Cv2.GetRotationMatrix2D(center, -roi.Angle, 1.0);
        using var bwdMat  = Cv2.GetRotationMatrix2D(center,  roi.Angle, 1.0);

        using var warped  = new Mat();
        Cv2.WarpAffine(image, warped, fwdMat, image.Size(), InterpolationFlags.Linear, BorderTypes.Replicate);

        using var crop2   = new Mat(warped, rect);
        using var patch2  = process(crop2);

        using var warpedOut = warped.Clone();
        using var dst2      = new Mat(warpedOut, rect);
        patch2.CopyTo(dst2);

        using var result  = new Mat();
        Cv2.WarpAffine(warpedOut, result, bwdMat, image.Size(), InterpolationFlags.Linear, BorderTypes.Replicate);

        // Build a mask: white rectangle in warped space → rotate back → composite mask.
        using var maskWarp = new Mat(image.Rows, image.Cols, MatType.CV_8U, Scalar.Black);
        using var maskCrop = new Mat(maskWarp, rect);
        maskCrop.SetTo(Scalar.White);
        using var mask     = new Mat();
        Cv2.WarpAffine(maskWarp, mask, bwdMat, image.Size(), InterpolationFlags.Nearest, BorderTypes.Constant);

        var output2 = image.Clone();
        result.CopyTo(output2, mask);
        return output2;
    }

    /// <summary>Copies all five ROI parameter values through to the output dictionary so downstream operators can wire their own ROI inputs.</summary>
    public static void AddToOutputs(Dictionary<string, object?> outputs,
                                    IReadOnlyDictionary<string, object?> parameters)
    {
        outputs["RoiCX"]    = parameters.GetValueOrDefault("RoiCX");
        outputs["RoiCY"]    = parameters.GetValueOrDefault("RoiCY");
        outputs["RoiW"]     = parameters.GetValueOrDefault("RoiW");
        outputs["RoiH"]     = parameters.GetValueOrDefault("RoiH");
        outputs["RoiAngle"] = parameters.GetValueOrDefault("RoiAngle");
    }
}
