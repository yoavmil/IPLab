using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class ChannelBlobTests
{
    private static readonly string ImagePath =
        Path.Combine(AppContext.BaseDirectory, "TestImages", "RGBCircles.png");

    [Fact]
    public async Task RGBCircles_PerChannelBlobDetection_CountsMatch()
    {
        using var probe = Cv2.ImRead(ImagePath, ImreadModes.Color);
        var minDist = (float)(probe.Width / 10.0);
        var minArea = (float)(probe.Width * probe.Height / 200.0);

        var flow = new FlowDef(
        [
            new Operator
            {
                Id           = "O1",
                DisplayName  = "Load",
                Type         = new LoadImageOperator(),
                Parameters   = [new ParameterValue { Name = "FilePaths", Value = new string[] { ImagePath } }],
                Dependencies = []
            },
            new Operator
            {
                Id           = "O2",
                DisplayName  = "Split",
                Type         = new SplitChannelsOperator(),
                Parameters   = [new ParameterValue { Name = "Image", Source = new SourceRef("O1", "Image") }],
                Dependencies = [new Dependency("D1", "O1")]
            },
            new Operator
            {
                Id           = "O3",
                DisplayName  = "BlobsRed",
                Type         = new DetectSimpleBlobsOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",               Source = new SourceRef("O2", "Red") },
                    new ParameterValue { Name = "MinCircularity",      Value  = 0.8     },
                    new ParameterValue { Name = "MinArea",             Value  = (double)minArea },
                    new ParameterValue { Name = "MaxArea",             Value  = 50000.0 },
                    new ParameterValue { Name = "MinDistBetweenBlobs", Value  = (double)minDist },
                    new ParameterValue { Name = "MinThreshold",        Value  = 100.0 },
                    new ParameterValue { Name = "MaxThreshold",        Value  = 200.0 }
                ],
                Dependencies = [new Dependency("D2", "O2")]
            },
            new Operator
            {
                Id           = "O4",
                DisplayName  = "BlobsGreen",
                Type         = new DetectSimpleBlobsOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",               Source = new SourceRef("O2", "Green") },
                    new ParameterValue { Name = "MinCircularity",      Value  = 0.8     },
                    new ParameterValue { Name = "MinArea",             Value  = (double)minArea },
                    new ParameterValue { Name = "MaxArea",             Value  = 50000.0 },
                    new ParameterValue { Name = "MinDistBetweenBlobs", Value  = (double)minDist },
                    new ParameterValue { Name = "MinThreshold",        Value  = 100.0 },
                    new ParameterValue { Name = "MaxThreshold",        Value  = 200.0 }
                ],
                Dependencies = [new Dependency("D3", "O2")]
            },
            new Operator
            {
                Id           = "O5",
                DisplayName  = "BlobsBlue",
                Type         = new DetectSimpleBlobsOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",               Source = new SourceRef("O2", "Blue") },
                    new ParameterValue { Name = "MinCircularity",      Value  = 0.8     },
                    new ParameterValue { Name = "MinArea",             Value  = (double)minArea },
                    new ParameterValue { Name = "MaxArea",             Value  = 50000.0 },
                    new ParameterValue { Name = "MinDistBetweenBlobs", Value  = (double)minDist },
                    new ParameterValue { Name = "MinThreshold",        Value  = 100.0 },
                    new ParameterValue { Name = "MaxThreshold",        Value  = 200.0 }
                ],
                Dependencies = [new Dependency("D4", "O2")]
            }
        ]);

        var executor = new FlowEx(flow);
        await executor.RunAllAsync();

        KeyPoint[] GetBlobs(string id) =>
            (KeyPoint[])((Dictionary<string, object?>)executor.IntermediateResults[id]!)["Blobs"]!;

        var redBlobs   = GetBlobs("O3");
        var greenBlobs = GetBlobs("O4");
        var blueBlobs  = GetBlobs("O5");

        Assert.Equal(2, redBlobs.Length);
        Assert.Equal(3, greenBlobs.Length);
        Assert.Equal(3, blueBlobs.Length);
    }
}
