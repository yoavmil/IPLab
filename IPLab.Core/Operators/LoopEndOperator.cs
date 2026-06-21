using IPLab.Core.Interfaces;
using IPLab.Core.Models;

namespace IPLab.Core.Operators;

/// <summary>Marks the end of a loop body and passes up to four values from the current iteration to FlowEx for accumulation into output arrays.</summary>
public class LoopEndOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName => "LoopEnd";
    /// <inheritdoc/>
    public string Category => "Flow";
    /// <inheritdoc/>
    public string Icon => "loop-end";

    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Index", Label = "Index", ConnectableType = typeof(int) },
        new() { Name = "In1",   Label = "In 1",  ConnectableType = typeof(object) },
        new() { Name = "In2",   Label = "In 2",  ConnectableType = typeof(object) },
        new() { Name = "In3",   Label = "In 3",  ConnectableType = typeof(object) },
        new() { Name = "In4",   Label = "In 4",  ConnectableType = typeof(object) },
    ];

    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "Out1", DataType = typeof(object) },
        new() { Name = "Out2", DataType = typeof(object) },
        new() { Name = "Out3", DataType = typeof(object) },
        new() { Name = "Out4", DataType = typeof(object) },
    ];

    /// <inheritdoc/>
    public object? Execute(IReadOnlyDictionary<string, object?> parameters) =>
        new Dictionary<string, object?>
        {
            ["Out1"] = parameters.GetValueOrDefault("In1"),
            ["Out2"] = parameters.GetValueOrDefault("In2"),
            ["Out3"] = parameters.GetValueOrDefault("In3"),
            ["Out4"] = parameters.GetValueOrDefault("In4"),
        };
}
