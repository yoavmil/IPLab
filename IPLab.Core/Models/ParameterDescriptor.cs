namespace IPLab.Core.Models;

/// <summary>Describes one parameter of an operator type: its UI control, wiring contract, and value constraints.</summary>
public record ParameterDescriptor
{
    /// <summary>Stable programmatic key used in code, JSON, and <see cref="ParameterValue"/> matching. Never rename after release.</summary>
    public required string Name { get; init; }
    /// <summary>Human-readable label shown in the parameter panel. Can be changed or localized freely.</summary>
    public required string Label { get; init; }
    /// <summary>Controls which UI widget is rendered. <see cref="ParameterType.Object"/> (default) renders no widget — wire-only socket.</summary>
    public ParameterType Type { get; init; } = ParameterType.Object;
    /// <summary>CLR type this parameter accepts when wired to an output port. <see langword="null"/> means not connectable; <c>typeof(object)</c> accepts any port type.</summary>
    public Type? ConnectableType { get; init; }
    /// <summary>When <see langword="true"/>, the parameter is hidden from the UI.</summary>
    public bool IsHidden { get; init; }
    /// <summary>Initial value used when no <see cref="ParameterValue"/> has been set.</summary>
    public object? DefaultValue { get; init; }
    /// <summary>Inclusive lower bound enforced by numeric UI controls.</summary>
    public object? Min { get; init; }
    /// <summary>Inclusive upper bound enforced by numeric UI controls.</summary>
    public object? Max { get; init; }
    /// <summary>Available choices shown in a drop-down when <see cref="Type"/> is <see cref="ParameterType.Enum"/>.</summary>
    public string[]? Options { get; init; }
    /// <summary>Name of a sibling parameter that controls this parameter's visibility. When set, this parameter is visible only when the sibling's value is in <see cref="ShowWhenValues"/>.</summary>
    public string? ShowWhenParam { get; init; }
    /// <summary>Values of <see cref="ShowWhenParam"/> for which this parameter is visible.</summary>
    public string[]? ShowWhenValues { get; init; }
}
