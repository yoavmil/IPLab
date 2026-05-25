# IPLab TODO

> Completed items are erased from this file, not struck out.

## Docs

- **README.md** — root-level project introduction: what IPLab is, who it's for,
  quick-start (how to build and run), and links to OPERATORS.md

## IPLab (UI project)

- **Move existing UserControls into `Controls/` folder** — `InspectorControl`, `RibbonControl`, and `ToolboxControl` currently live in the root of `IPLab.UI`. Move them into `IPLab.UI/Controls/` alongside `LayerItemControl` and `OverlayImageViewer`, excluding `MainWindow`, updating namespaces and XAML references accordingly.

- **Settings panel drawer animation** — the parameter settings panel (currently a static overlay at the bottom of the graph) should animate open and closed like a drawer: slide up when opening, slide down when closing. When the user clicks ⚙ on a second operator while the panel is already open, it should quickly slide closed and then slide open again showing the new operator's parameters, rather than snapping instantly. The animation timing should feel snappy (open ~150 ms, close ~100 ms) so it doesn't slow down the workflow.

- **Delete operator with confirmation** — right-clicking a node (or pressing Delete when it is selected) should prompt "Remove operator X?" before removing it from the graph. Deleting a node must also remove all connections to/from it and prune stale wired sources from any downstream nodes, mirroring the cleanup already done in `OnDeleteConnection`.

- **Delete connection with confirmation** — before `OnDeleteConnection` removes a connection, show a brief confirmation (e.g. "Remove this connection? Downstream nodes may lose their inputs.") so the user doesn't accidentally break the flow.

- **ROI (Region of Interest)** — let the user draw one or more ROI shapes on an operator's input image. Each ROI specifies the spatial region the operator acts on; outside the ROI the original pixel data is preserved or masked. Some ROIs also carry a direction vector (e.g. for oriented filters or directional edge detection). Design questions to resolve: how ROIs are stored in `IOperator.Parameters` vs. a new dedicated field; whether ROI drawing lives inside `ImageViewer` or in a separate overlay canvas; and how the direction is visualised (arrow overlay) and stored.

- **Embed OPERATORS.md as in-app reference**
  Include `docs/OPERATORS.md` as an embedded resource in the UI project (Build Action: `EmbeddedResource`). Surface it in a dedicated "Operator Reference" panel or modal — e.g. a `FlowDocument`/Markdown viewer accessible from the Help menu or a toolbar button — so the user can browse the full catalogue without leaving the app. The content should update automatically whenever `OPERATORS.md` changes at build time (no manual copy step). Design question: whether to render raw Markdown (via a lightweight renderer) or convert to `FlowDocument` at build time.

- **Per-operator help in the settings panel**
  In the operator settings panel, add a collapsible "Help" section below the parameter list. When expanded it shows the operator's entry from OPERATORS.md (description, parameter table, usage notes). The section starts collapsed so it doesn't crowd the parameter controls for experienced users. Source the text from the same embedded resource used by the full reference panel above — parse out the relevant heading block at startup or on first expand. Design question: whether the collapse state is per-operator-type (remembered across opens) or always starts collapsed.

- **Ribbon polish and unified dark theme** — the current `RibbonControl` is a plain `ToolBarTray` with no visual styling. Replace it with a properly styled ribbon: grouped sections with icons, separator lines, and hover/press states. Adopt the Nodify built-in dark theme (merge its `ResourceDictionary` into `App.xaml`) so that the node editor, ribbon, panels, and all standard WPF controls share the same dark palette and accent color rather than each being styled ad-hoc.

- **Introduce a DI container and service layer for inter-ViewModel communication** — currently, callbacks like `onOpenSettings` and `onSelected` are threaded through `MainViewModel → FlowViewModel → BuildNodes → OperatorNodeViewModel` constructors. Replace with an `INodeInteractionService` (or similar) that ViewModels take as a constructor dependency, removing the callback parameters from the chain. This also makes it easier to add new cross-ViewModel interactions without touching intermediate classes.

