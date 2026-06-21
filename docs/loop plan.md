# LoopStart / LoopEnd Plan

## Summary

- Implement loops with flat `LoopStart` and `LoopEnd` operators instead of `GroupingNode`.
- Build this in small slices. Do not implement the full loop system in one pass.
- The graph stays flat and serializes as normal operators, parameters, dependencies, and layout.
- Stage 1 is complete (serial full-loop execution).
- Stage 2 is largely complete: Slices A (Mode param), B (sub-FlowEx + unified unrolling), B+ (live status), and E (Parallel mode) are done. Slices C (per-iteration results), D (inspector per-iteration), F (caching), and G (nested loops) remain pending.

## Current Status (Stage 2 — Slices A, B, B+, E complete; C, D, F, G pending)

Done:

- `LoopStartOperator` and `LoopEndOperator` are normal flat operators in the `Flow` category.
- `LoopStart.Source` accepts only `Mat` or non-string `IEnumerable`; it rejects null, strings, unsupported scalar values, empty sources.
- `LoopStart` outputs `Index` and `Count`.
- `LoopStart.Mode` parameter selects `Discrete`, `Serial`, or `Parallel`; default `Serial` so existing flows continue to work.
- `LoopEnd` has `Index`, `In1`–`In4`, outputs `Out1`–`Out4`; stateless pass-through per iteration. `FlowEx` accumulates results by index.
- **Unified execution path**: both loop and non-loop flows go through `BuildExecutionTasks`; body/LoopEnd ops redirect to their LoopStart task; no separate serial-only code path remains.
- **Sub-FlowEx approach** (`BuildBodyFlowDef` + seeded `FlowEx`): for each loop, builds a flat `FlowDef` with `N` renamed copies of all body+end ops (`O3#0`, `O3#1`, …); Serial adds cross-iteration ordering deps; Parallel/Discrete omit them. A seeded sub-`FlowEx` runs this flat DAG.
- **All three modes implemented**: Serial (sequential ordering via synthetic deps), Discrete (N=1, user-chosen index), Parallel (all iterations concurrent via the plain DAG executor).
- **Real-time status forwarding**: body operator status changes in the sub-flow are forwarded to the outer `StatusChanged` event, so each body operator's node color updates live during a loop run (not just at the end).
- After all iterations, `_results[loopEnd.Id]` holds `{Out1: object?[], …, Out4: object?[]}` across all N iterations; `_results[bodyOp.Id]` holds the last iteration's result.
- `FlowEx` probes loop count via a direct `LoopStart.Type.Execute` call before building the sub-FlowDef.
- The inspector falls back to upstream ancestor images for body nodes with no display output.
- Inspector `Unwrap<T>` looks through `LoopEnd` accumulator arrays and ignores null slots.
- Caching still throws `NotImplementedException` when loops are present (Slice F not yet implemented).
- Tests: `FlowExLoopTests.cs` (Serial ordering, Discrete single-slot, Parallel correctness + overlap); outer-flow DAG parallelism covered by separate test.

## Current Usage Example

`D:\iplab\decode_fidutials.ipl` demonstrates the Stage 1 pattern:
- `TemplateMatch.Rectangles` → `LoopStart.Source`.
- `LoopStart.Index` is set manually (Discrete concept before the mode existed).
- A `CSharpScript` node receives `LoopStart.Index` plus the original collection and outputs the ROI for that index.
- A paired `LoopEnd` collects body outputs into `Out1`–`Out4`.

---

## Stage 2: Mode-Aware Execution

### Design Principles

- Keep slices shippable: each slice leaves the codebase in a working state.
- Execution strategy is determined at runtime from the `Mode` parameter — not at graph-build time.
- `_results[bodyOp.Id]` always holds the **last (or only) iteration** for backward compatibility with downstream operators and the existing inspector image path.
- A separate `_iterationResults[(opId, index)]` table holds every iteration's per-body-operator result so the inspector can show any selected index without re-running.

### Mode Semantics

| Mode | Behavior |
|---|---|
| `Discrete` | Runs the body exactly once for `LoopStart.Index`. Replaces the old "set Index manually" usage. |
| `Serial` | Runs all iterations 0..count-1 in order. Current default behavior. |
| `Parallel` | Runs all iterations concurrently in isolated frames; accumulates after all frames complete. |

