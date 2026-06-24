using System.Collections.Concurrent;
using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using IPLab.Core.Utilities;

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
    /// <param name="flow">
    /// The flow definition to execute. When you have a <see cref="Flow"/> object returned by
    /// <see cref="Serialization.FlowDefSerializer.Deserialize"/>, pass <c>flow.Def</c> — the layout
    /// is only used by editor UIs and is irrelevant for programmatic execution.
    /// For batch use, create a new <see cref="FlowEx"/> per run. To reuse an instance across runs
    /// while preserving cached results, call <see cref="UpdateFlow"/> instead.
    /// </param>
    /// <param name="enableCaching">When <see langword="true"/>, skips re-execution when results and parameters are unchanged since the last run.</param>
    public FlowEx(IFlowDef flow, bool enableCaching = false)
    {
        _flow = flow;
        _enableCaching = enableCaching;
        _statuses = new ConcurrentDictionary<string, OperatorStatus>(
            flow.Operators.Select(o => KeyValuePair.Create(o.Id, OperatorStatus.NotRun)));
    }

    /// <summary>Initializes a sub-flow executor pre-seeded with results from an outer execution context.</summary>
    internal FlowEx(IFlowDef flow, IReadOnlyDictionary<string, object?> seed)
    {
        _flow = flow;
        _enableCaching = false;
        _statuses = new ConcurrentDictionary<string, OperatorStatus>(
            flow.Operators.Select(o => KeyValuePair.Create(o.Id, OperatorStatus.NotRun)));
        foreach (var (k, v) in seed)
            _results[k] = v;
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
        ThrowIfCachingLoopFlow();
        await Task.WhenAll(BuildExecutionTasks(ct).Values);
    }

    /// <inheritdoc/>
    public async Task RunSingleAsync(string operatorId, CancellationToken ct = default)
    {
        ThrowIfCachingLoopFlow();
        var tasks = BuildExecutionTasks(ct, operatorId);
        if (tasks.TryGetValue(operatorId, out var t))
            await t;
    }

    // Builds a task per operator. Ops run as soon as their predecessors complete (parallel DAG).
    // LoopStart ops run RunLoopBodyAsync instead of RunOperatorAsync.
    // Body/LoopEnd ops redirect to their LoopStart task so downstream ops wait for the full loop.
    // seedId limits the graph to one op and its transitive deps (used by RunSingleAsync).
    private Dictionary<string, Task> BuildExecutionTasks(CancellationToken ct, string? seedId = null)
    {
        var loopContexts = BuildLoopContexts();
        var contextsByStart = loopContexts.ToDictionary(c => c.Start.Id);
        var bodyToLoopStart = loopContexts
            .SelectMany(c => c.Body.Select(b => (b.Id, c.Start.Id)).Append((c.End.Id, c.Start.Id)))
            .ToDictionary(x => x.Item1, x => x.Item2);

        var tasks = new Dictionary<string, Task>();
        var byId  = Flow.Operators.ToDictionary(o => o.Id);

        Task GetTask(string id)
        {
            if (tasks.TryGetValue(id, out var t)) return t;

            // Body ops and LoopEnd are driven by their LoopStart — redirect.
            if (bodyToLoopStart.TryGetValue(id, out var loopStartId))
                return tasks[id] = GetTask(loopStartId);

            var op           = byId[id];
            var predecessors = op.Dependencies.Select(d => GetTask(d.OperatorId));

            if (contextsByStart.TryGetValue(id, out var ctx))
            {
                // Also wait for outer deps of body ops so _results is fully populated before the loop.
                var loopIds = ctx.Body.Select(o => o.Id)
                    .Append(ctx.Start.Id).Append(ctx.End.Id).ToHashSet();
                var outerBodyDeps = ctx.Body.Append(ctx.End)
                    .SelectMany(o => o.Dependencies.Where(dep => !loopIds.Contains(dep.OperatorId)))
                    .Select(dep => GetTask(dep.OperatorId));
                var all = predecessors.Concat(outerBodyDeps).ToArray();
                return tasks[id] = Task.WhenAll(all)
                    .ContinueWith(_ => RunLoopBodyAsync(ctx, ct), TaskScheduler.Default)
                    .Unwrap();
            }

            return tasks[id] = Task.WhenAll(predecessors)
                .ContinueWith(_ => RunOperatorAsync(op, ct), TaskScheduler.Default)
                .Unwrap();
        }

        var roots = seedId is not null
            ? [seedId]
            : Flow.Operators.Select(o => o.Id);
        foreach (var id in roots)
            GetTask(id);

        return tasks;
    }

    /// <inheritdoc/>
    public void ClearResults()
    {
        _results.Clear();
        _paramSnapshot.Clear();
        foreach (var key in _statuses.Keys)
            _statuses[key] = OperatorStatus.NotRun;
    }

    private async Task<object?> RunOperatorAsync(IOperator op, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ResolveParameters is inside the try so that a missing upstream result (because a
        // predecessor failed) marks this op Failed rather than faulting the whole task graph.
        try
        {
            var resolved = ResolveParameters(op);
            var cacheKey = BuildCacheKey(op, resolved);

            if (_enableCaching &&
                _results.ContainsKey(op.Id) &&
                _paramSnapshot.TryGetValue(op.Id, out var snapshot) &&
                ParamsEqual(cacheKey, snapshot))
            {
                SetStatus(op.Id, OperatorStatus.Success, null);
                return _results[op.Id];
            }

            SetStatus(op.Id, OperatorStatus.Running, null);
            var result = await Task.Run(() => op.Type.Execute(resolved), ct);
            _results[op.Id] = result;
            _paramSnapshot[op.Id] = cacheKey;
            SetStatus(op.Id, OperatorStatus.Success, null);
            return result;
        }
        catch (OperationCanceledException)
        {
            SetStatus(op.Id, OperatorStatus.NotRun, null);
            throw;
        }
        catch (Exception ex)
        {
            SetStatus(op.Id, OperatorStatus.Failed, ex);
            return null;
        }
    }

    private async Task RunLoopBodyAsync(LoopContext ctx, CancellationToken ct)
    {
        var baseParams = ResolveParameters(ctx.Start);

        // Probe the loop count by executing LoopStart with Index=0 (validates source is non-empty).
        // Use ToDictionary rather than new Dictionary<K,V>(IReadOnlyDictionary) — the latter overload
        // doesn't exist on .NET Framework 4.8 (only IDictionary is accepted there).
        var probeParams = baseParams.ToDictionary(kv => kv.Key, kv => kv.Value);
        probeParams["Index"] = 0;
        SetStatus(ctx.Start.Id, OperatorStatus.Running, null);
        object? probeResult;
        try { probeResult = ctx.Start.Type.Execute(probeParams); }
        catch (Exception ex) { SetStatus(ctx.Start.Id, OperatorStatus.Failed, ex); return; }
        if (!TryGetLoopCount(probeResult, out int count))
        {
            SetStatus(ctx.Start.Id, OperatorStatus.Failed,
                new InvalidOperationException("LoopStart did not produce a Count output."));
            return;
        }

        var modeStr = baseParams.GetValueOrDefault("Mode") as string ?? nameof(LoopMode.Serial);
        var mode = Enum.TryParse<LoopMode>(modeStr, out var m) ? m : LoopMode.Serial;


        int startIndex = mode == LoopMode.Discrete
            ? Convert.ToInt32(baseParams.GetValueOrDefault("Index") ?? 0)
            : 0;

        if (mode == LoopMode.Discrete && (startIndex < 0 || startIndex >= count))
        {
            SetStatus(ctx.Start.Id, OperatorStatus.Failed,
                new InvalidOperationException($"LoopStart Index {startIndex} is outside the source range 0..{count - 1}."));
            return;
        }

        int n = mode == LoopMode.Discrete ? 1 : count;

        var bodyFlowDef = BuildBodyFlowDef(ctx, n, mode);

        // Seed: all outer results (excluding loop ops) plus per-iteration LoopStart phantoms.
        var loopIds = ctx.Body.Select(o => o.Id).Append(ctx.Start.Id).Append(ctx.End.Id).ToHashSet();
        var seed = _results
            .Where(kv => !loopIds.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        for (int i = 0; i < n; i++)
        {
            int actualIndex = mode == LoopMode.Discrete ? startIndex : i;
            seed[$"{ctx.Start.Id}#{i}"] = new Dictionary<string, object?>
            {
                ["Index"] = actualIndex,
                ["Count"] = count,
            };
        }

        var subFlow = new FlowEx(bodyFlowDef, seed);

        // Forward live status changes from the sub-flow to the outer flow so the UI
        // updates each body op's color while the loop is running, not just at the end.
        var bodyExceptions = new ConcurrentDictionary<string, Exception?>();
        subFlow.StatusChanged += (renamedId, status, ex) =>
        {
            var hash = renamedId.LastIndexOf('#');
            if (hash < 0) return;
            var origId = renamedId[..hash];
            SetStatus(origId, status, ex);
            if (status == OperatorStatus.Failed && ex is not null)
                bodyExceptions.TryAdd(origId, ex);
        };

        await subFlow.RunAllAsync(ct);

        // Harvest body results: _results[id] = last (or only) iteration's result.
        for (int i = 0; i < n; i++)
        {
            foreach (var bodyOp in ctx.Body)
                if (subFlow._results.TryGetValue($"{bodyOp.Id}#{i}", out var r))
                    _results[bodyOp.Id] = r;
            if (subFlow._results.TryGetValue($"{ctx.End.Id}#{i}", out var er))
                _results[ctx.End.Id] = er;
        }

        // Accumulate LoopEnd Out1-Out4 across all iterations.
        var accumulators = Enumerable.Range(0, 4).Select(_ => new object?[count]).ToArray();
        for (int i = 0; i < n; i++)
        {
            int actualIndex = mode == LoopMode.Discrete ? startIndex : i;
            if (subFlow._results.TryGetValue($"{ctx.End.Id}#{i}", out var endResult) &&
                endResult is IReadOnlyDictionary<string, object?> endDict)
            {
                for (int p = 0; p < 4; p++)
                    accumulators[p][actualIndex] = endDict.GetValueOrDefault($"Out{p + 1}");
            }
        }
        _results[ctx.End.Id] = new Dictionary<string, object?>
        {
            ["Out1"] = accumulators[0],
            ["Out2"] = accumulators[1],
            ["Out3"] = accumulators[2],
            ["Out4"] = accumulators[3],
        };

        int representativeIndex = mode == LoopMode.Discrete ? startIndex : count - 1;
        _results[ctx.Start.Id] = new Dictionary<string, object?> { ["Index"] = representativeIndex, ["Count"] = count };

        // Propagate per-body-op statuses from the sub-flow.
        static OperatorStatus WorstStatus(string origId, int iterations, FlowEx sub)
        {
            var worst = OperatorStatus.NotRun;
            for (int i = 0; i < iterations; i++)
            {
                if (!sub._statuses.TryGetValue($"{origId}#{i}", out var s)) continue;
                if (s == OperatorStatus.Failed) return OperatorStatus.Failed;
                if (s > worst) worst = s;
            }
            return worst;
        }

        var anyFailed = false;
        foreach (var bodyOp in ctx.Body)
        {
            var st = WorstStatus(bodyOp.Id, n, subFlow);
            bodyExceptions.TryGetValue(bodyOp.Id, out var bex);
            SetStatus(bodyOp.Id, st, st == OperatorStatus.Failed ? bex : null);
            if (st == OperatorStatus.Failed) anyFailed = true;
        }
        {
            var st = WorstStatus(ctx.End.Id, n, subFlow);
            bodyExceptions.TryGetValue(ctx.End.Id, out var eex);
            SetStatus(ctx.End.Id, st, st == OperatorStatus.Failed ? eex : null);
            if (st == OperatorStatus.Failed) anyFailed = true;
        }

        SetStatus(ctx.Start.Id,
            anyFailed ? OperatorStatus.Failed : OperatorStatus.Success,
            anyFailed ? new InvalidOperationException("One or more loop body operators failed.") : null);
    }

    private static FlowDef BuildBodyFlowDef(LoopContext ctx, int n, LoopMode mode)
    {
        var bodyAndEnd = ctx.Body.Append(ctx.End).ToList();
        var bodyIds = bodyAndEnd.Select(o => o.Id).ToHashSet();
        var loopStartId = ctx.Start.Id;

        // Root body ops: all their deps are outside the body (outer or LoopStart sources).
        var rootBodyOpIds = bodyAndEnd
            .Where(op => op.Dependencies.All(dep => !bodyIds.Contains(dep.OperatorId)))
            .Select(op => op.Id)
            .ToHashSet();

        var operators = new List<Operator>();
        for (int i = 0; i < n; i++)
        {
            string Rename(string id) => $"{id}#{i}";
            foreach (var op in bodyAndEnd)
            {
                var renamedId = Rename(op.Id);

                // Only intra-body deps — outer/LoopStart deps are satisfied via seeded results.
                var deps = op.Dependencies
                    .Where(dep => bodyIds.Contains(dep.OperatorId))
                    .Select(dep => new Dependency($"D_{Rename(dep.OperatorId)}_{renamedId}", Rename(dep.OperatorId)))
                    .ToList();

                // Serial: iteration i+1 root ops must wait for LoopEnd#(i-1) to complete.
                if (mode == LoopMode.Serial && i > 0 && rootBodyOpIds.Contains(op.Id))
                    deps.Add(new Dependency($"D_{ctx.End.Id}#{i - 1}_{renamedId}", $"{ctx.End.Id}#{i - 1}"));

                var parameters = op.Parameters.Select(p =>
                {
                    if (p.Source is not { } src) return p;
                    string newSrcId = bodyIds.Contains(src.OperatorId) ? Rename(src.OperatorId)
                        : src.OperatorId == loopStartId ? $"{loopStartId}#{i}"
                        : src.OperatorId; // outer op — keep original ID, it's in the seed
                    if (newSrcId == src.OperatorId) return p;
                    return new ParameterValue { Name = p.Name, Value = p.Value, Source = new SourceRef(newSrcId, src.Port) };
                }).ToList();

                operators.Add(new Operator
                {
                    Id           = renamedId,
                    DisplayName  = op.DisplayName,
                    Type         = op.Type,
                    Parameters   = parameters,
                    Dependencies = deps,
                });
            }
        }
        return new FlowDef(operators);
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

    private IReadOnlyList<LoopContext> BuildLoopContexts()
    {
        var loops = new List<LoopContext>();
        var edges = DependencyEdges().ToList();
        var bodyOwner = new Dictionary<string, string>();

        foreach (var loopStart in Flow.Operators.Where(o => o.Type.TypeName == "LoopStart"))
        {
            var loopEnds = Flow.Operators.Where(candidate => IsPairedLoopEnd(candidate, loopStart.Id)).ToList();
            if (loopEnds.Count == 0)
                throw new InvalidOperationException($"LoopStart operator '{loopStart.Id}' does not have a paired LoopEnd operator.");
            if (loopEnds.Count > 1)
                throw new InvalidOperationException($"LoopStart operator '{loopStart.Id}' has more than one paired LoopEnd operator.");

            var loopEnd = loopEnds[0];
            var descendants = FlowGraph.GetSelfAndDescendants(loopStart.Id, edges).ToHashSet();
            var ancestors = FlowGraph.GetAncestors(loopEnd.Id, edges);
            var bodyIds = descendants
                .Intersect(ancestors)
                .Where(id => id != loopStart.Id && id != loopEnd.Id)
                .ToHashSet();

            foreach (var bodyId in bodyIds)
            {
                var bodyOp = Flow.Operators.First(o => o.Id == bodyId);
                if (bodyOp.Type.TypeName is "LoopStart" or "LoopEnd")
                    throw new NotImplementedException("Nested loop execution is not supported yet.");
                if (bodyOwner.TryGetValue(bodyId, out var owner))
                    throw new NotImplementedException($"Overlapping loop bodies are not supported yet. Operator '{bodyId}' is in loops '{owner}' and '{loopStart.Id}'.");
                bodyOwner[bodyId] = loopStart.Id;
            }

            var orderedBody = FlowGraph.TopologicalSort(bodyIds, edges)
                .Select(id => Flow.Operators.First(o => o.Id == id))
                .ToList();
            loops.Add(new LoopContext(loopStart, loopEnd, orderedBody));
        }

        return loops;
    }

    private IEnumerable<(string Source, string Target)> DependencyEdges() =>
        Flow.Operators
            .SelectMany(op => op.Dependencies.Select(dep => (dep.OperatorId, op.Id)));

    private static bool TryGetLoopCount(object? result, out int count)
    {
        count = 0;
        if (result is not IReadOnlyDictionary<string, object?> outputs ||
            !outputs.TryGetValue("Count", out var countValue))
            return false;

        count = Convert.ToInt32(countValue);
        return true;
    }

    private static bool IsPairedLoopEnd(IOperator op, string loopStartId) =>
        op.Type.TypeName == "LoopEnd" &&
        op.Parameters.Any(param =>
            param.Name == "Index" &&
            param.Source?.OperatorId == loopStartId &&
            param.Source?.Port == "Index");

    private void ThrowIfCachingLoopFlow()
    {
        if (!_enableCaching)
            return;

        if (Flow.Operators.Any(op => op.Type.TypeName is "LoopStart" or "LoopEnd"))
            throw new NotImplementedException("FlowEx caching is not supported for flows that contain loop operators.");
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
                // Extract by port name when the result is a dictionary; otherwise use the raw value.
                // Works uniformly for single-port ops (raw or dict), multi-port ops (always dict),
                // and seeded phantoms.
                resolved[param.Name] = raw is IReadOnlyDictionary<string, object?> d
                    ? d.GetValueOrDefault(source.Port)
                    : raw;
            }
            else
            {
                resolved[param.Name] = param.Value;
            }
        }
        return resolved;
    }

    private sealed record LoopContext(IOperator Start, IOperator End, IReadOnlyList<IOperator> Body);

}
