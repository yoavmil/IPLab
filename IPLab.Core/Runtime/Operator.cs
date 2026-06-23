using IPLab.Core.Interfaces;
using IPLab.Core.Models;

namespace IPLab.Core.Runtime;

/// <summary>
/// Concrete operator instance constructed by the serializer or flow builder.
/// The identity fields <see cref="Id"/>, <see cref="Type"/>, and <see cref="Dependencies"/> are fixed after construction.
/// <see cref="ParameterValue.Value"/> entries inside <see cref="Parameters"/> are mutable and may be changed
/// before passing the flow to <see cref="Runtime.FlowEx"/> to drive execution programmatically.
/// </summary>
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
