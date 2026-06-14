using IPLab.Core.Models;

namespace IPLab.Core.Interfaces;

/// <summary>An operator instance in a flow: its type, parameters, dependencies, and identity.</summary>
public interface IOperator
{
    /// <summary>Unique identifier within the flow (e.g. "O1").</summary>
    string Id { get; }
    /// <summary>User-visible name shown in the flow editor.</summary>
    string DisplayName { get; }
    /// <summary>The operator type that provides the parameter schema and execution logic.</summary>
    IOperatorType Type { get; }
    /// <summary>Current parameter values. Each entry is either a direct value or a wired source reference.</summary>
    IReadOnlyList<ParameterValue> Parameters { get; }
    /// <summary>Upstream operators this operator depends on, used for topological execution ordering.</summary>
    IReadOnlyList<Dependency> Dependencies { get; }
}
