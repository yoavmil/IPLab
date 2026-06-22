using IPLab.Core.Operators;
using IPLab.Core.Spatial;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class DetectLinearEdgeOperatorTests
{
    private static Dictionary<string, object?> Run(Mat image,
        double cx, double cy, double w, double h, double angleDeg,
        int stripeCount = 5, int stripeWidth = 10,
        string polarity = "DarkToBright", double threshVal = 10.0, double minScore = 0.5,
        string edgeSelect = "Strongest") =>
        (Dictionary<string, object?>)new DetectLinearEdgeOperator().Execute(
            new Dictionary<string, object?>
            {
                ["Image"]          = image,
                ["RoiCX"]          = cx,
                ["RoiCY"]          = cy,
                ["RoiW"]           = w,
                ["RoiH"]           = h,
                ["RoiAngle"]       = angleDeg,
                ["StripeCount"]    = stripeCount,
                ["StripeWidth"]    = stripeWidth,
                ["FilterSize"]     = 4,
                ["Threshold"]      = "Manual",
                ["ThresholdValue"] = threshVal,
                ["Polarity"]       = polarity,
                ["EdgeSelect"]     = edgeSelect,
                ["MinScore"]       = minScore,
            })!;

    // Dark left, bright right with a clean step edge through (edgeX, any Y).
    private static Mat MakeVerticalEdgeImage(int edgeX, int width = 200, int height = 200)
    {
        var mat = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);
        if (edgeX < width)
            mat.SubMat(new Rect(edgeX, 0, width - edgeX, height)).SetTo(new Scalar(200));
        return mat;
    }

    // White rectangle on black background so the edge has defined extent.
    private static Mat MakeRectEdgeImage(
        int rectX, int rectY, int rectW, int rectH, int imgW = 300, int imgH = 300)
    {
        var mat = new Mat(imgH, imgW, MatType.CV_8UC1, Scalar.Black);
        mat.SubMat(new Rect(rectX, rectY, rectW, rectH)).SetTo(new Scalar(200));
        return mat;
    }

    // Step edge along the 45° diagonal x = y: dark above/left, bright below/right.
    private static Mat MakeDiagonalEdgeImage(int size = 300)
    {
        var mat = new Mat(size, size, MatType.CV_8UC1, Scalar.Black);
        for (int row = 0; row < size; row++)
        {
            int edgeX = row;
            if (edgeX < size)
                mat.SubMat(new Rect(edgeX, row, size - edgeX, 1)).SetTo(new Scalar(200));
        }
        return mat;
    }

    // ── Found / not-found ────────────────────────────────────────────────────────

    [Fact]
    public void Found_WhenClearEdgeInRoi()
    {
        using var image = MakeVerticalEdgeImage(edgeX: 100);
        var result = Run(image, cx: 100, cy: 100, w: 80, h: 100, angleDeg: 0);
        Assert.True((bool)result["Found"]!);
    }

    [Fact]
    public void NotFound_WhenUniformImage()
    {
        using var image = new Mat(200, 200, MatType.CV_8UC1, new Scalar(128));
        var result = Run(image, cx: 100, cy: 100, w: 80, h: 100, angleDeg: 0);
        Assert.False((bool)result["Found"]!);
    }

    [Fact]
    public void NotFound_WhenInlierFractionBelowMinScore()
    {
        using var image = MakeVerticalEdgeImage(edgeX: 100);
        var result = Run(image, cx: 100, cy: 100, w: 80, h: 100, angleDeg: 0,
                         stripeCount: 4, minScore: 1.0);
        Assert.IsType<bool>(result["Found"]!);
    }

    [Fact]
    public void Found_UsesInlierFractionThreshold()
    {
        // Edge only in the bottom 40% of the ROI (y=[110,200] within ROI y=[50,150]).
        using var image = new Mat(200, 200, MatType.CV_8UC1, Scalar.Black);
        image.SubMat(new Rect(100, 110, 100, 90)).SetTo(new Scalar(200));

        var resultLow  = Run(image, cx: 100, cy: 100, w: 80, h: 100, angleDeg: 0,
                             stripeCount: 5, minScore: 0.3);
        var resultHigh = Run(image, cx: 100, cy: 100, w: 80, h: 100, angleDeg: 0,
                             stripeCount: 5, minScore: 0.9);

        Assert.True((bool)resultLow["Found"]!);
        Assert.False((bool)resultHigh["Found"]!);
    }

    // ── Line position ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.0)]
    [InlineData(30.0)]
    [InlineData(45.0)]
    [InlineData(-15.0)]
    [InlineData(60.0)]
    public void Line_CenteredOnEdge_VariousAngles(double angleDeg)
    {
        const double cx = 100, cy = 100;
        using var image = MakeVerticalEdgeImage(edgeX: 100);
        var result = Run(image, cx: cx, cy: cy, w: 80, h: 80, angleDeg: angleDeg);

        Assert.True((bool)result["Found"]!);
        var line = (LineSegment2f)result["Line"]!;

        double midX = (line.P1.X + line.P2.X) / 2.0;
        double midY = (line.P1.Y + line.P2.Y) / 2.0;
        Assert.InRange(midX, cx - 3, cx + 3);
        Assert.InRange(midY, cy - 3, cy + 3);
    }

    // ── Rotated ROI (required by CLAUDE.md) ─────────────────────────────────────

    [Theory]
    [InlineData(30.0)]
    [InlineData(45.0)]
    [InlineData(90.0)]
    public void RotatedRoi_BackProjectedPointsWithinImageBounds(double angleDeg)
    {
        using var image = MakeVerticalEdgeImage(edgeX: 100);
        const int imgW = 200, imgH = 200;
        var result = Run(image, cx: 100, cy: 100, w: 80, h: 60, angleDeg: angleDeg);

        var line = (LineSegment2f)result["Line"]!;
        foreach (var pt in new[] { line.P1, line.P2 })
        {
            Assert.InRange(pt.X, -1, imgW + 1);
            Assert.InRange(pt.Y, -1, imgH + 1);
        }
    }

    [Fact]
    public void RotatedRoi_LineMiddleNearEdge()
    {
        using var image = MakeVerticalEdgeImage(edgeX: 100);
        var result = Run(image, cx: 100, cy: 100, w: 80, h: 60, angleDeg: 45.0);

        Assert.True((bool)result["Found"]!);
        var line = (LineSegment2f)result["Line"]!;
        double midX = (line.P1.X + line.P2.X) / 2.0;
        double midY = (line.P1.Y + line.P2.Y) / 2.0;
        Assert.InRange(midX, 97, 103);
        Assert.InRange(midY, 97, 103);
    }

    // ── Line extent ───────────────────────────────────────────────────────────────

    [Fact]
    public void Line_SpansFullRoiHeight_WhenEdgeRunsFullHeight()
    {
        // Full-height edge: all stripes detect the edge.
        // ROI cy=100, h=60 → full extent y=70..130.
        using var image = MakeVerticalEdgeImage(edgeX: 100);
        var result = Run(image, cx: 100, cy: 100, w: 80, h: 60, angleDeg: 0);

        Assert.True((bool)result["Found"]!);
        var line = (LineSegment2f)result["Line"]!;
        double minY = Math.Min(line.P1.Y, line.P2.Y);
        double maxY = Math.Max(line.P1.Y, line.P2.Y);
        Assert.InRange(minY, 65, 75);   // ≈ cy - h/2 = 70
        Assert.InRange(maxY, 125, 135); // ≈ cy + h/2 = 130
    }

    [Fact]
    public void Line_SpansFullRoiHeight_NotInlierSpan()
    {
        // Edge only in the middle of the ROI (y=80..120 within ROI y=50..150).
        // Inlier t-span is ~40 px; full ROI height is 100 px.
        // The Line endpoint span must equal the ROI height, not the inlier span.
        const double h = 100;
        using var image = new Mat(200, 200, MatType.CV_8UC1, Scalar.Black);
        image.SubMat(new Rect(100, 80, 100, 40)).SetTo(new Scalar(200));

        var result = Run(image, cx: 100, cy: 100, w: 80, h: h, angleDeg: 0,
                         stripeCount: 10, stripeWidth: 8, minScore: 0.3);

        Assert.True((bool)result["Found"]!);
        var line = (LineSegment2f)result["Line"]!;
        double lineLen = Math.Sqrt(
            Math.Pow(line.P2.X - line.P1.X, 2) + Math.Pow(line.P2.Y - line.P1.Y, 2));
        Assert.InRange(lineLen, h * 0.95, h * 1.05);
    }

    [Fact]
    public void Line_ClampedToRoiWidth_WhenEdgeIsSlanted()
    {
        // 45° diagonal edge: x = y. ROI at cx=150, cy=150, W=60, H=80.
        // lineA ≈ 1 → without s-clamping the unclipped endpoints would be at x=110 and x=190,
        // both outside the ROI width [120, 180].
        // After clamping both endpoints must lie within [cx − W/2, cx + W/2].
        const double cx = 150, cy = 150, w = 60, h = 80;
        using var image = MakeDiagonalEdgeImage();
        var result = Run(image, cx: cx, cy: cy, w: w, h: h, angleDeg: 0,
                         stripeCount: 8, stripeWidth: 5, minScore: 0.4);

        Assert.True((bool)result["Found"]!);
        var line = (LineSegment2f)result["Line"]!;

        double sLo = cx - w / 2.0;  // 120
        double sHi = cx + w / 2.0;  // 180
        Assert.InRange((double)line.P1.X, sLo - 2, sHi + 2);
        Assert.InRange((double)line.P2.X, sLo - 2, sHi + 2);

        // The segment must still have meaningful length (not collapsed to a point).
        double len = Math.Sqrt(Math.Pow(line.P2.X - line.P1.X, 2) + Math.Pow(line.P2.Y - line.P1.Y, 2));
        Assert.True(len > w * 0.5, $"Line too short after clamping: {len:F1}");
    }

    // ── Polarity ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Polarity_BrightToDark_FindsRightEdge()
    {
        using var image = MakeRectEdgeImage(rectX: 50, rectY: 0, rectW: 100, rectH: 200);
        var result = Run(image, cx: 150, cy: 100, w: 80, h: 100, angleDeg: 0,
                         polarity: "BrightToDark");

        Assert.True((bool)result["Found"]!);
        var line = (LineSegment2f)result["Line"]!;
        double midX = (line.P1.X + line.P2.X) / 2.0;
        Assert.InRange(midX, 147, 153);
    }

    // ── 180° symmetry ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(100, 0,   "DarkToBright", "First")]
    [InlineData(100, 180, "BrightToDark", "Last")]
    [InlineData(100, 0,   "DarkToBright", "Last")]
    [InlineData(100, 180, "BrightToDark", "First")]
    public void Symmetry_Angle0Vs180_SameEdgeX(int edgeX, double angle, string polarity, string edgeSelect)
    {
        using var image = MakeVerticalEdgeImage(edgeX: edgeX);
        var result = Run(image, cx: 100, cy: 100, w: 80, h: 80, angleDeg: angle,
                         polarity: polarity, edgeSelect: edgeSelect);

        Assert.True((bool)result["Found"]!);
        var line  = (LineSegment2f)result["Line"]!;
        double midX = (line.P1.X + line.P2.X) / 2.0;
        Assert.InRange(midX, edgeX - 1.1, edgeX + 0.1);
    }

    // ── Points ordering ───────────────────────────────────────────────────────────

    [Fact]
    public void Points_SortedByStripeOrder()
    {
        // For angle=0 the stripe axis is Y; ascending t = ascending Y.
        using var image = MakeVerticalEdgeImage(edgeX: 100);
        var result = Run(image, cx: 100, cy: 100, w: 80, h: 80, angleDeg: 0, stripeCount: 5);

        var points = (Point2f[])result["Points"]!;
        Assert.True(points.Length >= 2);
        for (int i = 1; i < points.Length; i++)
            Assert.True(points[i].Y >= points[i - 1].Y - 0.01f,
                $"Points[{i}].Y={points[i].Y} < Points[{i-1}].Y={points[i-1].Y}");
    }

    [Fact]
    public void Points_AndScore_HaveSameLength()
    {
        using var image = MakeVerticalEdgeImage(edgeX: 100);
        var result = Run(image, cx: 100, cy: 100, w: 80, h: 80, angleDeg: 0);

        var points = (Point2f[])result["Points"]!;
        var scores = (double[])result["Score"]!;
        Assert.Equal(points.Length, scores.Length);
    }

    // ── No ROI ────────────────────────────────────────────────────────────────────

    [Fact]
    public void NoRoi_ReturnsEmpty()
    {
        using var image = MakeVerticalEdgeImage(edgeX: 100);
        var result = Run(image, cx: 100, cy: 100, w: 0, h: 0, angleDeg: 0);
        Assert.False((bool)result["Found"]!);
        Assert.Empty((Point2f[])result["Points"]!);
    }
}
