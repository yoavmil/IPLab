using IPLab.Core.Interfaces;
using IPLab.Core.Models;

namespace IPLab.Core.Runtime;

public class Operator : IOperator
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required IOperatorType Type { get; init; }
    public required IReadOnlyList<ParameterValue> Parameters { get; init; }
    public required IReadOnlyList<Dependency> Dependencies { get; init; }
}
