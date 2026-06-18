using IPLab.Core.Interfaces;
using IPLab.Core.Models;

namespace IPLab.Core.Operators;

/// <summary>Marks the end of a loop body and accumulates up to four values by iteration index.</summary>
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
        new() { Name = "Reset", Label = "Reset", Type = ParameterType.Bool, DefaultValue = false, IsHidden = true },
        new() { Name = "Count", Label = "Count", Type = ParameterType.Int,  DefaultValue = 1,     IsHidden = true, Min = 0 },
    ];

    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "Out1", DataType = typeof(object) },
        new() { Name = "Out2", DataType = typeof(object) },
        new() { Name = "Out3", DataType = typeof(object) },
        new() { Name = "Out4", DataType = typeof(object) },
    ];

    private object?[][]? _outputs;

    /// <inheritdoc/>
    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        bool reset = Convert.ToBoolean(parameters.GetValueOrDefault("Reset") ?? false);
        int count = Convert.ToInt32(parameters.GetValueOrDefault("Count") ?? 1);

        if (reset)
        {
            if (count < 0)
                throw new InvalidOperationException("LoopEnd Count cannot be negative.");

            _outputs = CreateOutputs(count);
            return Result();
        }

        int index = Convert.ToInt32(parameters.GetValueOrDefault("Index") ?? 0);
        _outputs ??= CreateOutputs(Math.Max(1, count));

        int outputCount = _outputs[0].Length;
        if (index < 0 || index >= outputCount)
            throw new InvalidOperationException($"LoopEnd Index {index} is outside the output range 0..{outputCount - 1}.");

        _outputs[0][index] = parameters.GetValueOrDefault("In1");
        _outputs[1][index] = parameters.GetValueOrDefault("In2");
        _outputs[2][index] = parameters.GetValueOrDefault("In3");
        _outputs[3][index] = parameters.GetValueOrDefault("In4");

        return Result();
    }

    private static object?[][] CreateOutputs(int count) =>
    [
        new object?[count],
        new object?[count],
        new object?[count],
        new object?[count],
    ];

    private Dictionary<string, object?> Result()
    {
        var outputs = _outputs ?? CreateOutputs(1);
        return new Dictionary<string, object?>
        {
            ["Out1"] = outputs[0],
            ["Out2"] = outputs[1],
            ["Out3"] = outputs[2],
            ["Out4"] = outputs[3],
        };
    }
}
