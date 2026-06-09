using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;
using System.IO;

namespace IPLab.Core.Operators;

public class FindStripeEdgesOperator : IOperatorType
{
    public string TypeName => "FindStripeEdges";
    public string Category => "Detection";
    public string Icon => "stripe-edge";

    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",          Label = "Image",           ConnectableType = typeof(Mat) },
        new() { Name = "RoiCX",          Label = "Center X",        Type = ParameterType.Double, DefaultValue = 0.0 },
        new() { Name = "RoiCY",          Label = "Center Y",        Type = ParameterType.Double, DefaultValue = 0.0 },
        new() { Name = "RoiW",           Label = "Length",          Type = ParameterType.Double, DefaultValue = 0.0, Min = 0.0 },
        new() { Name = "RoiH",           Label = "Width",           Type = ParameterType.Double, DefaultValue = 1.0, Min = 1.0 },
        new() { Name = "RoiAngle",       Label = "Angle (°)",       Type = ParameterType.Double, DefaultValue = 0.0 },
        new() { Name = "FilterSize",     Label = "Filter Size",     Type = ParameterType.Int,    DefaultValue = 5, Min = 2.0 },
        new() { Name = "Threshold",      Label = "Threshold",       Type = ParameterType.Enum,   DefaultValue = "Manual", Options = ["Manual", "Auto"] },
        new() { Name = "ThresholdValue", Label = "Threshold Value", Type = ParameterType.Double, DefaultValue = 10.0, Min = 0.0,
                ShowWhenParam = "Threshold", ShowWhenValues = ["Manual"] },
        new() { Name = "Polarity",       Label = "Polarity",        Type = ParameterType.Enum,   DefaultValue = "Both", Options = ["Both", "DarkToBright", "BrightToDark"] },
        new() { Name = "MaxEdges",       Label = "Max Edges",       Type = ParameterType.Int,    DefaultValue = 1, Min = 1.0 },
    ];

    public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "Points",   DataType = typeof(Point2f[]) },
        new() { Name = "Score",    DataType = typeof(double[]) },
        new() { Name = "Lines",    DataType = typeof(LineSegmentPoint[]) },
        new() { Name = "Polarity", DataType = typeof(string[]) },
    ];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image      = (Mat)parameters["Image"]!;
        var filterSize = Convert.ToInt32(parameters.GetValueOrDefault("FilterSize")     ?? 5);
        var threshMode = (string?)parameters.GetValueOrDefault("Threshold")             ?? "Manual";
        var threshVal  = Convert.ToDouble(parameters.GetValueOrDefault("ThresholdValue") ?? 10.0);
        var polarity   = (string?)parameters.GetValueOrDefault("Polarity")              ?? "Both";
        var maxEdges   = Convert.ToInt32(parameters.GetValueOrDefault("MaxEdges")       ?? 1);

        if (image.Channels() != 1)
            throw new InvalidOperationException("FindStripeEdges requires a single-channel (grayscale) image.");

        static Dictionary<string, object?> Empty() => new()
        {
            ["Points"] = Array.Empty<Point2f>(),
            ["Score"] = Array.Empty<double>(),
            ["Lines"] = Array.Empty<LineSegmentPoint>(),
            ["Polarity"] = Array.Empty<string>(),
        };

        var roi = RoiParameters.Extract(parameters);
        if (roi is null || roi.Width <= 0 || roi.Height <= 0)
            return Empty();

        int w = (int)Math.Round(roi.Width);
        int h = (int)Math.Round(roi.Height);

        filterSize = Math.Clamp(filterSize, 2, Math.Max(2, w / 2));
        int half = filterSize / 2;

        var angleRad = roi.Angle * Math.PI / 180.0;
        double cosA  = Math.Cos(angleRad);
        double sinA  = Math.Sin(angleRad);

        // Convention: positive Angle = right-up (CCW). To align (cosθ, -sinθ) → (1,0):
        // WarpAffine rotation angle must be -θ.
        using var rotated = RoiParameters.WarpForRoi(image, roi);

        var rect = RoiParameters.Clamp(roi, rotated.Width, rotated.Height);
        if (rect.Width <= 0 || rect.Height <= 0)
            return Empty();

        var transform  = RoiParameters.BuildTransform(roi, rect);
        int clampedW   = rect.Width;

        using var stripe = new Mat(rotated, rect);

        bool debug = false;
        if (debug)
        {
            // DEBUG: dump intermediate images + matrix to C:\temp\stripe_debug\
            var dbgDir = @"C:\temp\stripe_debug";
            Directory.CreateDirectory(dbgDir);

            File.WriteAllText(Path.Combine(dbgDir, "matrix.txt"),
                $"angleDeg={roi.Angle}  cx={roi.CX}  cy={roi.CY}  w={w}  h={h}\n" +
                $"rect=({rect.X},{rect.Y},{rect.Width},{rect.Height})\n");

            using var dbg1 = new Mat();
            Cv2.CvtColor(image, dbg1, ColorConversionCodes.GRAY2BGR);
            var rr = new RotatedRect(new Point2f((float)roi.CX, (float)roi.CY),
                                     new Size2f(w, h), (float)-roi.Angle);
            var corners = rr.Points();
            for (int i = 0; i < 4; i++)
                Cv2.Line(dbg1,
                    new Point((int)corners[i].X, (int)corners[i].Y),
                    new Point((int)corners[(i + 1) % 4].X, (int)corners[(i + 1) % 4].Y),
                    Scalar.Green, 2);
            Cv2.Circle(dbg1, new Point((int)roi.CX, (int)roi.CY), 5, Scalar.Red, -1);
            Cv2.ImWrite(Path.Combine(dbgDir, "01_original.png"), dbg1);

            using var dbg2 = new Mat();
            Cv2.CvtColor(rotated, dbg2, ColorConversionCodes.GRAY2BGR);
            Cv2.Rectangle(dbg2, rect, Scalar.Yellow, 2);
            Cv2.Circle(dbg2, new Point((int)roi.CX, (int)roi.CY), 5, Scalar.Red, -1);
            Cv2.ImWrite(Path.Combine(dbgDir, "02_rotated.png"), dbg2);

            Cv2.ImWrite(Path.Combine(dbgDir, "03_stripe.png"), stripe);
        }

        // 1D projection: column averages → 1×clampedW CV_64F row mat
        using var profile = new Mat();
        Cv2.Reduce(stripe, profile, ReduceDimension.Row, ReduceTypes.Avg, MatType.CV_64F);

        // Box-difference derivative via Filter2D.
        // Kernel [-1/half … -1/half, +1/half … +1/half] with anchor at col=half
        using var kmat = new Mat(1, filterSize, MatType.CV_64F);
        var kidx = kmat.GetGenericIndexer<double>();
        for (int j = 0;    j < half;       j++) kidx[0, j] = -1.0 / half;
        for (int j = half; j < filterSize; j++) kidx[0, j] =  1.0 / half;
        using var gradMat = new Mat();
        Cv2.Filter2D(profile, gradMat, MatType.CV_64F, kmat,
                     anchor: new Point(half, 0), borderType: BorderTypes.Constant);

        // Apply polarity filter via OpenCV element-wise ops:
        //   Tozero at 0 → max(0, x); negate + Tozero → max(0, -x); Abs → |x|
        using var responseMat = new Mat();
        switch (polarity)
        {
            case "DarkToBright":
                Cv2.Threshold(gradMat, responseMat, 0, 0, ThresholdTypes.Tozero);
                break;
            case "BrightToDark":
                Cv2.Threshold(-gradMat, responseMat, 0, 0, ThresholdTypes.Tozero);
                break;
            default:
                using (var absMat = Cv2.Abs(gradMat).ToMat())
                    absMat.CopyTo(responseMat);
                break;
        }
        responseMat.GetArray(out double[] response);

        double threshold;
        if (threshMode == "Auto")
        {
            // Otsu requires CV_8U. Response values are gradient magnitudes in [0, 255]
            // (averages of 8-bit pixels), so ConvertTo is a direct cast with no scaling.
            using var resp8u = new Mat();
            responseMat.ConvertTo(resp8u, MatType.CV_8U);
            using var dummy = new Mat();
            threshold = Cv2.Threshold(resp8u, dummy, 0, 255, ThresholdTypes.Otsu | ThresholdTypes.Binary);
        }
        else
            threshold = threshVal;

        // Find local maxima above threshold.
        // >= on the right side catches flat-topped peaks without double-detecting them.
        // Parabolic interpolation on the three-point neighbourhood gives subpixel position and
        // strength. The vertex is always above the sampled peak (vertex = y1 - b²/4a, a<0),
        // so the threshold must be checked against the interpolated strength, not response[i].
        // Near-zero curvature (flat plateau) falls back to integer column and sampled strength.
        var peaks = new List<(double col, double strength)>();
        for (int i = half + 1; i < clampedW - half - 1; i++)
        {
            if (!(response[i] > response[i - 1] && response[i] >= response[i + 1])) continue;

            // Fit a parabola through (i-1,y0),(i,y1),(i+1,y2) in local coords x∈{-1,0,1}.
            // a=(y0+y2)/2-y1, b=(y2-y0)/2 → vertex x*=-b/2a, value=y1-b²/4a.
            double y0 = response[i - 1], y1 = response[i], y2 = response[i + 1];
            double denom    = y0 - 2.0 * y1 + y2;
            bool   curved   = Math.Abs(denom) > 1e-6;
            double dx       = curved ? (y0 - y2) / (2.0 * denom) : 0.0;
            double strength = curved ? y1 - (y2 - y0) * (y2 - y0) / (8.0 * denom) : y1;

            if (strength <= threshold) continue;
            peaks.Add((i + Math.Clamp(dx, -0.5, 0.5), strength));
        }

        peaks.Sort((a, b) => b.strength.CompareTo(a.strength));
        if (peaks.Count > maxEdges)
            peaks.RemoveRange(maxEdges, peaks.Count - maxEdges);

        double perpHalf = roi.Height / 2.0;

        var points     = new Point2f[peaks.Count];
        var scores     = new double[peaks.Count];
        var lines      = new LineSegmentPoint[peaks.Count];
        var polarities = new string[peaks.Count];

        for (int idx = 0; idx < peaks.Count; idx++)
        {
            var (col, strength) = peaks[idx];

            // Back-project the crop-column position to full-image coordinates.
            // RoiParameters.BackProject convention: outX = CX + dx*cos + dy*sin,
            //                                       outY = CY - dx*sin + dy*cos
            // (see RoiParameters.cs for derivation).
            var center2 = RoiParameters.BackProject(col, h / 2.0, transform);
            double imgX = center2.X;
            double imgY = center2.Y;

            // Perpendicular direction to stripe axis (cosA, -sinA) is (sinA, cosA).
            double pdx = sinA * perpHalf;
            double pdy = cosA * perpHalf;

            points[idx] = new Point2f((float)imgX, (float)imgY);
            scores[idx] = strength;
            lines[idx] = new LineSegmentPoint(
                new Point((int)Math.Round(imgX - pdx), (int)Math.Round(imgY - pdy)),
                new Point((int)Math.Round(imgX + pdx), (int)Math.Round(imgY + pdy)));
            polarities[idx] = gradMat.At<double>(0, (int)Math.Round(col)) > 0 ? "DarkToBright" : "BrightToDark";
        }

        return new Dictionary<string, object?>
        {
            ["Points"] = points,
            ["Score"] = scores,
            ["Lines"] = lines,
            ["Polarity"] = polarities,
        };
    }
}
