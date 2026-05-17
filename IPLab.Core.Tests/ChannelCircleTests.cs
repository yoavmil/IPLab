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
        using var probe = Cv2.ImRead(ImagePath, ImreadModes.Color);
        var minDist   = probe.Width / 10.0;
        var minRadius = 30;
        var maxRadius = 50;

        // Flow: Load → Split → Threshold(×3) → DetectCircles(×3)
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
                DisplayName  = "ThreshRed",
                Type         = new ThresholdOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",  Source = new SourceRef("O2", "Red") },
                    new ParameterValue { Name = "Thresh", Value  = 128.0 },
                    new ParameterValue { Name = "MaxVal", Value  = 255.0 }
                ],
                Dependencies = [new Dependency("D2", "O2")]
            },
            new Operator
            {
                Id           = "O4",
                DisplayName  = "ThreshGreen",
                Type         = new ThresholdOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",  Source = new SourceRef("O2", "Green") },
                    new ParameterValue { Name = "Thresh", Value  = 128.0 },
                    new ParameterValue { Name = "MaxVal", Value  = 255.0 }
                ],
                Dependencies = [new Dependency("D3", "O2")]
            },
            new Operator
            {
                Id           = "O5",
                DisplayName  = "ThreshBlue",
                Type         = new ThresholdOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",  Source = new SourceRef("O2", "Blue") },
                    new ParameterValue { Name = "Thresh", Value  = 128.0 },
                    new ParameterValue { Name = "MaxVal", Value  = 255.0 }
                ],
                Dependencies = [new Dependency("D4", "O2")]
            },
            new Operator
            {
                Id           = "O6",
                DisplayName  = "DetectRed",
                Type         = new DetectCirclesOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",     Source = new SourceRef("O3", "Image") },
                    new ParameterValue { Name = "MinDist",   Value  = minDist   },
                    new ParameterValue { Name = "Param1",    Value  = 150.0     },
                    new ParameterValue { Name = "Param2",    Value  = 10.0      },
                    new ParameterValue { Name = "MinRadius", Value  = minRadius },
                    new ParameterValue { Name = "MaxRadius", Value  = maxRadius }
                ],
                Dependencies = [new Dependency("D5", "O3")]
            },
            new Operator
            {
                Id           = "O7",
                DisplayName  = "DetectGreen",
                Type         = new DetectCirclesOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",     Source = new SourceRef("O4", "Image") },
                    new ParameterValue { Name = "MinDist",   Value  = minDist   },
                    new ParameterValue { Name = "Param1",    Value  = 150.0     },
                    new ParameterValue { Name = "Param2",    Value  = 10.0      },
                    new ParameterValue { Name = "MinRadius", Value  = minRadius },
                    new ParameterValue { Name = "MaxRadius", Value  = maxRadius }
                ],
                Dependencies = [new Dependency("D6", "O4")]
            },
            new Operator
            {
                Id           = "O8",
                DisplayName  = "DetectBlue",
                Type         = new DetectCirclesOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",     Source = new SourceRef("O5", "Image") },
                    new ParameterValue { Name = "MinDist",   Value  = minDist   },
                    new ParameterValue { Name = "Param1",    Value  = 150.0     },
                    new ParameterValue { Name = "Param2",    Value  = 10.0      },
                    new ParameterValue { Name = "MinRadius", Value  = minRadius },
                    new ParameterValue { Name = "MaxRadius", Value  = maxRadius }
                ],
                Dependencies = [new Dependency("D7", "O5")]
            }
        ]);

        var executor = new FlowEx(flow);
        await executor.RunAllAsync();

        var redCircles   = (CircleSegment[])executor.IntermediateResults["O6"]!;
        var greenCircles = (CircleSegment[])executor.IntermediateResults["O7"]!;
        var blueCircles  = (CircleSegment[])executor.IntermediateResults["O8"]!;

        Assert.Equal(2, redCircles.Length);
        Assert.Equal(3, greenCircles.Length);
        Assert.Equal(3, blueCircles.Length);
    }
}
