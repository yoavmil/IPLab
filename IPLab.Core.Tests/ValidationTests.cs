using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;

namespace IPLab.Core.Tests;

public class ValidationTests
{
    [Fact]
    public void Validate_CircularDependency_ReturnsInvalid()
    {
        // O1 → O2 → O3 → O1
        var flow = new FlowDef(
        [
            new Operator { Id = "O1", DisplayName = "A", Type = new LoadImageOperator(),        Parameters = [], Dependencies = [new Dependency("D3", "O3")] },
            new Operator { Id = "O2", DisplayName = "B", Type = new ConvertToGrayscaleOperator(), Parameters = [], Dependencies = [new Dependency("D1", "O1")] },
            new Operator { Id = "O3", DisplayName = "C", Type = new ThresholdOperator(),         Parameters = [], Dependencies = [new Dependency("D2", "O2")] },
        ]);

        var result = flow.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("circular", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WiredParameterMissingDependency_ReturnsInvalid()
    {
        // O2 wires its Image parameter from O1, but O1 is not listed in O2's Dependencies.
        var flow = new FlowDef(
        [
            new Operator { Id = "O1", DisplayName = "Input",     Type = new LoadImageOperator(),          Parameters = [], Dependencies = [] },
            new Operator { Id = "O2", DisplayName = "Grayscale", Type = new ConvertToGrayscaleOperator(), Dependencies = [],
                Parameters = [new ParameterValue { Name = "Image", Source = new SourceRef("O1", "Image") }] },
        ]);

        var result = flow.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("O2") && e.Contains("O1"));
    }

    [Fact]
    public void Validate_WiredPortDoesNotExist_ReturnsInvalid()
    {
        // O2 references port "NonExistent" which LoadImage does not declare.
        var flow = new FlowDef(
        [
            new Operator { Id = "O1", DisplayName = "Load",      Type = new LoadImageOperator(),          Parameters = [], Dependencies = [] },
            new Operator { Id = "O2", DisplayName = "Grayscale", Type = new ConvertToGrayscaleOperator(), Dependencies = [new Dependency("D1", "O1")],
                Parameters = [new ParameterValue { Name = "Image", Source = new SourceRef("O1", "NonExistent") }] },
        ]);

        var result = flow.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("NonExistent"));
    }

    [Fact]
    public void Validate_TypeIncompatibleWiring_ReturnsInvalid()
    {
        // DetectCircles outputs CircleSegment[] on "Circles"; GaussianBlur.Image expects Mat.
        // Wiring the Circles port into the Image (Mat) parameter is a declared type mismatch.
        var flow = new FlowDef(
        [
            new Operator { Id = "O1", DisplayName = "Load",    Type = new LoadImageOperator(),     Parameters = [], Dependencies = [] },
            new Operator { Id = "O2", DisplayName = "Circles", Type = new DetectCirclesOperator(), Dependencies = [new Dependency("D1", "O1")],
                Parameters = [new ParameterValue { Name = "Image", Source = new SourceRef("O1", "Image") }] },
            new Operator { Id = "O3", DisplayName = "Blur",    Type = new GaussianBlurOperator(),  Dependencies = [new Dependency("D2", "O2")],
                Parameters = [new ParameterValue { Name = "Image", Source = new SourceRef("O2", "Circles") }] },
        ]);

        var result = flow.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Image") && e.Contains("Circles"));
    }

    [Fact]
    public void Validate_CompatibleWiring_ReturnsValid()
    {
        // LoadImage → GaussianBlur wiring Image (Mat) into Image (Object) — valid.
        var flow = new FlowDef(
        [
            new Operator { Id = "O1", DisplayName = "Load", Type = new LoadImageOperator(),    Parameters = [], Dependencies = [] },
            new Operator { Id = "O2", DisplayName = "Blur", Type = new GaussianBlurOperator(), Dependencies = [new Dependency("D1", "O1")],
                Parameters = [new ParameterValue { Name = "Image", Source = new SourceRef("O1", "Image") }] },
        ]);

        var result = flow.Validate();

        Assert.True(result.IsValid);
    }
}
