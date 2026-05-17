using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class ChannelCircleTests
{
    private static readonly string ImagePath =
        Path.Combine(AppContext.BaseDirectory, "TestImages", "RGBCircles.png");

    [Fact]
    public async Task RGBCircles_PerChannelDetection_CountsMatch()
    {
        // Load image once upfront to derive scale-dependent HoughCircles parameters
        using var probe = Cv2.ImRead(ImagePath, ImreadModes.Color);
        var minDist   = probe.Width / 10.0;
        var minRadius = probe.Width / 30;
        var maxRadius = probe.Width / 6;

        // Build the full flow: Load → SplitChannels → DetectCircles (×3, one per channel)
        // O3R, O3G, O3B each wire their Image parameter to a different output port of O2
        var flow = new FlowDef(
        [
            new Operator
            {
                Id           = "O1",
                DisplayName  = "Load",
                Type         = new LoadImageOperator(),
                Parameters   = [new ParameterValue { Name = "FilePath", Value = ImagePath }],
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
                DisplayName  = "DetectRed",
                Type         = new DetectCirclesOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",     Source = new SourceRef("O2", "Red") },
                    new ParameterValue { Name = "MinDist",   Value  = minDist   },
                    new ParameterValue { Name = "Param1",    Value  = 200.0     },
                    new ParameterValue { Name = "Param2",    Value  = 19.0      },
                    new ParameterValue { Name = "MinRadius", Value  = minRadius },
                    new ParameterValue { Name = "MaxRadius", Value  = maxRadius }
                ],
                Dependencies = [new Dependency("D2", "O2")]
            },
            new Operator
            {
                Id           = "O4",
                DisplayName  = "DetectGreen",
                Type         = new DetectCirclesOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",     Source = new SourceRef("O2", "Green") },
                    new ParameterValue { Name = "MinDist",   Value  = minDist   },
                    new ParameterValue { Name = "Param1",    Value  = 200.0     },
                    new ParameterValue { Name = "Param2",    Value  = 19.0      },
                    new ParameterValue { Name = "MinRadius", Value  = minRadius },
                    new ParameterValue { Name = "MaxRadius", Value  = maxRadius }
                ],
                Dependencies = [new Dependency("D3", "O2")]
            },
            new Operator
            {
                Id           = "O5",
                DisplayName  = "DetectBlue",
                Type         = new DetectCirclesOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",     Source = new SourceRef("O2", "Blue") },
                    new ParameterValue { Name = "MinDist",   Value  = minDist   },
                    new ParameterValue { Name = "Param1",    Value  = 200.0     },
                    new ParameterValue { Name = "Param2",    Value  = 20.0      },
                    new ParameterValue { Name = "MinRadius", Value  = minRadius },
                    new ParameterValue { Name = "MaxRadius", Value  = maxRadius }
                ],
                Dependencies = [new Dependency("D4", "O2")]
            }
        ]);

        var executor = new FlowEx(flow);
        await executor.RunAllAsync();

        var redCircles   = (CircleSegment[])executor.IntermediateResults["O3"]!;
        var greenCircles = (CircleSegment[])executor.IntermediateResults["O4"]!;
        var blueCircles  = (CircleSegment[])executor.IntermediateResults["O5"]!;

        Assert.Equal(2, redCircles.Length);
        Assert.Equal(3, greenCircles.Length);
        Assert.Equal(3, blueCircles.Length);
    }
}
