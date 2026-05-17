using IPLab.Core.Models;

namespace IPLab.Core.Interfaces;

public interface IOperator
{
    string Id { get; }
    string DisplayName { get; }
    IOperatorType Type { get; }
    IReadOnlyList<ParameterValue> Parameters { get; }
    IReadOnlyList<Dependency> Dependencies { get; }
}
