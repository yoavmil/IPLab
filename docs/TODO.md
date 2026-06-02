# IPLab TODO

> Completed items are erased from this file, not struck out.

## Docs

- **README.md** — root-level project introduction: what IPLab is, who it's for,
  quick-start (how to build and run), and links to OPERATORS.md. Include a
  *Usage example* section that links to [`scripts/Waviness/`](../scripts/Waviness/)
  as a worked end-to-end example of building and running an IPLab flow from code.

## IPLab (UI project)

- **FPS counter in status bar during continuous run** — while `IsRunningContinuous` is active, replace or augment the status bar text at the bottom right with a live FPS readout (e.g. "12 fps"). Measure wall-clock time between consecutive `UpdateSelectedImage` calls and display a rolling average. Hide the counter when continuous run stops.

- **Per-operator execution timing** — record how long each operator's `Execute` call takes (wall-clock, excluding queue wait). Display the duration in the bottom-right corner of the operator node (e.g. "14 ms") so the user can spot bottlenecks at a glance. Also surface the timing in the Data tab of the inspector when that operator is selected. Store timings inside `FlowEx` alongside `IntermediateResults`; reset on `ClearResults`.

- **Cursor change on connection hover** — when the mouse hovers over a connector line (edge) between two operators, change the cursor to indicate it is interactive (e.g. `Hand` or a custom pointer). Currently the cursor stays as the default arrow, giving no affordance that the connection can be clicked or deleted.

- **Delete connection with confirmation** — before `OnDeleteConnection` removes a connection, show a brief confirmation (e.g. "Remove this connection? Downstream nodes may lose their inputs.") so the user doesn't accidentally break the flow.

- **ROI (Region of Interest)** — let the user draw one or more ROI shapes on an operator's input image. Each ROI specifies the spatial region the operator acts on; outside the ROI the original pixel data is preserved or masked. Some ROIs also carry a direction vector (e.g. for oriented filters or directional edge detection). Design questions to resolve: how ROIs are stored in `IOperator.Parameters` vs. a new dedicated field; whether ROI drawing lives inside `ImageViewer` or in a separate overlay canvas; and how the direction is visualised (arrow overlay) and stored.

- **Embed OPERATORS.md as in-app reference**
  Include `docs/OPERATORS.md` as an embedded resource in the UI project (Build Action: `EmbeddedResource`). Surface it in a dedicated "Operator Reference" panel or modal — e.g. a `FlowDocument`/Markdown viewer accessible from the Help menu or a toolbar button — so the user can browse the full catalogue without leaving the app. The content should update automatically whenever `OPERATORS.md` changes at build time (no manual copy step). Design question: whether to render raw Markdown (via a lightweight renderer) or convert to `FlowDocument` at build time.

- **Per-operator help in the settings panel**
  In the operator settings panel, add a collapsible "Help" section below the parameter list. When expanded it shows the operator's entry from OPERATORS.md (description, parameter table, usage notes). The section starts collapsed so it doesn't crowd the parameter controls for experienced users. Source the text from the same embedded resource used by the full reference panel above — parse out the relevant heading block at startup or on first expand. Design question: whether the collapse state is per-operator-type (remembered across opens) or always starts collapsed.

- **Ribbon polish and unified dark theme** — the current `RibbonControl` is a plain `ToolBarTray` with no visual styling. Replace it with a properly styled ribbon: grouped sections with icons, separator lines, and hover/press states. Adopt the Nodify built-in dark theme (merge its `ResourceDictionary` into `App.xaml`) so that the node editor, ribbon, panels, and all standard WPF controls share the same dark palette and accent color rather than each being styled ad-hoc.

- **Introduce a DI container and service layer for inter-ViewModel communication** — currently, callbacks like `onOpenSettings` and `onSelected` are threaded through `MainViewModel → FlowViewModel → BuildNodes → OperatorNodeViewModel` constructors. Replace with an `INodeInteractionService` (or similar) that ViewModels take as a constructor dependency, removing the callback parameters from the chain. This also makes it easier to add new cross-ViewModel interactions without touching intermediate classes.

- **Abstract the executor result store behind an interface** — `MainViewModel.ResolveRoiParam` directly accesses `_executor.IntermediateResults` and casts values out of a raw `Dictionary<string, object?>`, coupling the ViewModel to the executor's internal storage layout. Introduce an `IExecutionResults` interface on `FlowEx` (e.g. `bool TryGetPortValue(string operatorId, string port, out object? value)`) so the ViewModel only depends on the interface, not on the concrete dict structure. This makes the ROI overlay (and any similar feature that needs to read runtime values from the ViewModel) testable without a real executor instance. Pairs naturally with the DI/service-layer item above.


