using IPLab.Core.Interfaces;
using IPLab.Core.Models;

namespace IPLab.Core.Runtime;

public class FlowEx : IFlowEx
{
    private readonly Dictionary<string, object?> _results = [];
    private readonly Dictionary<string, OperatorStatus> _statuses;

    public IFlowDef Flow { get; }

    public FlowEx(IFlowDef flow)
    {
        Flow = flow;
        _statuses = flow.Operators.ToDictionary(o => o.Id, _ => OperatorStatus.NotRun);
    }

    public IReadOnlyDictionary<string, object?> IntermediateResults => _results;
    public IReadOnlyDictionary<string, OperatorStatus> Statuses => _statuses;

    public async Task RunAllAsync()
    {
        var sorted = TopologicalSort().ToList();
        foreach (var op in sorted)
            await RunOperatorAsync(op);
    }

    public async Task RunSingleAsync(string operatorId)
    {
        var op = Flow.Operators.First(o => o.Id == operatorId);
        await RunOperatorAsync(op);
    }

    public void ClearResults()
    {
        _results.Clear();
        foreach (var key in _statuses.Keys.ToList())
            _statuses[key] = OperatorStatus.NotRun;
    }

    private async Task RunOperatorAsync(IOperator op)
    {
        _statuses[op.Id] = OperatorStatus.Running;
        try
        {
            var resolved = ResolveParameters(op);
            _results[op.Id] = await Task.Run(() => op.Type.Execute(resolved));
            _statuses[op.Id] = OperatorStatus.Success;
        }
        catch
        {
            _statuses[op.Id] = OperatorStatus.Failed;
            throw;
        }
    }

    private IReadOnlyDictionary<string, object?> ResolveParameters(IOperator op)
    {
        var resolved = new Dictionary<string, object?>();
        foreach (var param in op.Parameters)
        {
            if (param.Source is { } source)
            {
                var raw = _results[source.OperatorId];
                var sourceOp = Flow.Operators.First(o => o.Id == source.OperatorId);
                resolved[param.Name] = sourceOp.Type.OutputPorts.Count == 1
                    ? raw
                    : ((IReadOnlyDictionary<string, object?>)raw!)[source.Port];
            }
            else
            {
                resolved[param.Name] = param.Value;
            }
        }
        return resolved;
    }

    private IEnumerable<IOperator> TopologicalSort()
    {
        var inDegree = Flow.Operators.ToDictionary(o => o.Id, _ => 0);
        var graph = Flow.Operators.ToDictionary(o => o.Id, _ => new List<string>());

        foreach (var op in Flow.Operators)
            foreach (var dep in op.Dependencies)
            {
                graph[dep.OperatorId].Add(op.Id);
                inDegree[op.Id]++;
            }

        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var byId = Flow.Operators.ToDictionary(o => o.Id);
        var result = new List<IOperator>();

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            result.Add(byId[id]);
            foreach (var next in graph[id])
                if (--inDegree[next] == 0)
                    queue.Enqueue(next);
        }

        return result;
    }
}
