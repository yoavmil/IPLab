namespace IPLab.Core.Models;

/// <summary>A reference to an upstream operator's output port, used as a wired parameter value.</summary>
/// <param name="OperatorId">ID of the upstream operator.</param>
/// <param name="Port">Name of the output port on that operator.</param>
public record SourceRef(string OperatorId, string Port);

/// <summary>Runtime value of a single parameter — either a direct value or a wired source reference.</summary>
public record ParameterValue
{
    /// <summary>Matches the corresponding <see cref="ParameterDescriptor.Name"/>.</summary>
    public required string Name { get; init; }
    /// <summary>
    /// Direct value used at runtime when <see cref="Source"/> is <see langword="null"/>.
    /// Mutable after deserialization; assigning here before passing <c>flow.Def</c> to <see cref="Runtime.FlowEx"/>
    /// is the primary way to drive a flow programmatically.
    /// </summary>
    public object? Value { get; set; }
    /// <summary>Upstream port to pull the value from at execution time. When set, <see cref="Value"/> is ignored.</summary>
    public SourceRef? Source { get; init; }
}
