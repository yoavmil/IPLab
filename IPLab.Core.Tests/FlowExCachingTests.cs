using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class FlowExCachingTests
{
    private static string WriteTempImage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"iplab-cache-test-{Guid.NewGuid()}.png");
        using var img = new Mat(64, 64, MatType.CV_8UC3, new Scalar(100, 150, 200));
        Cv2.ImWrite(path, img);
        return path;
    }

    [Fact]
    public async Task SecondRun_WithUnchangedParams_SkipsLoadImage()
    {
        var path = WriteTempImage();
        try
        {
            var flow = new FlowDef(
            [
                new Operator
                {
                    Id           = "O1",
                    DisplayName  = "Load",
                    Type         = new LoadImageOperator(),
                    Parameters   = [new ParameterValue { Name = "FilePaths", Value = new string[] { path } }],
                    Dependencies = []
                },
                new Operator
                {
                    Id           = "O2",
                    DisplayName  = "Gray",
                    Type         = new ConvertToGrayscaleOperator(),
                    Parameters   = [new ParameterValue { Name = "Image", Source = new SourceRef("O1", "Image") }],
                    Dependencies = [new Dependency("D1", "O1")]
                }
            ]);

            var ex = new FlowEx(flow, enableCaching: true);

            await ex.RunAllAsync();
            var o1After1 = ((IReadOnlyDictionary<string, object?>)ex.IntermediateResults["O1"]!)["Image"];

            await ex.RunAllAsync();
            var o1After2 = ((IReadOnlyDictionary<string, object?>)ex.IntermediateResults["O1"]!)["Image"];

            // Same object reference proves LoadImage was not re-executed.
            Assert.True(ReferenceEquals(o1After1, o1After2), "LoadImage result should be reused on unchanged second run.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task SecondRun_WhenUpstreamParamChanges_RerunsDownstreamOnly()
    {
        var path = WriteTempImage();
        try
        {
            var kernelParam = new ParameterValue { Name = "KernelSize", Value = 3 };
            var flow = new FlowDef(
            [
                new Operator
                {
                    Id           = "O1",
                    DisplayName  = "Load",
                    Type         = new LoadImageOperator(),
                    Parameters   = [new ParameterValue { Name = "FilePaths", Value = new string[] { path } }],
                    Dependencies = []
                },
                new Operator
                {
                    Id           = "O2",
                    DisplayName  = "Blur",
                    Type         = new GaussianBlurOperator(),
                    Parameters   =
                    [
                        new ParameterValue { Name = "Image", Source = new SourceRef("O1", "Image") },
                        kernelParam
                    ],
                    Dependencies = [new Dependency("D1", "O1")]
                }
            ]);

            var ex = new FlowEx(flow, enableCaching: true);

            await ex.RunAllAsync();
            var o1After1 = ((IReadOnlyDictionary<string, object?>)ex.IntermediateResults["O1"]!)["Image"];
            var o2After1 = ex.IntermediateResults["O2"];

            // Change a scalar param on O2, leave O1 untouched.
            kernelParam.Value = 5;

            await ex.RunAllAsync();
            var o1After2 = ((IReadOnlyDictionary<string, object?>)ex.IntermediateResults["O1"]!)["Image"];
            var o2After2 = ex.IntermediateResults["O2"];

            Assert.True(ReferenceEquals(o1After1, o1After2), "O1 (LoadImage) should be cached — its params did not change.");
            Assert.False(ReferenceEquals(o2After1, o2After2), "O2 (GaussianBlur) should re-run — its KernelSize changed.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
