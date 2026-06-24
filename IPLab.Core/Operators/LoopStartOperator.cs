using System.Collections;
using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>Marks the start of a discrete loop body and outputs the selected iteration index and source count.</summary>
/// <seealso href="https://github.com/yoavmil/IPLab/blob/master/docs/OPERATORS.md#loopstart">Operator reference</seealso>
public class LoopStartOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName => "LoopStart";
    /// <inheritdoc/>
    public string Category => "Flow";
    /// <inheritdoc/>
    public string Icon => "loop-start";

    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Source", Label = "Source", ConnectableType = typeof(object) },
        new() { Name = "Index",  Label = "Index",  Type = ParameterType.Int, DefaultValue = 0, Min = 0 },
        new() { Name = "Mode",   Label = "Mode",   Type = ParameterType.Enum,
                DefaultValue = nameof(LoopMode.Serial),
                Options = [nameof(LoopMode.Discrete), nameof(LoopMode.Serial), nameof(LoopMode.Parallel)] },
    ];

    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "Index", DataType = typeof(int) },
        new() { Name = "Count", DataType = typeof(int) },
    ];

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var source = parameters.GetValueOrDefault("Source")
            ?? throw new InvalidOperationException("LoopStart requires a Source value.");
        int index = Convert.ToInt32(parameters.GetValueOrDefault("Index") ?? 0);
        int count = GetCount(source);

        if (count <= 0)
            throw new InvalidOperationException("LoopStart Source is empty.");

        return new Dictionary<string, object?>
        {
            ["Index"] = index,
            ["Count"] = count,
        };
    }

    private static int GetCount(object source) => source switch
    {
        Mat mat => mat.Rows,
        string => throw new InvalidOperationException("LoopStart Source must be a Mat or non-string IEnumerable."),
        ICollection collection => collection.Count,
        IEnumerable enumerable => enumerable.Cast<object?>().Count(),
        _ => throw new InvalidOperationException("LoopStart Source must be a Mat or non-string IEnumerable."),
    };
}
