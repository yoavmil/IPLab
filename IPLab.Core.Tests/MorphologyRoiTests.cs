using IPLab.Core.Operators;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class MorphologyRoiTests
{
    private static readonly MorphologyOperator Op = new();

    private static Mat Execute(Mat image, int roiX = 0, int roiY = 0, int roiW = 0, int roiH = 0,
        string operation = "Erode", int kernelSize = 3)
    {
        var outputs = (Dictionary<string, object?>)Op.Execute(new Dictionary<string, object?>
        {
            ["Image"]       = image,
            ["Operation"]   = operation,
            ["KernelShape"] = "Rect",
            ["KernelSize"]  = kernelSize,
            ["Iterations"]  = 1,
            ["RoiX"]        = roiX,
            ["RoiY"]        = roiY,
            ["RoiW"]        = roiW,
            ["RoiH"]        = roiH,
        })!;
        return (Mat)outputs["Image"]!;
    }

    // ── RoiParameters.Extract ────────────────────────────────────────────────

    [Fact]
    public void Extract_ZeroWidth_ReturnsNull()
    {
        var roi = RoiParameters.Extract(new Dictionary<string, object?>
            { ["RoiX"] = 0, ["RoiY"] = 0, ["RoiW"] = 0, ["RoiH"] = 10 });
        Assert.Null(roi);
    }

    [Fact]
    public void Extract_ZeroHeight_ReturnsNull()
    {
        var roi = RoiParameters.Extract(new Dictionary<string, object?>
            { ["RoiX"] = 0, ["RoiY"] = 0, ["RoiW"] = 10, ["RoiH"] = 0 });
        Assert.Null(roi);
    }

    [Fact]
    public void Extract_NegativeWidth_ReturnsNull()
    {
        var roi = RoiParameters.Extract(new Dictionary<string, object?>
            { ["RoiX"] = 0, ["RoiY"] = 0, ["RoiW"] = -5, ["RoiH"] = 10 });
        Assert.Null(roi);
    }

    [Fact]
    public void Extract_NegativeHeight_ReturnsNull()
    {
        var roi = RoiParameters.Extract(new Dictionary<string, object?>
            { ["RoiX"] = 0, ["RoiY"] = 0, ["RoiW"] = 10, ["RoiH"] = -5 });
        Assert.Null(roi);
    }

    [Fact]
    public void Extract_AbsentKeys_ReturnsNull()
    {
        var roi = RoiParameters.Extract(new Dictionary<string, object?>());
        Assert.Null(roi);
    }

    [Fact]
    public void Extract_ValidRect_ReturnsRoiDef()
    {
        var roi = RoiParameters.Extract(new Dictionary<string, object?>
            { ["RoiX"] = 5, ["RoiY"] = 10, ["RoiW"] = 30, ["RoiH"] = 20 });
        Assert.NotNull(roi);
        Assert.Equal(5,  roi.X);
        Assert.Equal(10, roi.Y);
        Assert.Equal(30, roi.Width);
        Assert.Equal(20, roi.Height);
    }

    // ── Operator behavior ────────────────────────────────────────────────────

    [Fact]
    public void Morphology_WithZeroRoi_AppliesToFullImage()
    {
        using var src = new Mat(20, 20, MatType.CV_8UC1, Scalar.Black);
        src.Set<byte>(10, 10, 255);
        using var result = Execute(src); // roiW=0, roiH=0 → full image
        Assert.Equal(0, result.At<byte>(10, 10));
    }

    [Fact]
    public void Morphology_NegativeRoiDimensions_AppliesToFullImage()
    {
        using var src = new Mat(20, 20, MatType.CV_8UC1, Scalar.Black);
        src.Set<byte>(10, 10, 255);
        using var result = Execute(src, roiW: -1, roiH: -1); // negative → full image
        Assert.Equal(0, result.At<byte>(10, 10));
    }

    [Fact]
    public void Morphology_WithRoi_AppliesOnlyInsideRoi()
    {
        using var src = new Mat(30, 30, MatType.CV_8UC1, Scalar.Black);
        src.Set<byte>(5,  5,  255); // inside  ROI (0,0,15,15)
        src.Set<byte>(22, 22, 255); // outside ROI
        using var result = Execute(src, roiX: 0, roiY: 0, roiW: 15, roiH: 15);
        Assert.Equal(0,   result.At<byte>(5,  5));  // eroded inside
        Assert.Equal(255, result.At<byte>(22, 22)); // preserved outside
    }

    [Fact]
    public void Morphology_RoiPartiallyExceedsBounds_ClampedAndApplied()
    {
        // 30x30 image; ROI starts at (20,20) with size 20x20 — extends beyond the image.
        // After clamping: effective rect is (20,20,10,10).
        using var src = new Mat(30, 30, MatType.CV_8UC1, Scalar.Black);
        src.Set<byte>(25, 25, 255); // inside clamped area
        src.Set<byte>(5,  5,  255); // outside the (clamped) ROI

        using var result = Execute(src, roiX: 20, roiY: 20, roiW: 20, roiH: 20);

        Assert.Equal(0,   result.At<byte>(25, 25)); // eroded
        Assert.Equal(255, result.At<byte>(5,  5));  // untouched
        Assert.Equal(30,  result.Rows);
        Assert.Equal(30,  result.Cols);
    }

    [Fact]
    public void Morphology_RoiEntirelyOutsideBounds_ReturnsOriginalUnchanged()
    {
        // ROI origin (50,50) is beyond the 30x30 image → clamped rect has zero area.
        using var src = new Mat(30, 30, MatType.CV_8UC1, Scalar.Black);
        src.Set<byte>(10, 10, 255);

        using var result = Execute(src, roiX: 50, roiY: 50, roiW: 10, roiH: 10);

        // Nothing processed — original pixel preserved.
        Assert.Equal(255, result.At<byte>(10, 10));
        Assert.Equal(30,  result.Rows);
        Assert.Equal(30,  result.Cols);
    }

    [Fact]
    public void Morphology_WithRoi_OutputSizeMatchesInput()
    {
        using var img    = new Mat(80, 120, MatType.CV_8UC1, new Scalar(200));
        using var result = Execute(img, roiX: 10, roiY: 10, roiW: 40, roiH: 30);
        Assert.Equal(80,  result.Rows);
        Assert.Equal(120, result.Cols);
    }
}
