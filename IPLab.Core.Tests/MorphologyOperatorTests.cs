using IPLab.Core.Operators;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class MorphologyOperatorTests
{
    private static Dictionary<string, object?> P(Mat image, string op,
        string shape = "Rect", int kernelSize = 3, int iterations = 1)
        => new()
        {
            ["Image"]       = image,
            ["Operation"]   = op,
            ["KernelShape"] = shape,
            ["KernelSize"]  = kernelSize,
            ["Iterations"]  = iterations,
        };

    [Fact]
    public void Erode_UniformImage_Unchanged()
    {
        using var src    = new Mat(8, 8, MatType.CV_8UC1, new Scalar(200));
        using var result = (Mat)new MorphologyOperator().Execute(P(src, "Erode"))!;
        Assert.Equal(200, result.At<byte>(4, 4));
    }

    [Fact]
    public void Dilate_UniformImage_Unchanged()
    {
        using var src    = new Mat(8, 8, MatType.CV_8UC1, new Scalar(100));
        using var result = (Mat)new MorphologyOperator().Execute(P(src, "Dilate"))!;
        Assert.Equal(100, result.At<byte>(4, 4));
    }

    [Fact]
    public void Erode_IsolatedBrightPixel_Disappears()
    {
        // Single bright pixel on a black background: erode's min-filter zeroes it out.
        using var src = new Mat(5, 5, MatType.CV_8UC1, Scalar.Black);
        src.Set<byte>(2, 2, 255);

        using var result = (Mat)new MorphologyOperator().Execute(P(src, "Erode"))!;
        Assert.Equal(0, result.At<byte>(2, 2));
    }

    [Fact]
    public void Dilate_IsolatedBrightPixel_Expands()
    {
        // Single bright pixel: dilate's max-filter spreads it to a 3×3 patch.
        using var src = new Mat(5, 5, MatType.CV_8UC1, Scalar.Black);
        src.Set<byte>(2, 2, 255);

        using var result = (Mat)new MorphologyOperator().Execute(P(src, "Dilate"))!;
        Assert.Equal(255, result.At<byte>(1, 1));
        Assert.Equal(255, result.At<byte>(1, 3));
        Assert.Equal(255, result.At<byte>(3, 1));
        Assert.Equal(255, result.At<byte>(3, 3));
    }

    [Fact]
    public void Open_RemovesIsolatedSpeck()
    {
        // Open = erode then dilate. A single pixel is removed by erode and never restored.
        using var src = new Mat(5, 5, MatType.CV_8UC1, Scalar.Black);
        src.Set<byte>(2, 2, 255);

        using var result = (Mat)new MorphologyOperator().Execute(P(src, "Open"))!;
        Assert.Equal(0, result.At<byte>(2, 2));
    }

    [Fact]
    public void Erode_LargerKernel_RemovesMorePixels()
    {
        // 7×7 image, bright 3×3 center region. 3×3 erode leaves center pixel; 5×5 erode wipes it.
        using var src = new Mat(7, 7, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(src, new Point(2, 2), new Point(4, 4), new Scalar(255), -1);

        using var result3 = (Mat)new MorphologyOperator().Execute(P(src, "Erode", kernelSize: 3))!;
        using var result5 = (Mat)new MorphologyOperator().Execute(P(src, "Erode", kernelSize: 5))!;

        Assert.Equal(255, result3.At<byte>(3, 3)); // center survives 3×3 erode
        Assert.Equal(0,   result5.At<byte>(3, 3)); // 5×5 erode wipes it
    }

    [Fact]
    public void Erode_TwoIterations_ErodesMoreThanOne()
    {
        // 7×7 image, bright 3×3 center region. 1 iteration leaves center pixel; 2 iterations erase it.
        using var src = new Mat(7, 7, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(src, new Point(2, 2), new Point(4, 4), new Scalar(255), -1);

        using var once  = (Mat)new MorphologyOperator().Execute(P(src, "Erode", iterations: 1))!;
        using var twice = (Mat)new MorphologyOperator().Execute(P(src, "Erode", iterations: 2))!;

        Assert.Equal(255, once.At<byte>(3, 3));  // survives 1 pass
        Assert.Equal(0,   twice.At<byte>(3, 3)); // gone after 2 passes
    }
}