- **Multi-image input in the image source operator** — extend the `LoadImageOperator` (or add a dedicated `ImageSourceOperator`) to hold a list of image file paths rather than a single path. The operator's result panel should display thumbnails of all listed images beneath the main preview; clicking a thumbnail loads that image as the operator's active output, triggering a full re-run of the flow so all downstream operators update. The parameter editor should allow adding/removing files from the list (e.g. via a file-picker or drag-and-drop). This lets the user quickly toggle between images and visually compare how the same pipeline handles different inputs.

- **Center image in inspector when smaller than viewport** — when the `ImageViewer` is resized, if the image is smaller than the available viewport (e.g. after zooming out or opening a small image), keep the image centered rather than leaving it pinned to the top-left. The centering should update live as the panel is dragged to resize, so the image tracks the viewport's midpoint smoothly. [Nice to have]

- **Pixel inspector on click** — when the user clicks a pixel in the `ImageViewer`, display its coordinates (x, y) and color value adapted to the image type: RGB triplet for colour images, single intensity for grayscale, and 0/1 (or 0/255) for binary masks. Show this info in a small overlay near the cursor or in a persistent status bar below the viewer. Clear the display when the user clicks outside the image. [Nice to have]

- **Output display settings per operator** — let the user configure how detection results are visualised in the inspector. For annotation color, offer three modes: a single fixed color (color-picker), a random-per-entity color (stable hash of entity index so colors don't shuffle on re-run), and a heatmap (map a scalar — e.g. circle radius or blob response — to a gradient). Store the chosen mode and parameters inside the operator's display metadata so settings persist with the saved flow. Start with circle/blob annotations; apply the same system to any future operator that produces non-image output.


## Project

- **`IProject` and `IFlow` naming** — `IFlow` gets a `Name` string. `IProject` owns an ordered collection of named `IFlow` instances and is the top-level document model. `MainViewModel` shifts from owning a single flow to owning an `IProject`. Serialization stores the project as a single file containing all flows.

- **Project view** — a home screen showing all flows in the project as a card grid. Each card displays the flow's name and its configured display image with annotation overlays (see *Flow display settings* below). Clicking a card navigates into the Flow view (the existing node graph editor). A breadcrumb or back button returns to the Project view.

- **Flow display settings** — each flow carries display metadata: `DisplayImageOperatorId` (which operator's output image to render on the project card) and `AnnotationOperatorIds[]` (which operators' detection results to overlay on that image as annotations). These settings are edited from within the Flow view — e.g. right-clicking an operator node offers "Set as display image" / "Toggle annotation". The project card re-renders whenever the flow is re-run.

## IPLab.Core (Architecture)

## IPLab.Core

- **Serialization: handle array parameter values**
  `FlowDefSerializer.CoerceValue` currently handles scalar types only (int, double, bool, string).
  If a parameter value is an array (e.g. a list of points or thresholds passed as a literal),
  serialization will store it but deserialization will not reconstruct the correct CLR array type.
  Extend `CoerceValue` and the `ParameterType` enum to cover array cases, and add a round-trip test.

- **Type-safe output ports on `IOperatorType`**
  Currently `OutputPorts` is `IReadOnlyList<string>` (names only). Each port should
  also declare its data type (e.g. `Mat`, `CircleSegment[]`, `int`) so that
  `FlowDef.Validate()` can verify that a wired `ParameterValue.Source` port type
  is compatible with the target `ParameterDescriptor` type — the same way input
  parameters are already typed via `ParameterDescriptor`.

- **Loop / per-item expansion (fan-out execution)**
  When an operator emits a collection output (e.g. `CircleSegment[]`, `Point[][]`,
  `Mat[]`), a downstream operator connected to that port should be able to run once
  per element rather than receiving the whole array. Model this as a "fan-out" edge
  in `FlowDef`: the edge carries a flag (e.g. `expandItems: true`) that tells
  `FlowEx` to iterate over the collection and invoke the downstream operator for
  each item, collecting individual results back into a new array. The graph UI
  should mark such edges visually (e.g. a dashed or double-line style) and let the
  user toggle the mode on a connection. Design questions to resolve: how fan-out
  composes when multiple inputs are expanded simultaneously (zip vs. cross-product);
  how the collected result array is typed and passed further downstream; and whether
  partial failures (one item throws) abort the whole fan-out or accumulate errors.

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