Default mode = `Serial` so existing saved flows are unaffected (no `Mode` param → deserialized as `Serial`).

### FlowEx Execution Architecture

The main `RunAllAsync` identifies loop body/end ops up front (so they are skipped in the main DAG walk) but defers the actual loop work until `LoopStart` is reached during execution. At that point:

1. Resolve `LoopStart.Source` → `count`. Read `Mode`.
2. Call `BuildBodyFlowDef(loopContext, count, mode)` → temporary `FlowDef`.
3. Create a sub-`FlowEx` pre-seeded with outer results + per-iteration `LoopStart` phantom results.
4. `await subFlowEx.RunAllAsync(ct)`.
5. Harvest results back into main `_results` and `_iterationResults`.

**Serial and Parallel use the same unrolled FlowDef structure; they differ only by whether cross-iteration ordering deps are added.** The sub-`FlowEx` always runs a plain flat DAG — it has no knowledge of loops.

#### BuildBodyFlowDef — unified unrolling

For all modes, produce a flat `FlowDef` with `N` renamed copies of every body operator and `LoopEnd` (`N = count` for Serial/Parallel, `N = 1` for Discrete):

- IDs are suffixed: `O3` → `O3#0`, `O3#1`, …, `O3#N-1`.
- **Intra-body** dependency sources are rewritten to the `#i`-suffixed IDs.
- **Outer** dependency sources (pre-loop operators) keep their original IDs — they are in the pre-seeded results.
- **LoopStart** parameter sources are rewritten: `LoopStart.Index` → phantom ID `LoopStart#i` for iteration `i`. Each phantom is pre-seeded with `{Index: i, Count: count}`.

**Serial only**: add a synthetic dependency from each iteration `i+1`'s **root body ops** (body ops whose only deps are outer or LoopStart) onto `LoopEnd#i`. This forces the DAG to serialize the iterations while the executor itself is unchanged.

**Parallel**: no cross-iteration deps → the DAG executor runs all `N` iterations concurrently for free.

**Discrete**: same as Parallel with `N = 1`. No ordering deps needed.

After the sub-`FlowEx` finishes, for each original body op ID `id`:
- `_iterationResults[(id, i)]` ← `subFlowEx._results["id#i"]`
- `_results[id]` ← last iteration's result (`subFlowEx._results["id#(N-1)"]`)
- `_results[loopEnd.Id]` ← accumulated dict `{Out1: object?[], …}` built from all `LoopEnd#i` results.

#### Pre-seeding

Pre-seeding is done via a new `FlowEx(FlowDef, IReadOnlyDictionary<string, object?> seed)` constructor overload that copies `seed` into `_results` before execution starts. Seeded entries are treated as already-completed operators: `ResolveParameters` reads them normally, and `RunAllAsync` skips operators whose IDs are in `_results` at startup.

**Seeded entries per loop run:**
- All outer results (from main `_results`, excluding loop body/end IDs).
- Per-iteration LoopStart phantoms: `"LoopStart#i"` → `{Index: i, Count: count}` for every `i`.

Why count is deferred: `Source` must be resolved (outer ops must have run) before `count` is known. The body `FlowDef` cannot be built at plan time; building it when `LoopStart` is reached is the correct point.

### Per-Iteration Result Storage

```
_iterationResults: Dictionary<(string opId, int index), object?>
```

- Written during loop execution for every body operator at every index.
- Read by the inspector when a body operator is selected and `LoopStart.Index` is set.
- Cleared by `ClearResults()` along with `_results`.
- Exposed via `IFlowEx.IterationResults` (read-only).

### Inspector — Mode-Aware Display

`LoopStart.Index` is the inspector's iteration selector in **all** modes. The user sets it in the settings panel; the inspector shows that iteration's annotations and Mat for every body node. No new UI widget is needed.

#### Discrete

`LoopStart.Index` is the single iteration that executes. The inspector simply shows `_results[id]`, which is the only stored result. No special inspector logic is needed for this mode — it works today once Discrete execution is implemented.

#### Serial / Parallel

After a run, `_iterationResults[(id, i)]` holds every iteration's result per body operator. When a body node is selected, the inspector reads `_iterationResults[(id, loopStart.Index)]` instead of `_results[id]` (which holds only the last iteration).

