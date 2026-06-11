namespace IPLab.Core.Models;

/// <summary>Describes one output port of an operator type.</summary>
public record OutputPortDescriptor
{
    /// <summary>Port name. For multi-port operators, must match the key in the result dictionary returned by <c>Execute</c>.</summary>
    public required string Name           { get; init; }
    /// <summary>CLR type of the value this port emits. Use <c>typeof(object)</c> for dynamic or script ports.</summary>
    public required Type   DataType       { get; init; }
    /// <summary>When <see langword="true"/>, the inspector renders this port's value as an image preview. Set on at most one port per operator.</summary>
    public          bool   IsDisplayImage { get; init; } = false;
}