- **Output display settings per operator** — let the user configure how detection results are visualised in the inspector. For annotation color, offer three modes: a single fixed color (color-picker), a random-per-entity color (stable hash of entity index so colors don't shuffle on re-run), and a heatmap (map a scalar — e.g. circle radius or blob response — to a gradient). Store the chosen mode and parameters inside the operator's display metadata so settings persist with the saved flow. Start with circle/blob annotations; apply the same system to any future operator that produces non-image output.

- **Open empty .ipl file doesn't show error message** - it doesn't crash the app, but the user doesn't know anything happened. a popup error message is needed here.

- **Inspector overlay layers survive image-selection change** — when the user clicks a different thumbnail in a `LoadImageOperator` and the flow re-runs, the inspector's overlay layers (circle annotations, contour overlays, etc.) reset to their defaults. The active layer selection and any per-layer visibility toggles should be preserved across re-runs so the user doesn't have to re-enable them after every image switch.

- **New flow command** — provide a "New flow" action (toolbar button or File menu) that clears the current graph and starts fresh. If the current flow has unsaved changes, prompt to save first. Complements the existing open/save commands.


## Project

- **`IProject` and `IFlow` naming** — `IFlow` gets a `Name` string. `IProject` owns an ordered collection of named `IFlow` instances and is the top-level document model. `MainViewModel` shifts from owning a single flow to owning an `IProject`. Serialization stores the project as a single file containing all flows.

- **Project view** — a home screen showing all flows in the project as a card grid. Each card displays the flow's name and its configured display image with annotation overlays (see *Flow display settings* below). Clicking a card navigates into the Flow view (the existing node graph editor). A breadcrumb or back button returns to the Project view.

- **Flow display settings** — each flow carries display metadata: `DisplayImageOperatorId` (which operator's output image to render on the project card) and `AnnotationOperatorIds[]` (which operators' detection results to overlay on that image as annotations). These settings are edited from within the Flow view — e.g. right-clicking an operator node offers "Set as display image" / "Toggle annotation". The project card re-renders whenever the flow is re-run.

## IPLab.Core (Architecture)

- **Uniform operator return convention** — `Execute` currently returns the value directly for single-output operators and a `Dictionary<string, object?>` for multi-output ones; `FlowEx.ResolveParameters` branches on `OutputPorts.Count` to handle both. This is an implicit convention that's easy to get wrong. Change `IOperatorType.Execute` return type from `object?` to `IReadOnlyDictionary<string, object?>`, update all operators to always return a dictionary keyed by port name, and simplify `ResolveParameters` to always extract by port name — eliminating the count-based branch. The interface change makes the compiler enforce the convention.

- **Multiple LoadImage operators in one flow** — the current design assumes a single `LoadImageOperator` as the flow's image source. When multiple loaders exist (e.g. for a two-image comparison flow), the following are unresolved: which loader's thumbnail strip is shown and where; whether switching the active image on one loader triggers a full re-run or only the sub-graph downstream of that loader; and how `FlowEx` resolves execution order when two independent source nodes both feed the same downstream operator. Define the execution and UI model before adding multi-source flows.

## IPLab.Core

- **Serialization: handle array parameter values**
  `FlowDefSerializer.CoerceValue` currently handles scalar types only (int, double, bool, string).
  If a parameter value is an array (e.g. a list of points or thresholds passed as a literal),
  serialization will store it but deserialization will not reconstruct the correct CLR array type.
  Extend `CoerceValue` and the `ParameterType` enum to cover array cases, and add a round-trip test.


## Loop Groups

A **loop group** is a visual `GroupingNode` (Nodify's built-in resizable container) that wraps a sub-graph and executes it once per element of an input collection — like a `foreach` block in a visual programming environment. The group declares what collection it iterates; operators inside the group see the current element and loop index as typed inputs, and can also reach parameters from outside the group. After all iterations, the group collects individual results into a new output array that downstream operators receive.

- **Visual representation** — use Nodify's `GroupingNode` as the container. The group has a header label (e.g. "For each CircleSegment") and a dedicated input port on its border wired to the upstream collection output. The border style should distinguish it from plain grouping boxes (e.g. a coloured accent border).

- **Adding operators into a loop group** — two ways to populate a group: (a) drag existing nodes into the `GroupingNode` bounds so they snap inside, or (b) select nodes first and then apply "Wrap in loop" from a context menu, which creates a new `GroupingNode` sized to fit the selection. Removing a node from the group (dragging it out) restores it to the outer flow.

- **Loop-scoped input variables** — each iteration, the group exposes two implicit typed ports available to every internal operator: `loop_item` (typed to the element type of the input collection, e.g. `CircleSegment`) and `loop_index` (`int`, zero-based). Internal operators wire to these ports the same way they wire to any output port.

- **Access to outer parameters** — operators inside the group can still wire to any output port from the outer flow (operators outside the group). The execution model passes outer results by reference into each iteration; they are read-only inside the loop.

- **Result collection (fan-in)** — one operator inside the group is designated as the loop's output (e.g. via a "Mark as loop output" toggle on the node). After all iterations, `FlowEx` collects that operator's per-iteration results into a typed array (e.g. `Mat[]`) and exposes it as the group's single output port, available to downstream operators in the outer flow.

- **Execution model in `FlowEx`** — `FlowEx` detects loop group nodes in the flow graph, resolves the input collection, then runs the internal sub-graph sequentially once per element (parallel execution deferred post-MVP). Partial failures (one iteration throws) should be configurable: abort-all or accumulate-errors modes.

- **Serialization** — `FlowDef` represents a loop group as a node of type `LoopGroup` containing a nested `FlowDef` for the inner sub-graph, the collection source port reference, and the designated output operator ID. Round-trip JSON serialization must preserve the nested structure.

- **C# script operator**
  Add a `CSharpScriptOperator` that accepts a user-written C# snippet (via
  `Microsoft.CodeAnalysis.CSharp.Scripting` / Roslyn) and executes it at runtime.
  The script receives the operator's input ports as named variables (e.g.
  `Mat image`, `CircleSegment[] circles`) and must assign a value to a predefined
  `result` variable that becomes the operator's output. Parameters are the script
  text itself plus any named constants the script references. The parameter editor
  shows a code editor control with syntax highlighting and inline error squiggles
  (compile errors surfaced as operator status). Serialization stores the raw script
  text in the JSON flow file. Security note: scripts run in full trust inside the
  desktop process — no sandboxing in MVP, but document this clearly.
