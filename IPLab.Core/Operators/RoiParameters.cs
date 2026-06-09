using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

// Carries the data needed to back-project crop-local coordinates to original-image coordinates.
// ClampedX/Y is the crop's top-left corner in the warped-image space (same as image space when Angle=0).
public record RoiTransform(double CX, double CY, double AngleRad, int ClampedX, int ClampedY);

public static class RoiParameters
{
    public static IReadOnlyList<ParameterDescriptor> Schema =>
    [
        new() { Name = "RoiCX",    Label = "ROI Center X", Type = ParameterType.Double, ConnectableType = typeof(double), DefaultValue = 0.0 },
        new() { Name = "RoiCY",    Label = "ROI Center Y", Type = ParameterType.Double, ConnectableType = typeof(double), DefaultValue = 0.0 },
        new() { Name = "RoiW",     Label = "ROI Width",    Type = ParameterType.Double, ConnectableType = typeof(double), DefaultValue = 0.0, Min = 0.0 },
        new() { Name = "RoiH",     Label = "ROI Height",   Type = ParameterType.Double, ConnectableType = typeof(double), DefaultValue = 0.0, Min = 0.0 },
        new() { Name = "RoiAngle", Label = "ROI Angle (°)",Type = ParameterType.Double, ConnectableType = typeof(double), DefaultValue = 0.0 },
    ];

    // These port descriptors must be included in an operator's OutputPorts when it supports ROI,
    // so downstream operators can wire their own ROI params to the upstream operator's ROI values.
    public static IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "RoiCX",    DataType = typeof(double) },
        new() { Name = "RoiCY",    DataType = typeof(double) },
        new() { Name = "RoiW",     DataType = typeof(double) },
        new() { Name = "RoiH",     DataType = typeof(double) },
        new() { Name = "RoiAngle", DataType = typeof(double) },
    ];

    // Returns null when W=0 or H=0 — operator should run on the full image.
    public static RoiDef? Extract(IReadOnlyDictionary<string, object?> parameters)
    {
        var cx    = Convert.ToDouble(parameters.GetValueOrDefault("RoiCX")    ?? 0.0);
        var cy    = Convert.ToDouble(parameters.GetValueOrDefault("RoiCY")    ?? 0.0);
        var w     = Convert.ToDouble(parameters.GetValueOrDefault("RoiW")     ?? 0.0);
        var h     = Convert.ToDouble(parameters.GetValueOrDefault("RoiH")     ?? 0.0);
        var angle = Convert.ToDouble(parameters.GetValueOrDefault("RoiAngle") ?? 0.0);
        return (w > 0 && h > 0) ? new RoiDef(cx, cy, w, h, angle) : null;
    }

    // Returns the axis-aligned crop rect in warped-image space (same as original-image space
    // when Angle=0, because WarpAffine preserves image dimensions).
    // Caller must check Width <= 0 || Height <= 0 (ROI entirely outside the image).
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

    // Convenience overload: extracts the RoiDef from parameters first.
    // Returns null when no ROI is set (W=0 or H=0).
    public static Rect? Clamp(IReadOnlyDictionary<string, object?> parameters, int imageWidth, int imageHeight)
    {
        var roi = Extract(parameters);
        return roi is null ? null : Clamp(roi, imageWidth, imageHeight);
    }

    // Rotates the image so the ROI becomes axis-aligned. Rotation is around the ROI center.
    // Always returns a new Mat (caller owns it).
    public static Mat WarpForRoi(Mat image, RoiDef roi)
    {
        if (roi.Angle == 0.0) return image.Clone();
        var center = new Point2f((float)roi.CX, (float)roi.CY);
        using var rotMat = Cv2.GetRotationMatrix2D(center, -roi.Angle, 1.0);
        var warped = new Mat();
        Cv2.WarpAffine(image, warped, rotMat, image.Size(), InterpolationFlags.Linear, BorderTypes.Replicate);
        return warped;
    }

    // Builds the transform record needed for back-projection.
    public static RoiTransform BuildTransform(RoiDef roi, Rect clampedRect) =>
        new(roi.CX, roi.CY, roi.Angle * Math.PI / 180.0, clampedRect.X, clampedRect.Y);

    // Back-projects a crop-local position (cropX, cropY) to original-image coordinates.
    // Convention: WarpAffine was GetRotationMatrix2D(center, -angleDeg, 1.0).
    // Back-rotation uses +angle: outX = CX + dx*cos + dy*sin, outY = CY - dx*sin + dy*cos.
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

    public static Point2f BackProject(Point2f p, RoiTransform t)
        => BackProject(p.X, p.Y, t);

    public static CircleSegment BackProject(CircleSegment c, RoiTransform t)
    {
        var r = BackProject(c.Center.X, c.Center.Y, t);
        return new CircleSegment(r, c.Radius);
    }

    public static KeyPoint BackProject(KeyPoint k, RoiTransform t)
    {
        var r = BackProject(k.Pt.X, k.Pt.Y, t);
        return new KeyPoint(r.X, r.Y, k.Size, k.Angle, k.Response, k.Octave, k.ClassId);
    }

    public static OpenCvSharp.Point BackProject(OpenCvSharp.Point p, RoiTransform t)
    {
        var r = BackProject(p.X, p.Y, t);
        return new OpenCvSharp.Point((int)Math.Round(r.X), (int)Math.Round(r.Y));
    }

    public static OpenCvSharp.Point[] BackProject(OpenCvSharp.Point[] contour, RoiTransform t)
        => contour.Select(p => BackProject(p, t)).ToArray();

    // For filter operators: applies `process` to the (possibly rotated) ROI crop and composites
    // the result back over a clone of the full image.
    // • No ROI set           → calls process(fullImage) directly.
    // • ROI entirely outside → returns image.Clone() unchanged.
    // • Axis-aligned ROI     → SubMat composite (same as before).
    // • Rotated ROI          → WarpAffine → crop → filter → paste → WarpAffine back → mask composite.
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

    // Copies all five ROI values through to the output dictionary for downstream wiring.
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
