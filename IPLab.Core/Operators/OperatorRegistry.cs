using IPLab.Core.Interfaces;

namespace IPLab.Core.Operators;

/// <summary>Registry of all available operator types. Supports lookup by type name and enumeration in UI order.</summary>
public class OperatorRegistry
{
    private readonly Dictionary<string, IOperatorType> _operators = new();

    /// <summary>Registers an operator type, replacing any existing entry with the same <see cref="IOperatorType.TypeName"/>.</summary>
    public void Register(IOperatorType op) => _operators[op.TypeName] = op;

    /// <summary>Returns the operator type with the given type name, or throws <see cref="KeyNotFoundException"/> if not found.</summary>
    public IOperatorType Resolve(string typeName) =>
        _operators.TryGetValue(typeName, out var op) ? op
            : throw new KeyNotFoundException($"Operator type '{typeName}' is not registered.");

    /// <summary>Returns all registered operator types sorted by category then type name.</summary>
    public IReadOnlyList<IOperatorType> GetAll() =>
        _operators.Values.OrderBy(op => op.Category).ThenBy(op => op.TypeName).ToList();

    /// <summary>
    /// Scans the IPLab.Core assembly and registers every concrete <see cref="IOperatorType"/> implementation.
    /// Requires a public parameterless constructor on each operator class.
    /// </summary>
    public static OperatorRegistry CreateDefault()
    {
        var r = new OperatorRegistry();
        foreach (var type in typeof(IOperatorType).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IOperatorType).IsAssignableFrom(t)))
        {
            if (Activator.CreateInstance(type) is IOperatorType op)
                r.Register(op);
        }
        return r;
    }
}
