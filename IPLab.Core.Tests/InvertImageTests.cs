using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class InvertImageTests
{
    private class GraySourceOperator(byte value) : IOperatorType
    {
        public string TypeName  => "GraySource";
        public string Category  => "Test";
        public string Icon      => "";
        public IReadOnlyList<ParameterDescriptor> ParameterSchema => [];
        public IReadOnlyList<OutputPortDescriptor> OutputPorts => [new() { Name = "Image", DataType = typeof(Mat) }];
        public object? Execute(IReadOnlyDictionary<string, object?> _)
            => new Mat(4, 4, MatType.CV_8UC1, new Scalar(value));
    }

    private class BgrSourceOperator(byte b, byte g, byte r) : IOperatorType
    {
        public string TypeName  => "BgrSource";
        public string Category  => "Test";
        public string Icon      => "";
        public IReadOnlyList<ParameterDescriptor> ParameterSchema => [];
        public IReadOnlyList<OutputPortDescriptor> OutputPorts => [new() { Name = "Image", DataType = typeof(Mat) }];
        public object? Execute(IReadOnlyDictionary<string, object?> _)
            => new Mat(4, 4, MatType.CV_8UC3, new Scalar(b, g, r));
    }

    private static FlowDef BuildFlow(IOperatorType source) => new(
    [
        new Operator
        {
            Id = "O1", DisplayName = "Source", Type = source,
            Parameters = [], Dependencies = []
        },
        new Operator
        {
            Id = "O2", DisplayName = "Invert", Type = new InvertImageOperator(),
            Parameters = [new ParameterValue { Name = "Image", Source = new SourceRef("O1", "Image") }],
            Dependencies = [new Dependency("D1", "O1")]
        }
    ]);

    [Fact]
    public async Task Invert_Grayscale_PixelsAreComplemented()
    {
        var executor = new FlowEx(BuildFlow(new GraySourceOperator(50)));
        await executor.RunAllAsync();

        Assert.Equal(OperatorStatus.Success, executor.Statuses["O2"]);
        using var result = (Mat)((Dictionary<string, object?>)executor.IntermediateResults["O2"]!)["Image"]!;
        Assert.Equal(MatType.CV_8UC1, result.Type());
        Assert.Equal(205, result.At<byte>(0, 0)); // 255 - 50
    }

    [Fact]
    public async Task Invert_BGR_EachChannelIsComplemented()
    {
        var executor = new FlowEx(BuildFlow(new BgrSourceOperator(50, 100, 200)));
        await executor.RunAllAsync();

        Assert.Equal(OperatorStatus.Success, executor.Statuses["O2"]);
        using var result = (Mat)((Dictionary<string, object?>)executor.IntermediateResults["O2"]!)["Image"]!;
        Assert.Equal(MatType.CV_8UC3, result.Type());
        var pixel = result.At<Vec3b>(0, 0);
        Assert.Equal(205, pixel.Item0); // 255 - 50  (B)
        Assert.Equal(155, pixel.Item1); // 255 - 100 (G)
        Assert.Equal(55,  pixel.Item2); // 255 - 200 (R)
    }

    private class CloneSourceOp(Mat mat) : IOperatorType
    {
        public string TypeName  => "CloneSource";
        public string Category  => "Test";
        public string Icon      => "";
        public IReadOnlyList<ParameterDescriptor> ParameterSchema => [];
        public IReadOnlyList<OutputPortDescriptor> OutputPorts => [new() { Name = "Image", DataType = typeof(Mat) }];
        public object? Execute(IReadOnlyDictionary<string, object?> _) => mat.Clone();
    }

    [Fact]
    public async Task Invert_Twice_RestoresOriginalValues()
    {
        var executor1 = new FlowEx(BuildFlow(new GraySourceOperator(123)));
        await executor1.RunAllAsync();
        using var once = ((Mat)((Dictionary<string, object?>)executor1.IntermediateResults["O2"]!)["Image"]!).Clone();

        var executor2 = new FlowEx(BuildFlow(new CloneSourceOp(once)));
        await executor2.RunAllAsync();
        using var twice = (Mat)((Dictionary<string, object?>)executor2.IntermediateResults["O2"]!)["Image"]!;
        Assert.Equal(123, twice.At<byte>(0, 0));
    }
}
