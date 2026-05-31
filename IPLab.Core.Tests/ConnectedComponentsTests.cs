using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;

namespace IPLab.Core.Tests;

public class ConnectedComponentsTests
{
    private static readonly string ImagePath =
        Path.Combine(AppContext.BaseDirectory, "TestImages", "RGBCircles.png");

    [Fact]
    public async Task RGBCircles_AllChannels_SegmentationCountsMatch()
    {
        // Flow: Load → SplitChannels → Threshold(×3) → ConnectedComponents(×3)
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
                Dependencies = [new Dependency("D_O1_O2", "O1")]
            },
            new Operator
            {
                Id           = "O3",
                DisplayName  = "ThreshRed",
                Type         = new ThresholdOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",  Source = new SourceRef("O2", "Red") },
                    new ParameterValue { Name = "Thresh", Value  = 128.0 }
                ],
                Dependencies = [new Dependency("D_O2_O3", "O2")]
            },
            new Operator
            {
                Id           = "O4",
                DisplayName  = "ThreshGreen",
                Type         = new ThresholdOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",  Source = new SourceRef("O2", "Green") },
                    new ParameterValue { Name = "Thresh", Value  = 128.0 }
                ],
                Dependencies = [new Dependency("D_O2_O4", "O2")]
            },
            new Operator
            {
                Id           = "O5",
                DisplayName  = "ThreshBlue",
                Type         = new ThresholdOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",  Source = new SourceRef("O2", "Blue") },
                    new ParameterValue { Name = "Thresh", Value  = 128.0 }
                ],
                Dependencies = [new Dependency("D_O2_O5", "O2")]
            },
            new Operator
            {
                Id           = "O6",
                DisplayName  = "SegRed",
                Type         = new ConnectedComponentsOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",            Source = new SourceRef("O3", "Image") },
                    new ParameterValue { Name = "OutputLabelImage", Value  = false }
                ],
                Dependencies = [new Dependency("D_O3_O6", "O3")]
            },
            new Operator
            {
                Id           = "O7",
                DisplayName  = "SegGreen",
                Type         = new ConnectedComponentsOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",            Source = new SourceRef("O4", "Image") },
                    new ParameterValue { Name = "OutputLabelImage", Value  = false }
                ],
                Dependencies = [new Dependency("D_O4_O7", "O4")]
            },
            new Operator
            {
                Id           = "O8",
                DisplayName  = "SegBlue",
                Type         = new ConnectedComponentsOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",            Source = new SourceRef("O5", "Image") },
                    new ParameterValue { Name = "OutputLabelImage", Value  = false }
                ],
                Dependencies = [new Dependency("D_O5_O8", "O5")]
            }
        ]);

        var executor = new FlowEx(flow);
        await executor.RunAllAsync();

        ConnectedComponentInfo[] Components(string id) =>
            (ConnectedComponentInfo[])((Dictionary<string, object?>)executor.IntermediateResults[id]!)["Components"]!;

        Assert.Equal(2, Components("O6").Length);
        Assert.Equal(3, Components("O7").Length);
        Assert.Equal(3, Components("O8").Length);
    }

    [Fact]
    public async Task RGBCircles_HsvGrayscale_CountsAllCircles()
    {
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
                DisplayName  = "ToGray",
                Type         = new ConvertToGrayscaleOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",  Source = new SourceRef("O1", "Image") },
                    new ParameterValue { Name = "Method", Value  = "HsvValue" }
                ],
                Dependencies = [new Dependency("D_O1_O2", "O1")]
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
                Dependencies = [new Dependency("D_O2_O3", "O2")]
            },
            new Operator
            {
                Id           = "O4",
                DisplayName  = "Components",
                Type         = new ConnectedComponentsOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",            Source = new SourceRef("O3", "Image") },
                    new ParameterValue { Name = "OutputLabelImage", Value  = false }
                ],
                Dependencies = [new Dependency("D_O3_O4", "O3")]
            }
        ]);

        var executor = new FlowEx(flow);
        await executor.RunAllAsync();

        var result     = (Dictionary<string, object?>)executor.IntermediateResults["O4"]!;
        var components = (ConnectedComponentInfo[])result["Components"]!;

        Assert.Equal(8, components.Length);
    }
}
