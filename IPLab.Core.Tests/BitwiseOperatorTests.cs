using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class BitwiseOperatorTests
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

    private static FlowDef BuildFlow(byte a, byte b, string operation) => new(
    [
        new Operator
        {
            Id = "O1", DisplayName = "SourceA", Type = new GraySourceOperator(a),
            Parameters = [], Dependencies = []
        },
        new Operator
        {
            Id = "O2", DisplayName = "SourceB", Type = new GraySourceOperator(b),
            Parameters = [], Dependencies = []
        },
        new Operator
        {
            Id = "O3", DisplayName = "Bitwise", Type = new BitwiseOperator(),
            Parameters =
            [
                new ParameterValue { Name = "ImageA",    Source = new SourceRef("O1", "Image") },
                new ParameterValue { Name = "ImageB",    Source = new SourceRef("O2", "Image") },
                new ParameterValue { Name = "Operation", Value  = operation },
            ],
            Dependencies = [new Dependency("D1", "O1"), new Dependency("D2", "O2")]
        }
    ]);

    private static async Task<byte> RunAndGetPixel(byte a, byte b, string operation)
    {
        var executor = new FlowEx(BuildFlow(a, b, operation));
        await executor.RunAllAsync();
        Assert.Equal(OperatorStatus.Success, executor.Statuses["O3"]);
        using var result = (Mat)executor.IntermediateResults["O3"]!;
        Assert.Equal(MatType.CV_8UC1, result.Type());
        return result.At<byte>(0, 0);
    }

    [Fact]
    public async Task And_ProducesCorrectPixels()
    {
        // 0b11001100 (204) & 0b10101010 (170) = 0b10001000 (136)
        Assert.Equal(136, await RunAndGetPixel(204, 170, "And"));
    }

    [Fact]
    public async Task Or_ProducesCorrectPixels()
    {
        // 0b11001100 (204) | 0b10101010 (170) = 0b11101110 (238)
        Assert.Equal(238, await RunAndGetPixel(204, 170, "Or"));
    }

    [Fact]
    public async Task Xor_ProducesCorrectPixels()
    {
        // 0b11001100 (204) ^ 0b10101010 (170) = 0b01100110 (102)
        Assert.Equal(102, await RunAndGetPixel(204, 170, "Xor"));
    }

    [Fact]
    public async Task And_SameImage_IsIdentity()
    {
        // x & x == x
        Assert.Equal(173, await RunAndGetPixel(173, 173, "And"));
    }

    [Fact]
    public async Task Xor_SameImage_IsZero()
    {
        // x ^ x == 0
        Assert.Equal(0, await RunAndGetPixel(173, 173, "Xor"));
    }

    [Fact]
    public async Task Or_WithZero_IsIdentity()
    {
        // x | 0 == x
        Assert.Equal(173, await RunAndGetPixel(173, 0, "Or"));
    }

    private static FlowDef BuildColorFlow(
        (byte b, byte g, byte r) a, (byte b, byte g, byte r) bv, string operation) => new(
    [
        new Operator
        {
            Id = "O1", DisplayName = "SourceA", Type = new BgrSourceOperator(a.b, a.g, a.r),
            Parameters = [], Dependencies = []
        },
        new Operator
        {
            Id = "O2", DisplayName = "SourceB", Type = new BgrSourceOperator(bv.b, bv.g, bv.r),
            Parameters = [], Dependencies = []
        },
        new Operator
        {
            Id = "O3", DisplayName = "Bitwise", Type = new BitwiseOperator(),
            Parameters =
            [
                new ParameterValue { Name = "ImageA",    Source = new SourceRef("O1", "Image") },
                new ParameterValue { Name = "ImageB",    Source = new SourceRef("O2", "Image") },
                new ParameterValue { Name = "Operation", Value  = operation },
            ],
            Dependencies = [new Dependency("D1", "O1"), new Dependency("D2", "O2")]
        }
    ]);

    private static async Task<Vec3b> RunAndGetBgrPixel(
        (byte b, byte g, byte r) a, (byte b, byte g, byte r) bv, string operation)
    {
        var executor = new FlowEx(BuildColorFlow(a, bv, operation));
        await executor.RunAllAsync();
        Assert.Equal(OperatorStatus.Success, executor.Statuses["O3"]);
        using var result = (Mat)executor.IntermediateResults["O3"]!;
        Assert.Equal(MatType.CV_8UC3, result.Type());
        return result.At<Vec3b>(0, 0);
    }

    [Fact]
    public async Task And_ThreeChannel_AppliedPerChannel()
    {
        // B: 0b11001100 (204) & 0b10101010 (170) = 136
        // G: 0b11110000 (240) & 0b00001111 (15)  = 0
        // R: 0b11111111 (255) & 0b10101010 (170) = 170
        var px = await RunAndGetBgrPixel((204, 240, 255), (170, 15, 170), "And");
        Assert.Equal(136, px.Item0);
        Assert.Equal(0,   px.Item1);
        Assert.Equal(170, px.Item2);
    }

    [Fact]
    public async Task Or_ThreeChannel_AppliedPerChannel()
    {
        // B: 204 | 170 = 238,  G: 240 | 15 = 255,  R: 0 | 170 = 170
        var px = await RunAndGetBgrPixel((204, 240, 0), (170, 15, 170), "Or");
        Assert.Equal(238, px.Item0);
        Assert.Equal(255, px.Item1);
        Assert.Equal(170, px.Item2);
    }

    [Fact]
    public async Task Xor_ThreeChannel_AppliedPerChannel()
    {
        // B: 204 ^ 170 = 102,  G: 240 ^ 15 = 255,  R: 255 ^ 255 = 0
        var px = await RunAndGetBgrPixel((204, 240, 255), (170, 15, 255), "Xor");
        Assert.Equal(102, px.Item0);
        Assert.Equal(255, px.Item1);
        Assert.Equal(0,   px.Item2);
    }

    [Fact]
    public void Execute_SizeMismatch_ThrowsClearError()
    {
        using var a = new Mat(4, 4, MatType.CV_8UC1, new Scalar(0));
        using var b = new Mat(8, 8, MatType.CV_8UC1, new Scalar(0));
        var op = new BitwiseOperator();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            op.Execute(new Dictionary<string, object?> { ["ImageA"] = a, ["ImageB"] = b, ["Operation"] = "And" }));
        Assert.Contains("size", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_ChannelCountMismatch_ThrowsClearError()
    {
        using var a = new Mat(4, 4, MatType.CV_8UC1, new Scalar(0));
        using var b = new Mat(4, 4, MatType.CV_8UC3, new Scalar(0));
        var op = new BitwiseOperator();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            op.Execute(new Dictionary<string, object?> { ["ImageA"] = a, ["ImageB"] = b, ["Operation"] = "Or" }));
        Assert.Contains("channel", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_UnsupportedChannelCount_ThrowsClearError()
    {
        using var a = new Mat(4, 4, MatType.CV_8UC4, new Scalar(0));
        using var b = new Mat(4, 4, MatType.CV_8UC4, new Scalar(0));
        var op = new BitwiseOperator();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            op.Execute(new Dictionary<string, object?> { ["ImageA"] = a, ["ImageB"] = b, ["Operation"] = "And" }));
        Assert.Contains("Unsupported channel count", ex.Message);
    }
}
