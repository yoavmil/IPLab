using System.Collections.Concurrent;
using IPLab.Core.Interfaces;
using IPLab.Core.Models;

namespace IPLab.Core.Runtime;

/// <summary>Async runtime executor for a processing flow. Resolves parameters, respects dependency order, and optionally caches results.</summary>
public class FlowEx : IFlowEx
{
    private readonly ConcurrentDictionary<string, object?> _results = new();
    private readonly ConcurrentDictionary<string, OperatorStatus> _statuses;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, object?>> _paramSnapshot = new();

    private IFlowDef _flow;
    /// <inheritdoc/>
    public IFlowDef Flow => _flow;
    private readonly bool _enableCaching;

    /// <summary>Initializes a new <see cref="FlowEx"/> for the given flow.</summary>
    /// <param name="flow">The flow to execute.</param>
    /// <param name="enableCaching">When <see langword="true"/>, skips re-execution when results and parameters are unchanged since the last run.</param>
    public FlowEx(IFlowDef flow, bool enableCaching = false)
    {
        _flow = flow;
        _enableCaching = enableCaching;
        _statuses = new ConcurrentDictionary<string, OperatorStatus>(
            flow.Operators.Select(o => KeyValuePair.Create(o.Id, OperatorStatus.NotRun)));
    }

    /// <summary>
    /// Replaces the flow definition in place so the executor can be reused across runs
    /// without losing the cached param snapshots and intermediate results.
    /// Operators added since the last run get a fresh NotRun status; deleted operators
    /// are pruned from results, statuses, and snapshots.
    /// </summary>
    public void UpdateFlow(IFlowDef newFlow)
    {
        _flow = newFlow;
        var newIds = newFlow.Operators.Select(o => o.Id).ToHashSet();
        foreach (var id in _statuses.Keys.Where(id => !newIds.Contains(id)).ToList())
        {
            _statuses.TryRemove(id, out _);
            _results.TryRemove(id, out _);
            _paramSnapshot.TryRemove(id, out _);
        }
        foreach (var op in newFlow.Operators)
            _statuses.TryAdd(op.Id, OperatorStatus.NotRun);
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> IntermediateResults => _results;
    /// <inheritdoc/>
    public IReadOnlyDictionary<string, OperatorStatus> Statuses => _statuses;

    /// <inheritdoc/>
    public event Action<string, OperatorStatus, Exception?>? StatusChanged;

    /// <inheritdoc/>
    public async Task RunAllAsync(CancellationToken ct = default)
    {
        var tasks = new Dictionary<string, Task>();
        var byId  = Flow.Operators.ToDictionary(o => o.Id);

        Task GetTask(string id)
        {
            if (tasks.TryGetValue(id, out var t)) return t;
            var op           = byId[id];
            var predecessors = op.Dependencies.Select(d => GetTask(d.OperatorId)).ToArray();
            return tasks[id] = Task.WhenAll(predecessors)
                .ContinueWith(_ => RunOperatorAsync(op, ct), TaskScheduler.Default)
                .Unwrap();
        }

        foreach (var op in Flow.Operators)
            _ = GetTask(op.Id);

        await Task.WhenAll(tasks.Values);
    }

    /// <inheritdoc/>
    public async Task RunSingleAsync(string operatorId, CancellationToken ct = default)
    {
        var op = Flow.Operators.First(o => o.Id == operatorId);
        await RunOperatorAsync(op, ct);
    }

    /// <inheritdoc/>
    public void ClearResults()
    {
        _results.Clear();
        _paramSnapshot.Clear();
        foreach (var key in _statuses.Keys)
            _statuses[key] = OperatorStatus.NotRun;
    }

    private async Task RunOperatorAsync(IOperator op, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var resolved = ResolveParameters(op);
        var cacheKey = BuildCacheKey(op, resolved);

        if (_enableCaching &&
            _results.ContainsKey(op.Id) &&
            _paramSnapshot.TryGetValue(op.Id, out var snapshot) &&
            ParamsEqual(cacheKey, snapshot))
        {
            SetStatus(op.Id, OperatorStatus.Success, null);
            return;
        }

        SetStatus(op.Id, OperatorStatus.Running, null);
        try
        {
            _results[op.Id] = await Task.Run(() => op.Type.Execute(resolved), ct);
            _paramSnapshot[op.Id] = cacheKey;
            SetStatus(op.Id, OperatorStatus.Success, null);
        }
        catch (OperationCanceledException)
        {
            SetStatus(op.Id, OperatorStatus.NotRun, null);
            throw;
        }
        catch (Exception ex)
        {
            SetStatus(op.Id, OperatorStatus.Failed, ex);
        }
    }

    // Builds the cache key compared between runs: the resolved parameters, plus any extra tokens an
    // operator contributes via ICacheInvalidationProvider (e.g. a calibration file's last-write
    // time). This invalidates the cache when external state changes even though the parameters —
    // such as the file path — are byte-for-byte identical.
    private static IReadOnlyDictionary<string, object?> BuildCacheKey(
        IOperator op, IReadOnlyDictionary<string, object?> resolved)
    {
        if (op.Type is not ICacheInvalidationProvider provider)
            return resolved;

        var key = resolved.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        foreach (var (name, value) in provider.GetCacheTokens(resolved))
            key[name] = value;
        return key;
    }

    private static bool ParamsEqual(IReadOnlyDictionary<string, object?> a, IReadOnlyDictionary<string, object?> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var (key, av) in a)
        {
            if (!b.TryGetValue(key, out var bv)) return false;
            if (!ValuesEqual(av, bv)) return false;
        }
        return true;
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a is string[] sa && b is string[] sb) return sa.SequenceEqual(sb);
        if (a.GetType().IsValueType || a is string) return a.Equals(b);
        return false; // reference types (Mat, etc.) → already equal only by ReferenceEquals above
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