Implementation (deferred to Slice D, not yet implemented):
- `InspectorViewModel.BuildState()`: if the selected node is inside a loop body, find the paired `LoopStart` and read its `Index` parameter value.
- Use `IterationResults.TryGetValue((id, selectedIndex), out var iterResult)` as the result; fall back to `IntermediateResults[id]` if not found.
- A `LoopStart.Index` parameter change triggers `UpdateSelectedImage` to refresh the inspector without re-running.

### Caching Strategy

Current state: caching throws for any flow with loop operators.

Proposed:
1. **Outer operators** (upstream of LoopStart): cache normally via the existing `_paramSnapshot` mechanism. These are not loop-specific.
2. **Loop body** (whole-loop caching): before running the loop, compute a `loopFingerprint`:
   - Identity of the source object (reference or hash for arrays).
   - `count` value.
   - Snapshot of all resolved parameters for every body operator and LoopEnd.
   - Mode.
   If the fingerprint matches the previous run's fingerprint (stored in `_loopSnapshot[loopStartId]`), skip the entire loop and reuse the stored accumulators.
3. **Per-iteration caching** (nice to have, after basic loop caching works): key each iteration's result by `(opId, index, iterationParamSnapshot)`. Only re-run iterations whose inputs changed.

The coarse whole-loop cache covers the common case (source image didn't change, params didn't change → skip the whole loop). Fine-grained per-iteration caching is a later optimization.

`ThrowIfCachingLoopFlow()` is removed once the whole-loop fingerprint is implemented.

---

## Stage 2 Slices

### Slice A: Mode Parameter ✅ Done

- `Mode` enum (`Discrete`, `Serial`, `Parallel`) in `IPLab.Core.Models.Enums`.
- `Mode` as an `Enum` parameter in `LoopStartOperator.ParameterSchema`. Default = `Serial`.
- `LoopStart.Execute` reads `Mode` from parameters (FlowEx reads it back through resolved params).

Tests:
- Schema includes `Mode` with default `Serial` and options `Discrete`/`Serial`/`Parallel`.
- Serialization round-trip with `Mode = Discrete`.
- Old saved flow without `Mode` param deserializes as `Serial`.

### Slice B: Sub-FlowEx + Unified Unrolling ✅ Done

- `FlowEx(FlowDef flow, IReadOnlyDictionary<string, object?> seed)` seeded constructor.
- `BuildBodyFlowDef(LoopContext ctx, int n, LoopMode mode)`: renames body+end op IDs with `#i` suffix; rewrites intra-body deps and LoopStart param sources; Serial adds synthetic `rootOps#(i+1)` → `LoopEnd#i` deps; Parallel/Discrete omit them.
- `RunLoopBodyAsync(ctx, ct)`: resolves count, builds sub-FlowDef, creates seeded sub-FlowEx, runs it; harvests back into main `_results`.
- Unified `BuildExecutionTasks`: loop and non-loop flows share the same DAG executor; body/LoopEnd ops redirect to their LoopStart task.
- `Index`-range validation removed from `LoopStart.Execute`; FlowEx validates for Discrete before building the sub-FlowDef.

Tests: `FlowExLoopTests.cs` — Serial ordering, Discrete single-slot, Parallel correctness + overlap.

### Slice B+: Live Body-Operator Status Forwarding ✅ Done

The `subFlow.StatusChanged` subscription in `RunLoopBodyAsync` now forwards all status changes (Running, Success, Failed) to the outer `SetStatus`, mapping renamed IDs (`O3#2`) back to original IDs (`O3`). Body operator nodes now update their color live during a loop run, not just after it completes.

In Parallel mode, multiple iterations may fire `Running`/`Success` events for the same original ID concurrently; last-write-wins is acceptable for the live display. The post-loop bulk propagation always sets the correct final status.

### Slice C: Per-Iteration Result Storage

- Add `_iterationResults: ConcurrentDictionary<(string, int), object?>` to `FlowEx`.
- In `RunLoopBodyAsync` harvest step: for each original body op ID `id` and each `i`, write `_iterationResults[(id, i)]` from `subFlowEx._results["id#i"]`.
- Expose as `IFlowEx.IterationResults: IReadOnlyDictionary<(string, int), object?>`.
- `ClearResults()` also clears `_iterationResults`.
- `UpdateFlow()` prunes stale operator IDs from `_iterationResults`.

Tests:
- After serial run of 3 iterations, each body operator has 3 entries in `IterationResults`.
- After Discrete run, exactly one entry per body operator.
- `ClearResults()` empties `IterationResults`.

