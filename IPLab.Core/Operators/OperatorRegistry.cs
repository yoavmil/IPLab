using IPLab.Core.Interfaces;

namespace IPLab.Core.Operators;

public class OperatorRegistry
{
    private readonly Dictionary<string, IOperatorType> _operators = new();

    public void Register(IOperatorType op) => _operators[op.TypeName] = op;

    public IOperatorType Resolve(string typeName) =>
        _operators.TryGetValue(typeName, out var op) ? op
            : throw new KeyNotFoundException($"Operator type '{typeName}' is not registered.");

    // Scans the IPLab.Core assembly and registers every concrete IOperatorType.
    // Requires a public parameterless constructor on each operator class.
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
