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

    public event Action<string, OperatorStatus, Exception?>? StatusChanged;

    public async Task RunAllAsync()
    {
        var tasks = new Dictionary<string, Task>();
        var byId  = Flow.Operators.ToDictionary(o => o.Id);

        Task GetTask(string id)
        {
            if (tasks.TryGetValue(id, out var t)) return t;
            var op           = byId[id];
            var predecessors = op.Dependencies.Select(d => GetTask(d.OperatorId)).ToArray();
            return tasks[id] = Task.WhenAll(predecessors)
                .ContinueWith(_ => RunOperatorAsync(op), TaskScheduler.Default)
                .Unwrap();
        }

        foreach (var op in Flow.Operators)
            _ = GetTask(op.Id);

        await Task.WhenAll(tasks.Values);
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
        SetStatus(op.Id, OperatorStatus.Running, null);
        try
        {
            var resolved = ResolveParameters(op);
            _results[op.Id] = await Task.Run(() => op.Type.Execute(resolved));
            SetStatus(op.Id, OperatorStatus.Success, null);
        }
        catch (Exception ex)
        {
            SetStatus(op.Id, OperatorStatus.Failed, ex);
        }
    }

    private void SetStatus(string id, OperatorStatus status, Exception? ex)
    {
        _statuses[id] = status;
        StatusChanged?.Invoke(id, status, ex);
    }

    private IReadOnlyDictionary<string, object?> ResolveParameters(IOperator op)
    {
        var resolved = new Dictionary<string, object?>();
        foreach (var param in op.Parameters)
        {
            if (param.Source is { } source)
            {
                if (!_results.TryGetValue(source.OperatorId, out var raw))
                    throw new InvalidOperationException($"Operator '{source.OperatorId}' did not produce a result — it may have failed.");
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

}