### Slice D: Inspector Per-Iteration View

- `InspectorViewModel` receives `IterationResults` from `ExecutionService`.
- `BuildState()`: if selected node is a loop body operator, find its paired `LoopStart` and read `Index` from its parameters; use `IterationResults[(id, index)]` as the result.
- Fall back to `IntermediateResults[id]` if no per-iteration entry found.
- `LoopStart.Index` parameter change fires `PrecomputeAsync` + `UpdateSelectedImage` to refresh the inspector without re-running.

Tests:
- Inspector shows iteration-2 result when `LoopStart.Index = 2` and body node is selected.
- Inspector falls back to IntermediateResults when IterationResults has no entry for that body node.

### Slice E: Parallel Mode ✅ Done

Parallel mode works automatically: `BuildBodyFlowDef` omits cross-iteration deps for Parallel, and the sub-FlowEx's plain DAG executor runs all iterations concurrently. No separate Parallel code path was needed. See `Parallel_RunsAllIterationsConcurrently_CorrectResults` test in `FlowExLoopTests.cs`.

### Slice F: Loop Caching

- Add `_loopSnapshot: ConcurrentDictionary<string, LoopFingerprint>` to `FlowEx`.
- Before running a loop, compute `LoopFingerprint` (source reference + count + body param snapshot + mode).
- If fingerprint matches stored snapshot, skip the loop body and reuse `_results[loopEnd.Id]`.
- Remove `ThrowIfCachingLoopFlow()`.
- Outer operator caching is already unblocked once loops no longer throw.

Tests:
- Second run with unchanged source and params: loop body not re-executed.
- Second run with changed source: loop re-executes.
- Second run with changed body param: loop re-executes.

### Slice G: Nested Loops

The sub-`FlowEx` architecture supports nested loops naturally because `BuildBodyFlowDef` renames inner `LoopStart`/`LoopEnd` IDs along with everything else, and the sub-`FlowEx` calls `BuildLoopContexts()` on the unrolled body — finding the renamed inner loops and expanding them recursively.

Two changes are required:

- **`BuildLoopContexts` in the main FlowEx**: change from "throw on any `LoopStart` found inside a body" to "only collect top-level loops — pairs whose `LoopStart` is not inside any other loop's body." Inner loops are left in the outer body FlowDef and handled recursively by the sub-`FlowEx`.
- **`_iterationResults` key**: `(opId, int)` only captures one level. For nested body ops the key becomes ambiguous (outer index vs. inner index). Widen the key to `(opId, int[])` — an index path — so `[outerIndex]` addresses an outer body op and `[outerIndex, innerIndex]` addresses an inner body op. Inspector falls back to `IntermediateResults[id]` for any depth it cannot resolve.

Tests:
- Outer serial loop containing an inner serial loop produces the correct accumulated results at both levels.
- Outer parallel loop containing an inner serial loop: iterations are isolated; inner loops are serialized within each outer frame.
- `_iterationResults` correctly stores and retrieves results at both index depths.

---

## Operator Interfaces (Stage 2)

### LoopStart Parameters (updated)

| Name | Type | Connectable | Notes |
|---|---|---|---|
| `Source` | `Object` (wire-only) | `typeof(object)` | Mat or non-string IEnumerable |
| `Index` | `Int` | No | In Discrete mode: the single iteration that executes. In all modes: which iteration's results the inspector displays for body operators. |
| `Mode` | `Enum` | No | `Discrete` / `Serial` / `Parallel`; default `Serial` |

### LoopStart Outputs

| Name | DataType |
|---|---|
| `Index` | `int` |
| `Count` | `int` |

### LoopEnd (unchanged)

`In1`–`In4` inputs, `Out1`–`Out4` outputs, stateless pass-through. `FlowEx` accumulates.

---

## Assumptions / Constraints

- `Mode = Serial` is the default so existing `.ipl` files continue to work without migration.
- Nested and overlapping loops remain unsupported through Stage 2.
- Parallel frame count is unbounded for now (no concurrency cap). A throttle can be added later via `SemaphoreSlim`.
- The iteration browser (a dedicated UI widget to scrub through results) is deferred beyond Stage 2. `LoopStart.Index` in the settings panel is sufficient for now.
- `GetItemOperator` (Stage 2 direction from before) is still planned but independent of the mode refactor.
