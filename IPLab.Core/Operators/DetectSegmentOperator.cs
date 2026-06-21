using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using IPLab.Core.Spatial;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>
/// Detects a sub-pixel line edge within a rotated ROI.
/// The ROI is split into <c>StripeCount</c> stripes along the height axis; each stripe is
/// processed by <see cref="FindStripeEdgesOperator.FindEdges"/> to locate one edge candidate.
/// </summary>
public class DetectSegmentOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName => "DetectSegment";
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
        new() { Name = "Line",     DataType = typeof(LineSegment2f) },
        new() { Name = "Points",   DataType = typeof(Point2f[]) },
        new() { Name = "Score",    DataType = typeof(double[]) },
        new() { Name = "Found",    DataType = typeof(bool) },
        new() { Name = "Contours", DataType = typeof(Point2f[][]) },
        ..RoiParameters.OutputPorts,
    ];

    bool debug = false;

    /// <inheritdoc/>
    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
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
            throw new InvalidOperationException("DetectSegment requires a single-channel (grayscale) image.");

        Dictionary<string, object?> MakeEmpty()
        {
            var d = new Dictionary<string, object?>
            {
                ["Line"]     = default(LineSegment2f),
                ["Points"]   = Array.Empty<Point2f>(),
                ["Score"]    = Array.Empty<double>(),
                ["Found"]    = false,
                ["Contours"] = null,
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

        // ── Segment from inlier span ──────────────────────────────────────────────

        LineSegment2f segment = default;
        Point2f[][]? searchContour = null;

        if (found)
        {
            // Line fit on all inliers: scan pos s = lineA*t + lineB
            double lineA = 0, lineB = 0;
            double tMin = inliers.Min(h => h.t);
            double tMax = inliers.Max(h => h.t);
            {
                int n = inliers.Count;
                double sumT = 0, sumS = 0, sumTT = 0, sumTS = 0;
                foreach (var (_, _, s, t) in inliers) { sumT += t; sumS += s; sumTT += t * t; sumTS += t * s; }
                double den2 = n * sumTT - sumT * sumT;
                lineA = Math.Abs(den2) > 1e-10 ? (n * sumTS - sumT * sumS) / den2 : 0.0;
                lineB = (sumS - lineA * sumT) / n;
            }

            // Endpoints at the extreme inlier t values, projected onto the fitted line.
            Point2f LinePoint(double t) => new(
                (float)((lineA * t + lineB) * ux + t * vx),
                (float)((lineA * t + lineB) * uy + t * vy));

            segment = new LineSegment2f(LinePoint(tMin), LinePoint(tMax));

            // Fitted line direction (normalized) and its perpendicular.
            double lineNorm = Math.Sqrt(lineA * lineA + 1.0);
            double lux = (lineA * ux + vx) / lineNorm;
            double luy = (lineA * uy + vy) / lineNorm;
            // perpendicular: (-luy, lux)

            double f = filterSize;
            var p1 = segment.P1;
            var p2 = segment.P2;

            // Split the full extent ROI into two halves on opposite perpendicular sides of the segment.
            double segLen   = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
            double extAngle = Math.Atan2(-luy, lux) * 180.0 / Math.PI;
            double roiW     = segLen + 2.0 * f;
            double cx = (p1.X + p2.X) / 2.0;
            double cy = (p1.Y + p2.Y) / 2.0;
            // Perpendicular to fitted line: (-luy, lux)
            var roi1 = new RoiDef(cx - luy * f / 2.0, cy + lux * f / 2.0, roiW, f, extAngle);
            var roi2 = new RoiDef(cx + luy * f / 2.0, cy - lux * f / 2.0, roiW, f, extAngle);

            // Debug: show the corners of both extent half-ROIs.
            if (debug)
            {
                Point2f[] RoiCorners(double rcx, double rcy, double w, double h)
                {
                    double hw = w / 2.0, hh = h / 2.0;
                    return [
                        new((float)(rcx + lux*hw - luy*hh), (float)(rcy + luy*hw + lux*hh)),
                        new((float)(rcx + lux*hw + luy*hh), (float)(rcy + luy*hw - lux*hh)),
                        new((float)(rcx - lux*hw + luy*hh), (float)(rcy - luy*hw - lux*hh)),
                        new((float)(rcx - lux*hw - luy*hh), (float)(rcy - luy*hw + lux*hh)),
                    ];
                }
                searchContour = [
                    RoiCorners(roi1.CX, roi1.CY, roiW, f),
                    RoiCorners(roi2.CX, roi2.CY, roiW, f),
                ];
            }

            var edges1 = FindStripeEdgesOperator.FindEdges(image, roi1, filterSize, threshMode, threshVal, "Both", maxEdges: 2);
            var edges2 = FindStripeEdgesOperator.FindEdges(image, roi2, filterSize, threshMode, threshVal, "Both", maxEdges: 2);

            // Pick the 2 most prominent from both sides, project onto the fitted line.
            var top2 = edges1.Concat(edges2)
                .OrderByDescending(e => e.Score)
                .Take(2)
                .OrderBy(e => e.Point.X * vx + e.Point.Y * vy)
                .ToArray();

            if (top2.Length == 2)
            {
                segment = new LineSegment2f(
                    LinePoint(top2[0].Point.X * vx + top2[0].Point.Y * vy),
                    LinePoint(top2[1].Point.X * vx + top2[1].Point.Y * vy));
            }
        }

        var outputs = new Dictionary<string, object?>
        {
            ["Line"]     = segment,
            ["Points"]   = debug?inliers.Select(h => h.point).ToArray() : [],
            ["Score"]    = inliers.Select(h => h.score).ToArray(),
            ["Found"]    = found,
            ["Contours"] = searchContour,
        };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }
}
