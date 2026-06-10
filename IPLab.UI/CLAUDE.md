# IPLab UI – Notes for Claude

This project (`IPLab.UI`) is the WPF front-end.
See the root [CLAUDE.md](../CLAUDE.md) for project-level context.

## Layout

```
┌──────────────────────────────────────────────┐
│ Toolbar (Run All | Run Selected | Clear)     │
├───────────────────────┬──────────────────────┤
│ Left: Pipeline Editor │ Right: Inspector     │
│  Tab: Graph           │  Tab: Image Preview  │
│                       │  Tab: Data           │
├───────────────────────┴──────────────────────┤
│ Status Bar (bottom)                          │
└──────────────────────────────────────────────┘
```

- Both `TabControl` elements use `TabStripPlacement="Bottom"`.
- The pipeline editor Graph tab hosts the `NodifyEditor`.
- The Inspector right panel is placeholder for now.

## Ribbon / Toolbar (planned)

```
File     Open Project | Save | Save As
Run      Run All | Run Selected | Stop | Clear Results
View     Zoom Fit | Actual Size | Show Grid | Show Overlays
Tools    Settings | Operator Manager
```

## Operator Visual Status (planned)

```
Not Run      neutral / gray
Running      blue or animated border
Success      green
Failed       red
Disabled     faded gray
```

## Nodify 7.3

Full implementation guide: [docs/nodify-workflow-implementation-guide.md](../docs/nodify-workflow-implementation-guide.md).

