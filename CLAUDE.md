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
See [IPLab.Core/CLAUDE.md](IPLab.Core/CLAUDE.md) for operator implementation rules and OpenCV coding patterns.
See [IPLab.UI/CLAUDE.md](IPLab.UI/CLAUDE.md) for UI-specific rules and Nodify implementation notes.

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
- **Typed output ports:** `IOperatorType.OutputPorts` is `IReadOnlyList<OutputPortDescriptor>` — each port carries `Name`, `DataType: System.Type`, and `IsDisplayImage: bool`. Set `IsDisplayImage = true` on the port whose rendered bitmap the inspector should show; the inspector takes the first such port. `ParameterDescriptor` carries an optional `ConnectableType: Type?` — the CLR type the parameter accepts when wired; `null` = wildcard. Compatibility is checked by `PortTypeCompat.IsCompatible(ConnectableType, portDataType)`: `typeof(object)` port or `null`/`typeof(object)` param = wildcard; `double` param accepts `int` port (widening); otherwise `IsAssignableFrom`. Wire-only parameters (e.g. Image inputs) omit `Type` (defaults to `ParameterType.Object` = no UI control) and just declare `ConnectableType = typeof(Mat)`. Image ports all use `typeof(Mat)` — grayscale vs. colour is not distinguished at this level.

## Open Questions

1. How should multiple outputs be represented — overlaid on the image, or shown in the textual result panel?
2. Where should the parameter settings panel open when an operator is double-clicked?

## NuGet Distribution

`IPLab.Core` is published as a local NuGet package so external solutions can consume it without a source reference. `IPLab.Core/README.md` is the NuGet-facing readme (shown on nuget.org) — it is included in the package via `<PackageReadmeFile>`.

- **Local feed:** `%USERPROFILE%\LocalNuGet\`
- **nuget.config** at the repo root registers this feed alongside nuget.org.
- **Pack command:** `dotnet pack IPLab.Core\IPLab.Core.csproj -c Release -o .\nupkg`
- **Version:** bump `<Version>` in `IPLab.Core\IPLab.Core.csproj` before packing a new release. Current: `1.0.0-alpha.7`.
- Consuming solutions add the same `nuget.config` and reference `<PackageReference Include="IPLab.Core" Version="x.y.z" />`.

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
