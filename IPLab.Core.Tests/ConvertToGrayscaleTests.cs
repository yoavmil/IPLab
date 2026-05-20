using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class ConvertToGrayscaleTests
{
    // Injects a single-channel Mat without touching the file system.
    private class GrayscaleSourceOperator : IOperatorType
    {
        public string TypeName  => "GrayscaleSource";
        public string Category  => "Test";
        public string Icon      => "";
        public IReadOnlyList<ParameterDescriptor> ParameterSchema => [];
        public IReadOnlyList<string> OutputPorts => ["Image"];
        public object? Execute(IReadOnlyDictionary<string, object?> _)
            => new Mat(100, 100, MatType.CV_8UC1, new Scalar(128));
    }

    [Theory]
    [InlineData("Luminance")]
    [InlineData("HsvValue")]
    public async Task ConvertToGrayscale_GrayscaleInput_FailsWithErrorStatus(string method)
    {
        var flow = new FlowDef(
        [
            new Operator
            {
                Id = "O1", DisplayName = "Source", Type = new GrayscaleSourceOperator(),
                Parameters = [], Dependencies = []
            },
            new Operator
            {
                Id = "O2", DisplayName = "Convert", Type = new ConvertToGrayscaleOperator(),
                Parameters =
                [
                    new ParameterValue { Name = "Image",  Source = new SourceRef("O1", "Image") },
                    new ParameterValue { Name = "Method", Value  = method }
                ],
                Dependencies = [new Dependency("D1", "O1")]
            }
        ]);

        var executor = new FlowEx(flow);

        await executor.RunAllAsync();
        Assert.Equal(OperatorStatus.Failed, executor.Statuses["O2"]);
    }
}