Key rules:
- **Do NOT use `nodify:Node` for custom layouts** — use a plain `DataTemplate` with `nodify:Connector` elements directly, not wrapped in `nodify:Node`.
- The connector's `ControlTemplate` must contain `<Border x:Name="PART_Connector" …/>` — that named part is what `Connector.OnApplyTemplate()` looks for.
- `ItemContainer.Location` must be bound **TwoWay** so dragging updates the VM.
- Set `NodifyEditor.AutoRegisterConnectionsLayer = false` in `MainWindow`'s **static constructor** before `InitializeComponent()`.
- **Use `nodify:StepConnection`** with `SourcePosition="{Binding SourcePosition}"` and `TargetPosition="{Binding TargetPosition}"` (both `ConnectorPosition` enum). `StepConnection` auto-coerces `Direction`/`SourceOrientation`/`TargetOrientation` internally and correctly handles all connector combinations: opposite sides (standard S), same side (U-loop), and mixed (L-bend). Built-in arrowheads via `ArrowEnds`/`ArrowSize`. Do NOT use `nodify:Connection` (bezier), `nodify:LineConnection` (can't handle same-side), or manual `Path`/`StreamGeometry`.
- `NodifyEditor.ConnectionCompletedCommand` receives a **`ValueTuple<object, object?>`** (not `Tuple<T,U>`). Use `RelayCommand<(object, object?)>` — if you use the class `Tuple`, the cast silently fails and the handler is never called.

## Node connector layout

Each `OperatorNodeViewModel` has exactly **4 fixed connectors**, one per side, always visible:

| Property | Side | XAML position |
|---|---|---|
| `TopConnector` | `ConnectionSide.Top` | Row 0, Col 1, `HorizontalAlignment="Center"` |
| `BottomConnector` | `ConnectionSide.Bottom` | Row 2, Col 1, `HorizontalAlignment="Center"` |
| `LeftConnector` | `ConnectionSide.Left` | Row 1, Col 0, `VerticalAlignment="Center"` |
| `RightConnector` | `ConnectionSide.Right` | Row 1, Col 2, `VerticalAlignment="Center"` |

In XAML, each is bound with `DataContext="{Binding TopConnector}"` (etc.) so that Nodify passes the `ConnectorViewModel` — not the node — as the command parameter.

## Connection rules

- **Default sides**: source exits `Bottom`, target enters `Top`. This produces top-to-bottom vertical S-curves for a standard pipeline flow.
- **Per-dependency overrides**: `IFlowLayout.Dependencies` carries a `DependencyLayout` per dependency ID. `FlowViewModel` builds a lookup at construction; unrecognised IDs fall back to `(Bottom, Top)`.
- **Bezier geometry**: `ConnectionViewModel.BezierGeometry` is a `StreamGeometry` bezier computed from `Source.Anchor`/`Target.Anchor`. Control points are offset along the connector's declared `Side` by `ControlOffset = 80` pixels. It re-evaluates whenever either anchor raises `PropertyChanged`.
- **One connection per node pair**: at most one visual connection may exist between any (sourceNode, targetNode) pair. Drawing a new connection between a pair that already has one removes the old first.
- **Dependency ID format**: `D_{sourceOperatorId}_{targetOperatorId}` (e.g. `D_O2_O3`). Connections carry this ID so the serializer can match visual connector sides to the correct `DependencyLayout`.
- **Parameter wiring**: `OnConnect` wires the **first `CanBeWired` parameter** of the target node to the **first output port** of the source node. The `ParameterEditViewModel` updates `IsWired = true` and `SelectedSource`. `BuildExecutionFlow` derives `Dependencies` — one per unique upstream operator, grouped from wired parameters — it does **not** read `IOperator.Dependencies`, which is immutable.
- **Dep vs. param wiring**: a `Dependency` expresses execution ordering only (one per unique upstream operator). The fine-grained data-flow detail (which output port feeds which parameter) lives in `ParameterValue.Source`, not in `Dependency`.

## ViewModel structure

| Class | Key properties |
|---|---|
| `ConnectorViewModel` | `Name: string`, `Side: ConnectionSide`, `Anchor: Point` (INPC) |
| `OperatorNodeViewModel` | `TopConnector`, `BottomConnector`, `LeftConnector`, `RightConnector` (fixed, always present), `Parameters`, `Location: Point` (INPC) |
| `ConnectionViewModel` | `Source: ConnectorViewModel`, `Target: ConnectorViewModel`, `SourceOrientation`, `TargetOrientation` (derived from `Side`) |
| `FlowViewModel` | `Nodes`, `Connections`, `Json`, `ConnectCommand` |
| `MainViewModel` | `Flow: FlowViewModel`, `Status: string` |

Anchor tracking flow: `Connector` (UI) computes screen position → writes to `ConnectorViewModel.Anchor` via `OneWayToSource` binding → `ConnectionViewModel` re-raises `Source`/`Target` via INPC → `LineConnection.Source`/`Target` re-renders.

## AvailableSources type filtering

`RebuildAvailableSources` (in `FlowViewModel`) filters each parameter's `AvailableSources` to only include ports whose `DataType` is compatible with the parameter's `ParameterType`, using `PortTypeCompat.IsCompatible` from `IPLab.Core.Models`. `SourceRefViewModel` carries the port's `DataType: Type` for this purpose. The auto-wiring default on connect picks the first *compatible* port from `AvailableSources` rather than blindly using `OutputPorts[0]`.

## Inspector Overlay Rendering

The inspector uses `RControls.ImageViewer` (wrapping `RControls.ImageCanvas`) to draw overlays on top of the displayed image. Coordinate system: image pixels, (column=X, row=Y).

**Data flow:**
1. `InspectorState` — sealed record carrying the current operator's results + overlay data. Because it is a record, `==` does structural comparison: all fields are reference types (arrays, `BitmapSource`) so equality is reference equality per field, except `RoiDef` which is a value record.
2. `InspectorViewModel.UpdateSelectedImage` — computes a candidate `InspectorState` via `BuildState()`, then **only assigns `State` if `newState != State`**. This prevents unnecessary `RedrawAnnotations` calls when nothing changed.
3. `InspectorViewModel.EditingNode` setter — subscribes to parameter changes for live overlay updates (ROI params).
4. `InspectorControl.RedrawAnnotations` — called only when `State` actually changes; clears old shapes and calls `ImageViewer.DrawXxx`.

**Available draw methods on `ImageViewer`:**
- `DrawCircle(row, column, radius, name, color, bFilled)` — for circles/blobs
- `DrawRectangle(row1, col1, row2, col2, name, color, bFilled)` — axis-aligned rectangle (used for ROI)
- `DrawRectangle2(row, col, phi, length1, length2, name, color, bFilled)` — rotated rectangle; `length1`/`length2` are half-width/half-height; used for stripe region overlay
- `DrawLine(rowBegin, colBegin, rowEnd, colEnd, name, color, bFilled)` — single line
- `DrawLine(double[], double[], double[], double[], name, color, bFilled)` — multiple lines in one call; all four arrays must be the same length; `bFilled` is **required** (not optional unlike the `ImageCanvas` overload)
- `DrawPolygons(IReadOnlyList<List<Point>> polygons, name, color)` — multiple closed polygons as a single `Path`
- `RemoveRegion(name, ShapeMode)` — remove overlays by name and/or shape type

**`ShapeMode` values used for `RemoveRegion`:** `Circle`, `Rectangle`, `Cross`, `Polygon`, `Line`

**`StrokeThickness`** is automatically set to `2.0 / Viewer.scaleRatio` so lines stay 2px wide at any zoom level.

**Pattern for adding a new overlay type:**
1. Add a field to `InspectorState` for the data
2. Add an `Unwrap<T>` branch in `BuildState()` to detect it from the executor result
3. If the overlay should react to parameter changes while editing, subscribe in the `EditingNode` setter (see `_roiParamSubscriptions` pattern)
4. Add a `DrawXxx` private method in `InspectorControl` and call it from `RedrawAnnotations`
5. Add the corresponding `RemoveRegion` call at the top of `RedrawAnnotations`

## Flicker / CPU rules — do not regress these

**Rule: `InspectorViewModel.UpdateSelectedImage` must use the `newState != State` guard.** Never set `State` unconditionally. The guard prevents `RedrawAnnotations` from firing when nothing changed, which is the primary cause of inspector flicker.

**Rule: `PrecomputeAsync` must reuse `BitmapSource` objects when the source `Mat` reference is unchanged.** The `_precomputedMats` dictionary tracks which `Mat` produced each cached `BitmapSource`. When the same `Mat` reference appears again (because `FlowEx` caching kept it alive), skip PNG re-encoding and copy the old `BitmapSource`. A new `BitmapSource` object — even with identical pixels — breaks reference equality in `InspectorState` and causes a flicker.

**Rule: `ExecutionService` must reuse its `FlowEx` instance across runs.** Call `_executor.UpdateFlow(newFlow)` rather than `new FlowEx(newFlow)` on every run. Creating a new `FlowEx` discards `_paramSnapshot`, which defeats operator-level caching entirely: every run re-executes all operators, producing new `Mat` objects, which breaks `BitmapSource` caching, which breaks the `State` equality guard.

**Rule: continuous-run mode must enforce a minimum cycle time.** After each run in `ExecuteRunAsync`, compute elapsed time and `await Task.Delay` for the remainder of the minimum cycle (currently 16 ms). Without this, a fully-cached run completes in ~0 ms and the dispatcher loop spins at 100% CPU on one core.
