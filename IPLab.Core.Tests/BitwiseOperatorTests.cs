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
}
