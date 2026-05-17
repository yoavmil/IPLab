using System.Collections.Concurrent;
using IPLab.Core.Interfaces;
using IPLab.Core.Models;

namespace IPLab.Core.Runtime;

public class FlowEx : IFlowEx
{
    private readonly ConcurrentDictionary<string, object?> _results = new();
    private readonly ConcurrentDictionary<string, OperatorStatus> _statuses;

    public IFlowDef Flow { get; }

    public FlowEx(IFlowDef flow)
    {
        Flow = flow;
        _statuses = new ConcurrentDictionary<string, OperatorStatus>(
            flow.Operators.Select(o => KeyValuePair.Create(o.Id, OperatorStatus.NotRun)));
    }

    public IReadOnlyDictionary<string, object?> IntermediateResults => _results;
    public IReadOnlyDictionary<string, OperatorStatus> Statuses => _statuses;

    public async Task RunAllAsync()
    {
        var levels = TopologicalLevels();

		foreach (var level in levels)
            await Task.WhenAll(level.Select(RunOperatorAsync));
    }

    public async Task RunSingleAsync(string operatorId)
    {
        var op = Flow.Operators.First(o => o.Id == operatorId);
        await RunOperatorAsync(op);
    }

    public void ClearResults()
    {
        _results.Clear();
        foreach (var key in _statuses.Keys)
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

    // Groups operators into dependency levels. All operators within a level are
    // independent of each other and can run in parallel.
    private IEnumerable<IReadOnlyList<IOperator>> TopologicalLevels()
    {
        var inDegree = Flow.Operators.ToDictionary(o => o.Id, _ => 0);
        var graph    = Flow.Operators.ToDictionary(o => o.Id, _ => new List<string>());

        foreach (var op in Flow.Operators)
            foreach (var dep in op.Dependencies)
            {
                graph[dep.OperatorId].Add(op.Id);
                inDegree[op.Id]++;
            }

        var byId      = Flow.Operators.ToDictionary(o => o.Id);
        var remaining = new HashSet<string>(Flow.Operators.Select(o => o.Id));

        while (remaining.Count > 0)
        {
            var level = remaining.Where(id => inDegree[id] == 0)
                                 .Select(id => byId[id])
                                 .ToList();
            if (level.Count == 0) break; // cycle — Validate() should have caught this

            yield return level;

            foreach (var op in level)
            {
                remaining.Remove(op.Id);
                foreach (var next in graph[op.Id])
                    inDegree[next]--;
            }
        }
    }
}
