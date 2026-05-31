using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class HistogramEqualizationTests
{
    private static Mat RunEqualize(Mat image, string method = "Equalize",
                                   double clipLimit = 2.0, int tileGridSize = 8)
    {
        var op = new HistogramEqualizationOperator();
        return (Mat)op.Execute(new Dictionary<string, object?>
        {
            ["Image"]        = image,
            ["Method"]       = method,
            ["ClipLimit"]    = clipLimit,
            ["TileGridSize"] = tileGridSize,
        })!;
    }

    [Fact]
    public void Equalize_OutputSizeMatchesInput()
    {
        using var input  = new Mat(64, 64, MatType.CV_8UC1, Scalar.All(30));
        using var output = RunEqualize(input);
        Assert.Equal(input.Size(), output.Size());
    }

    [Fact]
    public void Equalize_DarkImage_SpreadsBrightnessUp()
    {
        // Image with pixels spanning 0–30 only — equalization should stretch max up toward 255.
        using var input = new Mat(256, 1, MatType.CV_8UC1);
        for (int i = 0; i < 256; i++)
            input.At<byte>(i, 0) = (byte)(i * 30 / 255);

        using var output = RunEqualize(input);

        double maxVal;
        Cv2.MinMaxLoc(output, out _, out maxVal);
        Assert.True(maxVal > 200, $"Equalized max {maxVal} should be stretched toward 255");
    }

    [Fact]
    public void Equalize_FullRangeImage_OutputRemainsFullRange()
    {
        // Image already spanning 0–255 — equalization should keep full range.
        using var input = new Mat(256, 1, MatType.CV_8UC1);
        for (int i = 0; i < 256; i++)
            input.At<byte>(i, 0) = (byte)i;

        using var output = RunEqualize(input);

        double minVal, maxVal;
        Cv2.MinMaxLoc(output, out minVal, out maxVal);
        Assert.True(maxVal >= 250, $"Max {maxVal} should stay near 255");
        Assert.True(minVal <= 5,   $"Min {minVal} should stay near 0");
    }

    [Fact]
    public void CLAHE_OutputSizeMatchesInput()
    {
        using var input  = new Mat(64, 64, MatType.CV_8UC1, Scalar.All(50));
        using var output = RunEqualize(input, method: "CLAHE");
        Assert.Equal(input.Size(), output.Size());
    }

    [Fact]
    public void CLAHE_DarkImage_SpreadsBrightnessUp()
    {
        using var input  = new Mat(128, 128, MatType.CV_8UC1, Scalar.All(20));
        using var output = RunEqualize(input, method: "CLAHE", clipLimit: 2.0, tileGridSize: 8);

        double maxVal;
        Cv2.MinMaxLoc(output, out _, out maxVal);
        Assert.True(maxVal > 20, $"CLAHE max {maxVal} should exceed input value 20");
    }

    [Fact]
    public void CLAHE_HigherClipLimit_GreaterContrast()
    {
        // Gradient image: CLAHE with higher clip limit should produce wider value range.
        using var input = new Mat(128, 128, MatType.CV_8UC1);
        for (int r = 0; r < 128; r++)
            for (int c = 0; c < 128; c++)
                input.At<byte>(r, c) = (byte)(r + c);

        using var lowClip  = RunEqualize(input, method: "CLAHE", clipLimit: 1.0);
        using var highClip = RunEqualize(input, method: "CLAHE", clipLimit: 40.0);

        Cv2.MinMaxLoc(lowClip,  out double lowMin,  out double lowMax);
        Cv2.MinMaxLoc(highClip, out double highMin, out double highMax);

        Assert.True(highMax - highMin >= lowMax - lowMin,
            $"Higher clip limit ({highMax - highMin}) should produce at least as wide a range as lower ({lowMax - lowMin})");
    }

    // Integration: HistogramEqualization → Threshold in a FlowEx pipeline.
    // This catches the single-port return-value convention bug (Execute must return
    // the Mat directly, not wrapped in a dictionary, when there is only one output port).
    [Fact]
    public async Task FlowEx_HistEq_Into_Threshold_DoesNotThrow()
    {
        using var scratch = new Mat(64, 64, MatType.CV_8UC1, Scalar.All(30));
        var tempPath = Path.Combine(Path.GetTempPath(), $"histtest_{Guid.NewGuid()}.png");
        scratch.SaveImage(tempPath);

        try
        {
            var flow = new FlowDef(
            [
                new Operator
                {
                    Id           = "O1",
                    DisplayName  = "Load",
                    Type         = new LoadImageOperator(),
                    Parameters   = [new ParameterValue { Name = "FilePaths", Value = new string[] { tempPath } }],
                    Dependencies = []
                },
                new Operator
                {
                    Id           = "O2",
                    DisplayName  = "Grayscale",
                    Type         = new ConvertToGrayscaleOperator(),
                    Parameters   = [new ParameterValue { Name = "Image", Source = new SourceRef("O1", "Image") }],
                    Dependencies = [new Dependency("D_O1_O2", "O1")]
                },
                new Operator
                {
                    Id           = "O3",
                    DisplayName  = "HistEq",
                    Type         = new HistogramEqualizationOperator(),
                    Parameters   = [new ParameterValue { Name = "Image", Source = new SourceRef("O2", "Image") }],
                    Dependencies = [new Dependency("D_O2_O3", "O2")]
                },
                new Operator
                {
                    Id           = "O4",
                    DisplayName  = "Threshold",
                    Type         = new ThresholdOperator(),
                    Parameters   =
                    [
                        new ParameterValue { Name = "Image",  Source = new SourceRef("O3", "Image") },
                        new ParameterValue { Name = "Method", Value  = "Adaptive" },
                        new ParameterValue { Name = "BlockSize", Value = 11 },
                        new ParameterValue { Name = "C",         Value = 2.0 },
                    ],
                    Dependencies = [new Dependency("D_O3_O4", "O3")]
                },
            ]);

            var ex = new FlowEx(flow);
            await ex.RunAllAsync();

            Assert.Equal(OperatorStatus.Success, ex.Statuses["O4"]);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
