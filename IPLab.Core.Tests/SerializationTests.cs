using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;
using IPLab.Core.Serialization;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class SerializationTests
{
    private static readonly string ImagePath =
        Path.Combine(AppContext.BaseDirectory, "TestImages", "RGBCircles.png");

    [Fact]
    public async Task FlowRoundTrip_HsvGrayscale_DetectsAllCircles()
    {
        using var probe = Cv2.ImRead(ImagePath, ImreadModes.Color);
        var minDist = probe.Width / 10.0;

        // Build flow: Load → ConvertToGrayscale(HsvValue) → Threshold → DetectCircles
        var original = new FlowDef(
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
                DisplayName  = "ToGray",
                Type         = new ConvertToGrayscaleOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",  Source = new SourceRef("O1", "Image") },
                    new ParameterValue { Name = "Method", Value  = "HsvValue" }
                ],
                Dependencies = [new Dependency("D1", "O1")]
            },
            new Operator
            {
                Id           = "O3",
                DisplayName  = "Thresh",
                Type         = new ThresholdOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",  Source = new SourceRef("O2", "Image") },
                    new ParameterValue { Name = "Thresh", Value  = 128.0 }
                ],
                Dependencies = [new Dependency("D2", "O2")]
            },
            new Operator
            {
                Id           = "O4",
                DisplayName  = "Detect",
                Type         = new DetectCirclesOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",     Source = new SourceRef("O3", "Image") },
                    new ParameterValue { Name = "MinDist",   Value  = minDist },
                    new ParameterValue { Name = "Param1",    Value  = 150.0   },
                    new ParameterValue { Name = "Param2",    Value  = 10.0    },
                    new ParameterValue { Name = "MinRadius", Value  = 30      },
                    new ParameterValue { Name = "MaxRadius", Value  = 50      }
                ],
                Dependencies = [new Dependency("D3", "O3")]
            }
        ]);

        // Round-trip through JSON
        var json     = FlowDefSerializer.Serialize(original);
        var restored = FlowDefSerializer.Deserialize(json, OperatorRegistry.CreateDefault());

        var executor = new FlowEx(restored);
        await executor.RunAllAsync();

        var circles = (CircleSegment[])executor.IntermediateResults["O4"]!;
        Assert.Equal(8, circles.Length); // 2 red + 3 green + 3 blue
    }
}
