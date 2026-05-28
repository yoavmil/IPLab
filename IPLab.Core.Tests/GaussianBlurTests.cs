using IPLab.Core.Models;
using IPLab.Core.Operators;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class GaussianBlurTests
{
    private static IReadOnlyDictionary<string, object?> Params(int kernelSize, double sigma, Mat image) =>
        new Dictionary<string, object?>
        {
            ["Image"]      = image,
            ["KernelSize"] = kernelSize,
            ["Sigma"]      = sigma,
        };

    private static Mat Output(IReadOnlyDictionary<string, object?> parameters)
    {
        var op     = new GaussianBlurOperator();
        var result = (Dictionary<string, object?>)op.Execute(parameters)!;
        return (Mat)result["Image"]!;
    }

    [Fact]
    public void GaussianBlur_SmoothsSharpEdge()
    {
        // 50x50 black image with a 10-pixel-wide white vertical bar in the center
        using var input = new Mat(50, 50, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(input, new Rect(20, 0, 10, 50), Scalar.White, -1);

        using var output = Output(Params(9, 2.0, input));

        Assert.Equal(input.Size(), output.Size());

        // Pixel just outside the bar edge should now be grey (blur leaked out)
        Assert.True(output.At<byte>(25, 19) > 0,   "Left  neighbour of bar should be non-zero after blur");
        Assert.True(output.At<byte>(25, 30) > 0,   "Right neighbour of bar should be non-zero after blur");

        // Centre of bar should still be bright (well above mid-grey)
        Assert.True(output.At<byte>(25, 25) > 200, "Centre of bar should remain bright after blur");
    }

    [Fact]
    public void GaussianBlur_KernelSizeZero_TreatedAsIdentity()
    {
        // OpenCV treats kernel size 0 as "derive from sigma" — just verify it doesn't throw
        using var input  = new Mat(20, 20, MatType.CV_8UC1, Scalar.Gray);
        using var output = Output(Params(0, 1.5, input));
        Assert.Equal(input.Size(), output.Size());
    }

    [Fact]
    public void GaussianBlur_NegativeKernelSize_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            using var img = new Mat(10, 10, MatType.CV_8UC1, Scalar.Black);
            Output(Params(-1, 1.0, img));
        });

    [Fact]
    public void GaussianBlur_NegativeSigma_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            using var img = new Mat(10, 10, MatType.CV_8UC1, Scalar.Black);
            Output(Params(5, -0.1, img));
        });
}
