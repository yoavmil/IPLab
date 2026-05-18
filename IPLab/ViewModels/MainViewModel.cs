using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;

namespace IPLab.ViewModels;

public class MainViewModel : ViewModelBase
{
    public FlowViewModel Flow   { get; }
    public string        Status { get; } = "Ready";

    public MainViewModel()
    {
        Flow = new FlowViewModel(BuildSampleFlow());
    }

    private static FlowDef BuildSampleFlow() => new(
    [
        new Operator
        {
            Id           = "O1",
            DisplayName  = "Load Image",
            Type         = new LoadImageOperator(),
            Parameters   = [new ParameterValue { Name = "FilePath", Value = "sample.png" }],
            Dependencies = []
        },
        new Operator
        {
            Id           = "O2",
            DisplayName  = "To Grayscale",
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
            DisplayName  = "Threshold",
            Type         = new ThresholdOperator(),
            Parameters   =
            [
                new ParameterValue { Name = "Image",  Source = new SourceRef("O2", "Image") },
                new ParameterValue { Name = "Thresh", Value  = 128.0 },
                new ParameterValue { Name = "MaxVal", Value  = 255.0 }
            ],
            Dependencies = [new Dependency("D2", "O2")]
        },
        new Operator
        {
            Id           = "O4",
            DisplayName  = "Detect Circles",
            Type         = new DetectCirclesOperator(),
            Parameters   =
            [
                new ParameterValue { Name = "Image",     Source = new SourceRef("O3", "Image") },
                new ParameterValue { Name = "MinDist",   Value  = 50.0  },
                new ParameterValue { Name = "Param1",    Value  = 150.0 },
                new ParameterValue { Name = "Param2",    Value  = 10.0  },
                new ParameterValue { Name = "MinRadius", Value  = 10    },
                new ParameterValue { Name = "MaxRadius", Value  = 100   }
            ],
            Dependencies = [new Dependency("D3", "O3")]
        }
    ]);
}
