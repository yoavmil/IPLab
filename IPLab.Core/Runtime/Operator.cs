using IPLab.Core.Interfaces;
using IPLab.Core.Models;

namespace IPLab.Core.Runtime;

/// <summary>Concrete, immutable operator instance. Constructed by the serializer or flow builder.</summary>
public class Operator : IOperator
{
    /// <inheritdoc/>
    public required string Id { get; init; }
    /// <inheritdoc/>
    public required string DisplayName { get; init; }
    /// <inheritdoc/>
    public required IOperatorType Type { get; init; }
    /// <inheritdoc/>
    public required IReadOnlyList<ParameterValue> Parameters { get; init; }
    /// <inheritdoc/>
    public required IReadOnlyList<Dependency> Dependencies { get; init; }
}
