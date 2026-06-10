using IPLab.Core.Operators;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class FindStripeEdgesOperatorTests
{
    // General-purpose runner with full parameter control.
    private static Dictionary<string, object?> Run(Mat image,
        double cx, double cy, double w, double h, double angleDeg,
        double threshVal = 10.0, int maxEdges = 1) =>
        (Dictionary<string, object?>)new FindStripeEdgesOperator().Execute(
            new Dictionary<string, object?>
            {
                ["Image"]          = image,
                ["RoiCX"]          = cx,
                ["RoiCY"]          = cy,
                ["RoiW"]           = w,
                ["RoiH"]           = h,
                ["RoiAngle"]       = angleDeg,
                ["FilterSize"]     = 4,
                ["Threshold"]      = "Manual",
                ["ThresholdValue"] = threshVal,
                ["Polarity"]       = "Both",
                ["MaxEdges"]       = maxEdges,
            })!;

    // Build a 200×200 image with a step edge through (cx, cy) perpendicular to the
    // stripe axis.  Convention: positive angle = right-up → direction = (cos a, -sin a).
    // Pixels on the forward side of the axis → 255, behind → 0.
    private static Mat MakeStepEdgeImage(double cx, double cy, double angleDeg,
        int width = 200, int height = 200)
    {
        var rad  = angleDeg * Math.PI / 180.0;
        var cosA = Math.Cos(rad);
        var sinA = Math.Sin(rad);
        var mat  = new Mat(height, width, MatType.CV_8UC1);
        var idx  = mat.GetGenericIndexer<byte>();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                idx[y, x] = (byte)(((x - cx) * cosA - (y - cy) * sinA >= 0) ? 255 : 0);
        return mat;
    }

    // ── Back-projection tests ────────────────────────────────────────────────────

    [Theory]
    [InlineData(  0.0)]
    [InlineData( 30.0)]
    [InlineData( 45.0)]
    [InlineData(-15.0)]
    [InlineData( 60.0)]
    public void DetectsEdgeAtStripeCenter(double angleDeg)
    {
        const double cx = 100, cy = 100, stripeW = 100, stripeH = 20;

        using var image = MakeStepEdgeImage(cx, cy, angleDeg);
        var result = Run(image, cx, cy, stripeW, stripeH, angleDeg);

        var points = (Point2f[])result["Points"]!;
        var lines  = (LineSegmentPoint[])result["Lines"]!;

        Assert.Single(points);

        Assert.InRange(points[0].X, cx - 2, cx + 2);
        Assert.InRange(points[0].Y, cy - 2, cy + 2);

        var rad  = angleDeg * Math.PI / 180.0;
        var cosA = Math.Cos(rad);
        var sinA = Math.Sin(rad);
        foreach (var pt in new[] { lines[0].P1, lines[0].P2 })
        {
            double dx        = pt.X - cx;
            double dy        = pt.Y - cy;
            double alongAxis = dx * cosA - dy * sinA;
            double perpAxis  = dx * sinA + dy * cosA;

            Assert.InRange(alongAxis, -(stripeW / 2 + 2), stripeW / 2 + 2);
            Assert.InRange(perpAxis,  -(stripeH / 2 + 2), stripeH / 2 + 2);
        }
    }

    // ── Crop-clamping tests ──────────────────────────────────────────────────────
    // When the stripe starts off the left edge of the image (cropX < 0), the crop
    // width must be trimmed to match the intended right boundary (cropX + w), not
    // left at the full 'w'.  Otherwise the crop extends too far right and detects
    // edges outside the intended ROI.

    [Fact]
    public void CropClamp_DoesNotDetectEdgeBeyondIntendedRightBound()
    {
        // 300×20 grayscale image: dark for x<90, bright from x=90 onward.
        // Stripe: angle=0, center=(30,10), w=100, h=20.
        //   cropX = round(30 - 50) = -20  →  clampedX = 0
        //   intended right end: cropX + w = -20 + 100 = 80
        //   Bug:  clampedW = min(100, 300) = 100  → scans cols 0-99 → hits edge at x=90
        //   Fix:  clampedW = min(80,  300) - 0 = 80  → scans cols 0-79 → no edge found
        using var image = new Mat(20, 300, MatType.CV_8UC1, new Scalar(0));
        image.SubMat(new Rect(90, 0, 210, 20)).SetTo(new Scalar(255));

        var result = Run(image, cx: 30, cy: 10, w: 100, h: 20, angleDeg: 0);
        var points = (Point2f[])result["Points"]!;

        Assert.Empty(points); // edge at x=90 is outside the stripe [0..79]
    }

    [Fact]
    public void CropClamp_StillDetectsEdgeInsideIntendedBounds()
    {
        // Same geometry but the edge is at x=50, which is inside [0..79].
        using var image = new Mat(20, 300, MatType.CV_8UC1, new Scalar(0));
        image.SubMat(new Rect(50, 0, 250, 20)).SetTo(new Scalar(255));

        var result = Run(image, cx: 30, cy: 10, w: 100, h: 20, angleDeg: 0);
        var points = (Point2f[])result["Points"]!;

        Assert.Single(points);
        Assert.InRange(points[0].X, 48, 52);
    }

    // ── ROI partially outside image (top) ────────────────────────────────────────
    // When the stripe is centered at y=0 with height=100, the visible region is
    // y=0..50.  Line endpoints must stay within [0, 50], not extend to y=100.

    [Fact]
    public void LineEndpoints_ClampedToVisibleRegion_WhenRoiPartiallyOutsideTop()
    {
        // 200×200 image: left half dark, right half bright → vertical edge at x=100.
        // Stripe: angle=0, cx=100, cy=0, w=180, h=100.
        //   Unclamped crop:  y=-50 .. y=50   (height=100)
        //   Clamped crop:    y=0   .. y=50   (height=50)
        // Expected line: endpoints at y≈0 and y≈50, NOT y=0..100.
        using var image = new Mat(200, 200, MatType.CV_8UC1, new Scalar(0));
        image.SubMat(new Rect(100, 0, 100, 200)).SetTo(new Scalar(200));

        var result = Run(image, cx: 100, cy: 0, w: 180, h: 100, angleDeg: 0);
        var lines  = (LineSegmentPoint[])result["Lines"]!;

        Assert.Single(lines);

        int maxY = Math.Max(lines[0].P1.Y, lines[0].P2.Y);
        int minY = Math.Min(lines[0].P1.Y, lines[0].P2.Y);

        Assert.InRange(minY, 0, 5);          // top of line near y=0
        Assert.InRange(maxY, 45, 55);        // bottom of line near y=50, not y=100
    }
}
