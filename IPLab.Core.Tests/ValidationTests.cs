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
}
