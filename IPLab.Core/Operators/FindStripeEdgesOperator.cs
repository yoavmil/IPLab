using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using IPLab.Core.Spatial;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>One detected stripe edge with its center point, score, visual line, and polarity.</summary>
/// <param name="Point">The detected edge center in full-image coordinates.</param>
/// <param name="Score">The interpolated gradient response strength.</param>
/// <param name="Line">A line segment perpendicular to the stripe axis and centered on the edge.</param>
/// <param name="Polarity">The detected transition polarity: <c>DarkToBright</c> or <c>BrightToDark</c>.</param>
internal readonly record struct FindStripeEdge(Point2f Point, double Score, LineSegment2f Line, string Polarity);

/// <summary>Detects sub-pixel stripe edges within a rotated ROI by sampling a 1D stripe profile directly from the source image. Outputs edge positions, scores, line segments, and polarity labels.</summary>
public class FindStripeEdgesOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName => "FindStripeEdges";
    /// <inheritdoc/>
    public string Category => "Detection";
    /// <inheritdoc/>
    public string Icon => "stripe-edge";

    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",          Label = "Image",           ConnectableType = typeof(Mat) },
        ..RoiParameters.Schema,
        new() { Name = "FilterSize",     Label = "Filter Size",     Type = ParameterType.Int,    DefaultValue = 5, Min = 2.0 },
        new() { Name = "Threshold",      Label = "Threshold",       Type = ParameterType.Enum,   DefaultValue = "Manual", Options = ["Manual", "Auto"] },
        new() { Name = "ThresholdValue", Label = "Threshold Value", Type = ParameterType.Double, DefaultValue = 10.0, Min = 0.0,
                ShowWhenParam = "Threshold", ShowWhenValues = ["Manual"] },
        new() { Name = "Polarity",       Label = "Polarity",        Type = ParameterType.Enum,   DefaultValue = "Both", Options = ["Both", "DarkToBright", "BrightToDark"] },
        new() { Name = "MaxEdges",       Label = "Max Edges",       Type = ParameterType.Int,    DefaultValue = 1, Min = 1.0 },
    ];

    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "Points",   DataType = typeof(Point2f[]) },
        new() { Name = "Score",    DataType = typeof(double[]) },
        new() { Name = "Lines",    DataType = typeof(LineSegment2f[]) },
        new() { Name = "Polarity", DataType = typeof(string[]) },
        ..RoiParameters.OutputPorts,
    ];

    /// <inheritdoc/>
    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image      = (Mat)parameters["Image"]!;
        var filterSize = Convert.ToInt32(parameters.GetValueOrDefault("FilterSize")      ?? 5);
        var threshMode = (string?)parameters.GetValueOrDefault("Threshold")              ?? "Manual";
        var threshVal  = Convert.ToDouble(parameters.GetValueOrDefault("ThresholdValue") ?? 10.0);
        var polarity   = (string?)parameters.GetValueOrDefault("Polarity")               ?? "Both";
        var maxEdges   = Convert.ToInt32(parameters.GetValueOrDefault("MaxEdges")        ?? 1);

        var roi = RoiParameters.Extract(parameters);
        var edges = roi is null
            ? []
            : FindEdges(image, roi, filterSize, threshMode, threshVal, polarity, maxEdges);

        var outputs = new Dictionary<string, object?>
        {
            ["Points"] = edges.Select(e => e.Point).ToArray(),
            ["Score"] = edges.Select(e => e.Score).ToArray(),
            ["Lines"] = edges.Select(e => e.Line).ToArray(),
            ["Polarity"] = edges.Select(e => e.Polarity).ToArray(),
        };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }

    /// <summary>Finds stripe edges directly from the source image without creating or executing an operator instance.</summary>
    internal static IReadOnlyList<FindStripeEdge> FindEdges(
        Mat image,
        RoiDef roi,
        int filterSize = 5,
        string threshold = "Manual",
        double thresholdValue = 10.0,
        string polarity = "Both",
        int maxEdges = 1,
        string edgeSelect = "Strongest")
    {
        if (image.Channels() != 1)
            throw new InvalidOperationException("FindStripeEdges requires a single-channel (grayscale) image.");

        if (roi.Width <= 0 || roi.Height <= 0 || maxEdges <= 0)
            return Array.Empty<FindStripeEdge>();

        int w = (int)Math.Round(roi.Width);
        int h = (int)Math.Round(roi.Height);
        if (w <= 0 || h <= 0)
            return Array.Empty<FindStripeEdge>();

        filterSize = Math.Clamp(filterSize, 2, Math.Max(2, w / 2));
        if ((filterSize & 1) == 1)
            filterSize--;
        filterSize = Math.Max(2, filterSize);
        int half = filterSize / 2;

        double theta = roi.Angle * Math.PI / 180.0;
        double ux = Math.Cos(theta);
        double uy = -Math.Sin(theta);
        double vx = Math.Sin(theta);
        double vy =  Math.Cos(theta);

        if (!TryBuildProfile(image, roi, w, h, ux, uy, vx, vy, out var profile))
            return Array.Empty<FindStripeEdge>();

        using (profile)
        {
            using var kmat = new Mat(1, filterSize, MatType.CV_64F);
            var kidx = kmat.GetGenericIndexer<double>();
            for (int j = 0; j < half; j++) kidx[0, j] = -1.0 / half;
            for (int j = half; j < filterSize; j++) kidx[0, j] = 1.0 / half;

            using var gradMat = new Mat();
            Cv2.Filter2D(profile, gradMat, MatType.CV_64F, kmat,
                         anchor: new Point(half, 0), borderType: BorderTypes.Constant);

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

            double thresholdValueToUse;
            if (threshold == "Auto")
            {
                using var resp8u = new Mat();
                responseMat.ConvertTo(resp8u, MatType.CV_8U);
                using var dummy = new Mat();
                thresholdValueToUse = Cv2.Threshold(resp8u, dummy, 0, 255, ThresholdTypes.Otsu | ThresholdTypes.Binary);
            }
            else
                thresholdValueToUse = thresholdValue;

            var peaks = new List<(double col, double strength, int gradIndex)>();
            for (int i = half + 1; i < w - half - 1; i++)
            {
                if (!(response[i] > response[i - 1] && response[i] >= response[i + 1])) continue;

                double y0 = response[i - 1], y1 = response[i], y2 = response[i + 1];
                double denom    = y0 - 2.0 * y1 + y2;
                bool   curved   = Math.Abs(denom) > 1e-6;
                double dx       = curved ? (y0 - y2) / (2.0 * denom) : 0.0;
                double strength = curved ? y1 - (y2 - y0) * (y2 - y0) / (8.0 * denom) : y1;

                if (strength <= thresholdValueToUse) continue;

                double edgeCol = i - 0.5 + Math.Clamp(dx, -0.5, 0.5);
                peaks.Add((edgeCol, strength, i));
            }

            Comparison<(double col, double strength, int gradIndex)> sorter = edgeSelect switch
            {
                "First" => (a, b) => a.col.CompareTo(b.col),
                "Last"  => (a, b) => b.col.CompareTo(a.col),
                _       => (a, b) => b.strength.CompareTo(a.strength),
            };
            peaks.Sort(sorter);
            if (peaks.Count > maxEdges)
                peaks.RemoveRange(maxEdges, peaks.Count - maxEdges);

            double halfW = (w - 1) / 2.0;
            double halfH = (h - 1) / 2.0;

            var edges = new FindStripeEdge[peaks.Count];

            for (int idx = 0; idx < peaks.Count; idx++)
            {
                var (col, strength, gradIndex) = peaks[idx];

                double s = col - halfW;
                double imgX = roi.CX + s * ux;
                double imgY = roi.CY + s * uy;

                var point = new Point2f((float)imgX, (float)imgY);
                var line = new LineSegment2f(
                    new Point2f((float)(imgX - halfH * vx), (float)(imgY - halfH * vy)),
                    new Point2f((float)(imgX + halfH * vx), (float)(imgY + halfH * vy)));
                var detectedPolarity = gradMat.At<double>(0, gradIndex) > 0 ? "DarkToBright" : "BrightToDark";
                edges[idx] = new FindStripeEdge(point, strength, line, detectedPolarity);
            }

            return edges;
        }
    }

    private static bool TryBuildProfile(
        Mat image,
        RoiDef roi,
        int w,
        int h,
        double ux,
        double uy,
        double vx,
        double vy,
        out Mat profile)
    {
        profile = new Mat(1, w, MatType.CV_64F, Scalar.All(0));
        var pidx = profile.GetGenericIndexer<double>();
        var valid = new bool[w];
        var sampleAt = CreateSampler(image);

        double halfW = (w - 1) / 2.0;
        double halfH = (h - 1) / 2.0;

        for (int i = 0; i < w; i++)
        {
            double s = i - halfW;
            double sum = 0.0;
            int count = 0;

            for (int j = 0; j < h; j++)
            {
                double t = j - halfH;
                double x = roi.CX + s * ux + t * vx;
                double y = roi.CY + s * uy + t * vy;
                if (!TrySampleBilinear(image, x, y, sampleAt, out double value))
                    continue;

                sum += value;
                count++;
            }

            if (count > 0)
            {
                pidx[0, i] = sum / count;
                valid[i] = true;
            }
        }

        int firstValid = Array.FindIndex(valid, v => v);
        if (firstValid < 0)
        {
            profile.Dispose();
            profile = null!;
            return false;
        }

        for (int i = 0; i < firstValid; i++)
            pidx[0, i] = pidx[0, firstValid];

        int lastValid = firstValid;
        for (int i = firstValid + 1; i < w; i++)
        {
            if (!valid[i])
                continue;

            if (i - lastValid > 1)
            {
                double left = pidx[0, lastValid];
                double right = pidx[0, i];
                for (int k = lastValid + 1; k < i; k++)
                {
                    double alpha = (double)(k - lastValid) / (i - lastValid);
                    pidx[0, k] = left + (right - left) * alpha;
                }
            }
            lastValid = i;
        }

        for (int i = lastValid + 1; i < w; i++)
            pidx[0, i] = pidx[0, lastValid];

        return true;
    }

    private static bool TrySampleBilinear(
        Mat image,
        double x,
        double y,
        Func<int, int, double> sampleAt,
        out double value)
    {
        value = 0.0;
        if (x < 0 || y < 0 || x > image.Width - 1 || y > image.Height - 1)
            return false;

        int x0 = (int)Math.Floor(x);
        int y0 = (int)Math.Floor(y);
        int x1 = Math.Min(x0 + 1, image.Width - 1);
        int y1 = Math.Min(y0 + 1, image.Height - 1);
        double ax = x - x0;
        double ay = y - y0;

        double v00 = sampleAt(x0, y0);
        double v10 = sampleAt(x1, y0);
        double v01 = sampleAt(x0, y1);
        double v11 = sampleAt(x1, y1);

        double top = v00 + (v10 - v00) * ax;
        double bottom = v01 + (v11 - v01) * ax;
        value = top + (bottom - top) * ay;
        return true;
    }

    private static Func<int, int, double> CreateSampler(Mat image)
    {
        var type = image.Type();
        if (type == MatType.CV_8UC1)
        {
            var idx = image.GetGenericIndexer<byte>();
            return (x, y) => idx[y, x];
        }
        if (type == MatType.CV_16UC1)
        {
            var idx = image.GetGenericIndexer<ushort>();
            return (x, y) => idx[y, x];
        }
        if (type == MatType.CV_32FC1)
        {
            var idx = image.GetGenericIndexer<float>();
            return (x, y) => idx[y, x];
        }
        if (type == MatType.CV_64FC1)
        {
            var idx = image.GetGenericIndexer<double>();
            return (x, y) => idx[y, x];
        }

        throw new NotSupportedException($"FindStripeEdges does not support image type {type}.");
    }
}
