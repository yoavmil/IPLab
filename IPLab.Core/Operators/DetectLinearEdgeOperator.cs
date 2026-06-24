using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using IPLab.Core.Spatial;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>
/// Detects a sub-pixel straight line edge within a rotated ROI.
/// The ROI is split into <c>StripeCount</c> stripes along the height axis; each stripe is
/// processed by <see cref="FindStripeEdgesOperator.FindEdges"/> to locate one edge candidate.
/// Outlier rejection fits a line through the candidates and removes stripes whose residuals
/// exceed 3 pixels. The <c>Line</c> output spans the full ROI height along the fitted line.
/// </summary>
/// <seealso href="https://github.com/yoavmil/IPLab/blob/master/docs/OPERATORS.md#detectlinearedge">Operator reference</seealso>
public class DetectLinearEdgeOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName => "DetectLinearEdge";
    /// <inheritdoc/>
    public string Category => "Detection";
    /// <inheritdoc/>
    public string Icon => "edge-line";

    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",          Label = "Image",           ConnectableType = typeof(Mat) },
        ..RoiParameters.Schema,
        new() { Name = "StripeCount",    Label = "Stripe Count",    Type = ParameterType.Int,    DefaultValue = 5,              Min = 2.0 },
        new() { Name = "StripeWidth",    Label = "Stripe Width",    Type = ParameterType.Int,    DefaultValue = 10,             Min = 1.0 },
        new() { Name = "FilterSize",     Label = "Filter Size",     Type = ParameterType.Int,    DefaultValue = 5,              Min = 2.0 },
        new() { Name = "Threshold",      Label = "Threshold",       Type = ParameterType.Enum,   DefaultValue = "Manual",       Options = ["Manual", "Auto"] },
        new() { Name = "ThresholdValue", Label = "Threshold Value", Type = ParameterType.Double, DefaultValue = 10.0,           Min = 0.0,
                ShowWhenParam = "Threshold", ShowWhenValues = ["Manual"] },
        new() { Name = "Polarity",       Label = "Polarity",        Type = ParameterType.Enum,   DefaultValue = "DarkToBright", Options = ["DarkToBright", "BrightToDark"] },
        new() { Name = "EdgeSelect",     Label = "Edge Select",     Type = ParameterType.Enum,   DefaultValue = "Strongest",    Options = ["Strongest", "First", "Last"] },
        new() { Name = "MinScore",       Label = "Min Score",       Type = ParameterType.Double, DefaultValue = 0.5,            Min = 0.0, Max = 1.0 },
    ];

    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "Line",   DataType = typeof(LineSegment2f) },
        new() { Name = "Points", DataType = typeof(Point2f[]) },
        new() { Name = "Score",  DataType = typeof(double[]) },
        new() { Name = "Found",  DataType = typeof(bool) },
        ..RoiParameters.OutputPorts,
    ];

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image       = (Mat)parameters["Image"]!;
        int stripeCount = Math.Max(2, Convert.ToInt32(parameters.GetValueOrDefault("StripeCount")    ?? 5));
        int stripeWidth = Math.Max(1, Convert.ToInt32(parameters.GetValueOrDefault("StripeWidth")    ?? 10));
        int filterSize  = Math.Max(2, Convert.ToInt32(parameters.GetValueOrDefault("FilterSize")     ?? 5));
        var threshMode  = (string?)parameters.GetValueOrDefault("Threshold")                         ?? "Manual";
        var threshVal   = Convert.ToDouble(parameters.GetValueOrDefault("ThresholdValue")            ?? 10.0);
        var polarity    = (string?)parameters.GetValueOrDefault("Polarity")                          ?? "DarkToBright";
        var edgeSelect  = (string?)parameters.GetValueOrDefault("EdgeSelect")                        ?? "Strongest";

        if (image.Channels() != 1)
            throw new InvalidOperationException("DetectLinearEdge requires a single-channel (grayscale) image.");

        Dictionary<string, object?> MakeEmpty()
        {
            var d = new Dictionary<string, object?>
            {
                ["Line"]   = default(LineSegment2f),
                ["Points"] = Array.Empty<Point2f>(),
                ["Score"]  = Array.Empty<double>(),
                ["Found"]  = false,
            };
            RoiParameters.AddToOutputs(d, parameters);
            return d;
        }

        var roi = RoiParameters.Extract(parameters);
        if (roi is null || roi.Width <= 0 || roi.Height <= 0)
            return MakeEmpty();

        var minScore = Convert.ToDouble(parameters.GetValueOrDefault("MinScore") ?? 0.5);

        // Scan direction (W) and perpendicular height direction (H) in image space.
        double theta = roi.Angle * Math.PI / 180.0;
        double ux = Math.Cos(theta), uy = -Math.Sin(theta); // along W (scan)
        double vx = Math.Sin(theta), vy =  Math.Cos(theta); // along H (stripes)

        // ── Per-stripe edge detection ─────────────────────────────────────────────

        var hits = new List<(Point2f point, double score, double s, double t)>();

        for (int k = 0; k < stripeCount; k++)
        {
            // Offset of this stripe's center from the ROI center along the height axis.
            double offset = ((k + 0.5) / stripeCount - 0.5) * roi.Height;

            var stripeRoi = new RoiDef(
                roi.CX + offset * vx,
                roi.CY + offset * vy,
                roi.Width, stripeWidth,
                roi.Angle);

            var edges = FindStripeEdgesOperator.FindEdges(
                image, stripeRoi, filterSize, threshMode, threshVal, polarity,
                maxEdges: 1, edgeSelect: edgeSelect);

            if (edges.Count > 0)
            {
                var p = edges[0].Point;
                hits.Add((p, edges[0].Score, p.X * ux + p.Y * uy, p.X * vx + p.Y * vy));
            }
        }

        // ── Iterative outlier rejection ───────────────────────────────────────────
        // Fit s = a*t + b (scan pos as function of height pos) and drop worst outlier.

        const double OutlierTolerance = 3.0;
        var inliers = hits.ToList();
        while (inliers.Count >= 2)
        {
            int n = inliers.Count;
            double sumT = 0, sumS = 0, sumTT = 0, sumTS = 0;
            foreach (var h in inliers) { sumT += h.t; sumS += h.s; sumTT += h.t * h.t; sumTS += h.t * h.s; }
            double den = n * sumTT - sumT * sumT;
            double a   = Math.Abs(den) > 1e-10 ? (n * sumTS - sumT * sumS) / den : 0.0;
            double b   = (sumS - a * sumT) / n;

            int    worstIdx = -1;
            double worstRes = OutlierTolerance;
            for (int i = 0; i < inliers.Count; i++)
            {
                double res = Math.Abs(inliers[i].s - (a * inliers[i].t + b));
                if (res > worstRes) { worstRes = res; worstIdx = i; }
            }
            if (worstIdx < 0) break;
            inliers.RemoveAt(worstIdx);
        }

        bool found = inliers.Count >= 2 && (double)inliers.Count / stripeCount >= minScore;

        // ── Segment spanning full ROI height ──────────────────────────────────────

        LineSegment2f segment = default;

        if (found)
        {
            // Line fit on all inliers: scan pos s = lineA*t + lineB
            double lineA = 0, lineB = 0;
            {
                int n = inliers.Count;
                double sumT = 0, sumS = 0, sumTT = 0, sumTS = 0;
                foreach (var (_, _, s, t) in inliers) { sumT += t; sumS += s; sumTT += t * t; sumTS += t * s; }
                double den2 = n * sumTT - sumT * sumT;
                lineA = Math.Abs(den2) > 1e-10 ? (n * sumTS - sumT * sumS) / den2 : 0.0;
                lineB = (sumS - lineA * sumT) / n;
            }

            // Endpoints clamped to the ROI rectangle in (s, t) space.
            // t is the coordinate along the height axis; s = lineA*t + lineB along the scan axis.
            // Clip the t-extent so that s also stays within [sCenter ± W/2].
            Point2f LinePoint(double t) => new(
                (float)((lineA * t + lineB) * ux + t * vx),
                (float)((lineA * t + lineB) * uy + t * vy));

            double tCenter = roi.CX * vx + roi.CY * vy;
            double sCenter = roi.CX * ux + roi.CY * uy;
            double tLo = tCenter - roi.Height / 2.0;
            double tHi = tCenter + roi.Height / 2.0;

            if (Math.Abs(lineA) > 1e-10)
            {
                double tAtSlo = (sCenter - roi.Width / 2.0 - lineB) / lineA;
                double tAtShi = (sCenter + roi.Width / 2.0 - lineB) / lineA;
                if (tAtSlo > tAtShi) { double tmp = tAtSlo; tAtSlo = tAtShi; tAtShi = tmp; }
                tLo = Math.Max(tLo, tAtSlo);
                tHi = Math.Min(tHi, tAtShi);
            }

            segment = new LineSegment2f(LinePoint(tLo), LinePoint(tHi));
        }

        // Sort inliers by t so Points[0]..Points[N-1] run from one end of the ROI to the other.
        inliers.Sort((a, b) => a.t.CompareTo(b.t));

        var outputs = new Dictionary<string, object?>
        {
            ["Line"]   = segment,
            ["Points"] = inliers.Select(h => h.point).ToArray(),
            ["Score"]  = inliers.Select(h => h.score).ToArray(),
            ["Found"]  = found,
        };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }
}
