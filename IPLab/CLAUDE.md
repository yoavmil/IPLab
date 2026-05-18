# IPLab UI – Notes for Claude

This project (currently named `IPLab`, to be renamed `IPLab.UI`) is the WPF front-end.
See the root [CLAUDE.md](../CLAUDE.md) for project-level context.

## Layout

```
┌──────────────────────────────────────────────┐
│ Toolbar (Run All | Run Selected | Clear)     │
├───────────────────────┬──────────────────────┤
│ Left: Pipeline Editor │ Right: Inspector     │
│  Tab: Graph           │  Tab: Image Preview  │
│  Tab: JSON            │  Tab: Data           │
├───────────────────────┴──────────────────────┤
│ Status Bar (bottom)                          │
└──────────────────────────────────────────────┘
```

- Both `TabControl` elements use `TabStripPlacement="Bottom"`.
- The pipeline editor Graph tab hosts the `NodifyEditor`.
- The JSON tab shows the serialized flow (read-only for now).
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
- **Do NOT use `nodify:LineConnection`** — it ignores the declared side when computing bezier tangent direction. Use `<Path Data="{Binding BezierGeometry}">` with a manually computed `StreamGeometry` bezier instead.
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
- **Per-dependency overrides**: pass `IReadOnlyDictionary<string, (ConnectionSide Source, ConnectionSide Target)>` keyed by dependency ID to `FlowViewModel`'s constructor. Unrecognised IDs fall back to the default.
- **Bezier geometry**: `ConnectionViewModel.BezierGeometry` is a `StreamGeometry` bezier computed from `Source.Anchor`/`Target.Anchor`. Control points are offset along the connector's declared `Side` by `ControlOffset = 80` pixels. It re-evaluates whenever either anchor raises `PropertyChanged`.
- **Multiple connections allowed**: any number of visual connections may exist simultaneously, including to the same connector. The last wired source wins for execution.
- **Replace on reconnect**: if the user draws a new connection between two nodes that already have a connection, the old one is removed first (de-duplicated by source-node / target-node pair, not by specific connector).
- **Parameter wiring**: `OnConnect` wires the **first `CanBeWired` parameter** of the target node to the **first output port** of the source node. The `ParameterEditViewModel` updates `IsWired = true` and `SelectedSource`. `BuildExecutionFlow` derives `Dependencies` from wired parameters — it does **not** read `IOperator.Dependencies`, which is immutable.

## ViewModel structure

| Class | Key properties |
|---|---|
| `ConnectorViewModel` | `Name: string`, `Side: ConnectionSide`, `Anchor: Point` (INPC) |
| `OperatorNodeViewModel` | `TopConnector`, `BottomConnector`, `LeftConnector`, `RightConnector` (fixed, always present), `Parameters`, `Location: Point` (INPC) |
| `ConnectionViewModel` | `Source: ConnectorViewModel`, `Target: ConnectorViewModel`, `BezierGeometry: Geometry` (INPC, derived) |
| `FlowViewModel` | `Nodes`, `Connections`, `Json`, `ConnectCommand` |
| `MainViewModel` | `Flow: FlowViewModel`, `Status: string` |

Anchor tracking flow: `Connector` (UI) computes screen position → writes to `ConnectorViewModel.Anchor` via `OneWayToSource` binding → `ConnectionViewModel.BezierGeometry` re-raises via INPC → `Path.Data` re-renders.
