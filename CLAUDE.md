# IPLab – Project Notes for Claude

## Project Goal

IPLab is a C# desktop tool for visually building, tuning, and debugging OpenCV image-processing pipelines.

> IPLab is an offline C# desktop tool for visually building, tuning, and debugging OpenCV image-processing pipelines on saved images, with live previews of each stage and export as C# code or JSON pipeline definitions.

## Current Scope

First version: **offline image processing only** — no live camera, no video, no real-time constraints. The user loads saved image files and builds processing flows around them.

## Main Workflow

1. Open an image or image set.
2. Build an image-processing flow visually.
3. Add processing steps (operators).
4. Connect operators together.
5. Run the full flow or individual operators.
6. Inspect the output of each operator.
7. Tune parameters.
8. Export the flow as JSON + executor module.

## Terminology

**Operator** — one processing step in the flow (Load image, Blur, Threshold, Find contours, etc.). Each operator has inputs, outputs, parameters, execution status, and a displayable result.

**Flow** — the complete visual pipeline: which operators exist, how they are connected, what parameters each uses, enough to save/load/execute.

**FlowEx** — the runtime executor. Responsibilities: run full flow or a single operator, resolve dependencies, store intermediate results (Mat objects, contours, etc.), report status and errors.

## Architecture Direction

See [docs/DESIGN.md](docs/DESIGN.md) for core interface definitions and diagrams.
See [docs/OPERATORS.md](docs/OPERATORS.md) for the catalogue of built-in operators.
See [docs/TODO.md](docs/TODO.md) for the backlog of planned work and known gaps.
See [IPLab.UI/CLAUDE.md](IPLab.UI/CLAUDE.md) for UI-specific rules and Nodify implementation notes.

**Rule: whenever a new operator is added to `IPLab.Core.Operators`, add a corresponding entry to [docs/OPERATORS.md](docs/OPERATORS.md).**

**Rule: whenever a new operator is added, decide explicitly whether it supports ROI and discuss with the user before implementing. If it does, follow the pattern in `RoiParameters` — filter operators use `ApplyImageFilter`, detection operators use `Clamp` + coordinate translation. See [docs/OPERATORS.md#roi](docs/OPERATORS.md) for the full list of ROI-supporting operators.**

**Rule: whenever a new operator is added, its `OutputPorts` must use `IReadOnlyList<OutputPortDescriptor>` with an explicit `DataType` per port (e.g. `typeof(Mat)`, `typeof(CircleSegment[])`, `typeof(int)`). Script/dynamic operators use `typeof(object)` as a wildcard. Connectable input parameters declare `ConnectableType` — the C# type they accept; `null` (omitted) means not connectable; `typeof(object)` means connectable wildcard. Omit `Type` for wire-only sockets (defaults to `ParameterType.Object` = no UI control). There is no `IsConnectable` flag — connectability is implied by `ConnectableType != null`. See `PortTypeCompat.IsCompatible` for the full compatibility rules.**

**Rule: after any meaningful change to `IPLab.Core` or its tests, run the full test suite (`dotnet test`) and confirm it passes before considering the task done.**

## Design Decisions

**Flow layout:** Free node graph (not a step list), supporting multiple paths and parallel execution.

**Operator interface:** Each operator exposes an auto-generated ID (`O` + number), a type name, and a user-selected display name.

**Non-image data between operators:** Strongly typed at the interface level, stored in runtime memory as `object`.

**Parameter UI generation:** Each operator is self-describing — it tells the UI how to render its parameters.

**Partial recomputation:** Not supported in MVP. Any parameter change triggers full re-execution.

**Memory management:** No special treatment for large images in MVP.

**Plugin system:** Built-in operators only. Extensibility deferred beyond MVP.

## Resolved Decisions

- **Flow data model:** `FlowDef` → list of `Operator` records, each with `Id`, `DisplayName`, `Type` (`IOperatorType`), `Parameters` (`ParameterValue[]`), `Dependencies` (`Dependency[]`). Serialized as pretty-printed camelCase JSON via `FlowDefSerializer` (no attributes — private DTO classes inside the serializer).
- **Operator registry:** `OperatorRegistry.CreateDefault()` auto-discovers all concrete `IOperatorType` implementations via reflection — no manual registration needed.
- **Export format:** JSON flow file + compatible runtime executor. No code generation in MVP.
- **Typed output ports:** `IOperatorType.OutputPorts` is `IReadOnlyList<OutputPortDescriptor>` — each port carries `Name` and `DataType: System.Type`. `ParameterDescriptor` carries an optional `ConnectableType: Type?` — the CLR type the parameter accepts when wired; `null` = wildcard. Compatibility is checked by `PortTypeCompat.IsCompatible(ConnectableType, portDataType)`: `typeof(object)` port or `null`/`typeof(object)` param = wildcard; `double` param accepts `int` port (widening); otherwise `IsAssignableFrom`. Wire-only parameters (e.g. Image inputs) omit `Type` (defaults to `ParameterType.Object` = no UI control) and just declare `ConnectableType = typeof(Mat)`. Image ports all use `typeof(Mat)` — grayscale vs. colour is not distinguished at this level.

## Open Questions

1. How should multiple outputs be represented — overlaid on the image, or shown in the textual result panel?
2. Where should the parameter settings panel open when an operator is double-clicked?

## MVP Feature List

- Open image, add operators visually, connect into a linear flow
- Run all / run selected
- Preview output image per operator
- Show textual result per operator
- Edit parameters
- Show operator status colors (not run / running / success / failed / disabled)
- Save/load flow as JSON
- Execute saved flow through `FlowEx`
- Edit flow as JSON in the pipeline editor (JSON view tab), with live two-way sync to graph view
