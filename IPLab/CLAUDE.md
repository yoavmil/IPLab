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
- **Do NOT use `nodify:Node` for custom layouts** — use a plain `DataTemplate` with `nodify:Connector` elements directly, not wrapped in `nodify:Node`. See guide §6.1 (`MainWorkflowStepTemplate`) for the top-down pattern.
- The connector's `ControlTemplate` must contain `<Border x:Name="PART_Connector" …/>` — that named part is what `Connector.OnApplyTemplate()` looks for.
- Use `nodify:LineConnection` with `SourceOrientation="Vertical"` + `TargetOrientation="Vertical"` for top-to-bottom curves.
- `ItemContainer.Location` must be bound **TwoWay** so dragging updates the VM.
- Set `NodifyEditor.AutoRegisterConnectionsLayer = false` in `MainWindow`'s **static constructor** before `InitializeComponent()`.

## ViewModel structure

| Class | Key properties |
|---|---|
| `ConnectorViewModel` | `Anchor: Point` (INPC), `Name: string` |
| `OperatorNodeViewModel` | `Inputs`, `Outputs` (`ObservableCollection<ConnectorViewModel>`), `Location: Point` (INPC) |
| `ConnectionViewModel` | `Source: ConnectorViewModel`, `Target: ConnectorViewModel` |
| `FlowViewModel` | `Nodes`, `Connections`, `Json` |
| `MainViewModel` | `Flow: FlowViewModel`, `Status: string` |

Anchor tracking flow: `Connector` (UI) computes screen position → writes to `ConnectorViewModel.Anchor` via `OneWayToSource` binding → `LineConnection` re-renders because `Source.Anchor`/`Target.Anchor` changed via INPC.
