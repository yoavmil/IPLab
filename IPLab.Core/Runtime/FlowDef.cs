using System.Collections.ObjectModel;
using IPLab.Core.Interfaces;
using IPLab.Core.Models;

namespace IPLab.Core.Runtime;

public class FlowDef : IFlowDef
{
    private readonly ObservableCollection<IOperator> _operators;
    private readonly ReadOnlyObservableCollection<IOperator> _readOnlyOperators;

    public FlowDef(IEnumerable<IOperator>? operators = null)
    {
        _operators = new ObservableCollection<IOperator>(operators ?? []);
        _readOnlyOperators = new ReadOnlyObservableCollection<IOperator>(_operators);
    }

    // IReadOnlyList<T> for the interface contract; the runtime type is
    // ReadOnlyObservableCollection so WPF bindings still receive CollectionChanged events.
    public IReadOnlyList<IOperator> Operators => _readOnlyOperators;

    public void AddOperator(IOperator op) => _operators.Add(op);

    public void RemoveOperator(string operatorId)
    {
        var op = _operators.FirstOrDefault(o => o.Id == operatorId);
        if (op is not null) _operators.Remove(op);
    }

    public ValidationResult Validate()
    {
        var errors = new List<string>();
        var ids = new HashSet<string>();

        foreach (var op in Operators)
            if (!ids.Add(op.Id))
                errors.Add($"Duplicate operator ID: {op.Id}");

        foreach (var op in Operators)
            foreach (var dep in op.Dependencies)
                if (!ids.Contains(dep.OperatorId))
                    errors.Add($"Operator '{op.Id}' references unknown dependency '{dep.OperatorId}'.");

        foreach (var op in Operators)
        {
            var declaredDeps = op.Dependencies.Select(d => d.OperatorId).ToHashSet();
            foreach (var pv in op.Parameters)
                if (pv.Source is { } src && !declaredDeps.Contains(src.OperatorId))
                    errors.Add($"Operator '{op.Id}' wires parameter '{pv.Name}' from '{src.OperatorId}' but that operator is not listed in its dependencies.");
        }

        // Check that wired port names exist and their types are compatible.
        var opById = Operators.ToDictionary(o => o.Id);
        foreach (var op in Operators)
        {
            foreach (var pv in op.Parameters)
            {
                if (pv.Source is not { } src) continue;
                if (!opById.TryGetValue(src.OperatorId, out var srcOp)) continue;

                var portDesc = srcOp.Type.OutputPorts.FirstOrDefault(p => p.Name == src.Port);
                if (portDesc is null)
                {
                    errors.Add($"Operator '{op.Id}' wires parameter '{pv.Name}' from port '{src.Port}' on '{src.OperatorId}', but that port does not exist.");
                    continue;
                }

                var paramDesc = op.Type.ParameterSchema.FirstOrDefault(p => p.Name == pv.Name);
                if (paramDesc is not null && !PortTypeCompat.IsCompatible(paramDesc.ConnectableType, portDesc.DataType))
                    errors.Add($"Operator '{op.Id}': parameter '{pv.Name}' ({paramDesc.Type}) is not compatible with port '{src.Port}' on '{src.OperatorId}' ({portDesc.DataType.Name}).");
            }
        }

        if (HasCycle())
            errors.Add("Flow contains circular dependencies.");

        return errors.Count == 0 ? ValidationResult.Ok() : ValidationResult.Fail([.. errors]);
    }

    private bool HasCycle()
    {
        var inDegree = Operators.ToDictionary(o => o.Id, _ => 0);
        var graph = Operators.ToDictionary(o => o.Id, _ => new List<string>());

        foreach (var op in Operators)
            foreach (var dep in op.Dependencies)
                if (graph.ContainsKey(dep.OperatorId))
                {
                    graph[dep.OperatorId].Add(op.Id);
                    inDegree[op.Id]++;
                }

        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        int processed = 0;

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            processed++;
            foreach (var next in graph[id])
                if (--inDegree[next] == 0)
                    queue.Enqueue(next);
        }

        return processed != Operators.Count;
    }
}
