# IPLab TODO

> Completed items are erased from this file, not struck out.

## Docs

- **Document algorithm vs. wrapper distinction in OPERATORS.md** — `FindStripeEdges` is a self-contained algorithm (1D projection, box-difference derivative, parabolic interpolation, subpixel peak detection) rather than a thin wrapper around a single OpenCV call like most other operators. OPERATORS.md should acknowledge this distinction — e.g. a short prose section explaining that most operators delegate to one `Cv2.*` function while a few implement multi-step algorithms internally. The `FindStripeEdges` entry should note its algorithmic nature (caliper-style 1D edge finder, not an OpenCV primitive) so readers know what to expect when reading or modifying its implementation.

- **Root README.md** — root-level project introduction: what IPLab is, who it's for,
  quick-start (how to build and run the desktop app), and links to OPERATORS.md and
  `IPLab.Core/README.md`. Include a *Usage example* section that links to
  [`scripts/Waviness/`](../scripts/Waviness/) as a worked end-to-end example.

## CI / CD

- **GitHub Actions: publish IPLab.Core to NuGet.org on git tag** — a workflow that triggers on `v*.*.*` tag pushes and publishes the package. Use NuGet.org Trusted Publishers (OIDC) to avoid storing an API key.

## IPLab (UI project)

- **Save inspector image to file** — add a "Save image…" button or right-click context menu item in the inspector image panel. Opens a `SaveFileDialog` filtered to JPEG and PNG. Saves the currently displayed `BitmapSource` (the operator's display-image output, without overlay shapes) to the chosen path. The default filename should include the operator display name and a timestamp.

- **FPS counter in status bar during continuous run** — while `IsRunningContinuous` is active, replace or augment the status bar text at the bottom right with a live FPS readout (e.g. "12 fps"). Measure wall-clock time between consecutive `UpdateSelectedImage` calls and display a rolling average. Hide the counter when continuous run stops.

- **Per-operator execution timing** — record how long each operator's `Execute` call takes (wall-clock, excluding queue wait). Display the duration in the bottom-right corner of the operator node (e.g. "14 ms") so the user can spot bottlenecks at a glance. Also surface the timing in the Data tab of the inspector when that operator is selected. Store timings inside `FlowEx` alongside `IntermediateResults`; reset on `ClearResults`.

- **Application settings dialog** — a modal settings window (accessible from a toolbar button or Tools menu) that stores preferences in a small JSON file next to the executable (or in `%AppData%\IPLab\settings.json`). Initial settings to expose:
  - **Inspector overlay colors** — one color-picker per overlay type: ROI rectangle, stripe region, circles/blobs, contours, line segments. Defaults match the current hard-coded colors in `InspectorControl`. Changes take effect immediately (live preview while the picker is open). Colors are passed into `InspectorControl.RedrawAnnotations` instead of being hard-coded there. call it legend.

  Persistence: serialize using ready-made ISettings I would take from another project I have.

- **Cursor change on connection hover** — when the mouse hovers over a connector line (edge) between two operators, change the cursor to indicate it is interactive (e.g. `Hand` or a custom pointer). Currently the cursor stays as the default arrow, giving no affordance that the connection can be clicked or deleted.

- **Delete connection with confirmation** — before `OnDeleteConnection` removes a connection, show a brief confirmation (e.g. "Remove this connection? Downstream nodes may lose their inputs.") so the user doesn't accidentally break the flow.

- **ROI (Region of Interest)** — let the user draw one or more ROI shapes on an operator's input image. Each ROI specifies the spatial region the operator acts on; outside the ROI the original pixel data is preserved or masked. Some ROIs also carry a direction vector (e.g. for oriented filters or directional edge detection). Design questions to resolve: how ROIs are stored in `IOperator.Parameters` vs. a new dedicated field; whether ROI drawing lives inside `ImageViewer` or in a separate overlay canvas; and how the direction is visualised (arrow overlay) and stored.

- **Embed OPERATORS.md as in-app reference**
  Include `docs/OPERATORS.md` as an embedded resource in the UI project (Build Action: `EmbeddedResource`). Surface it in a dedicated "Operator Reference" panel or modal — e.g. a `FlowDocument`/Markdown viewer accessible from the Help menu or a toolbar button — so the user can browse the full catalogue without leaving the app. The content should update automatically whenever `OPERATORS.md` changes at build time (no manual copy step). Design question: whether to render raw Markdown (via a lightweight renderer) or convert to `FlowDocument` at build time.

- **Per-operator help in the settings panel**
  In the operator settings panel, add a collapsible "Help" section below the parameter list. When expanded it shows the operator's entry from OPERATORS.md (description, parameter table, usage notes). The section starts collapsed so it doesn't crowd the parameter controls for experienced users. Source the text from the same embedded resource used by the full reference panel above — parse out the relevant heading block at startup or on first expand. Design question: whether the collapse state is per-operator-type (remembered across opens) or always starts collapsed.

- **Ribbon polish and unified dark theme** — the current `RibbonControl` is a plain `ToolBarTray` with no visual styling. Replace it with a properly styled ribbon: grouped sections with icons, separator lines, and hover/press states. Adopt the Nodify built-in dark theme (merge its `ResourceDictionary` into `App.xaml`) so that the node editor, ribbon, panels, and all standard WPF controls share the same dark palette and accent color rather than each being styled ad-hoc.

- **Introduce a DI container and service layer for inter-ViewModel communication** — currently, callbacks like `onOpenSettings` and `onSelected` are threaded through `MainViewModel → FlowViewModel → BuildNodes → OperatorNodeViewModel` constructors. Replace with an `INodeInteractionService` (or similar) that ViewModels take as a constructor dependency, removing the callback parameters from the chain. This also makes it easier to add new cross-ViewModel interactions without touching intermediate classes.

- **Abstract the executor result store behind an interface** — `InspectorViewModel.ResolveParamAsDouble` directly accesses `ExecutionService.IntermediateResults` and casts values out of a raw `Dictionary<string, object?>`, coupling the ViewModel to the executor's internal storage layout. Introduce an `IExecutionResults` interface on `FlowEx` (e.g. `bool TryGetPortValue(string operatorId, string port, out object? value)`) so the ViewModel only depends on the interface, not on the concrete dict structure. This makes the ROI overlay (and any similar feature that needs to read runtime values from the ViewModel) testable without a real executor instance. Pairs naturally with the DI/service-layer item above.


- **Output display settings per operator** — let the user configure how detection results are visualised in the inspector. For annotation color, offer three modes: a single fixed color (color-picker), a random-per-entity color (stable hash of entity index so colors don't shuffle on re-run), and a heatmap (map a scalar — e.g. circle radius or blob response — to a gradient). Store the chosen mode and parameters inside the operator's display metadata so settings persist with the saved flow. Start with circle/blob annotations; apply the same system to any future operator that produces non-image output.

- **Add snackbar / toast notification** — add a snackbar component and replace all `MessageBox.Show` calls with it. User has ready-made code for this component. Example use cases: loading a flow that contains unknown operator types (currently they are silently skipped — the user should see a dismissible warning such as "2 unknown operator types were skipped: GaussianBlur, OldCalibration").

- **Open empty .ipl file doesn't show error message** - it doesn't crash the app, but the user doesn't know anything happened. a popup error message is needed here.

- **Inspector overlay layers survive image-selection change** — when the user clicks a different thumbnail in a `LoadImageOperator` and the flow re-runs, the inspector's overlay layers (circle annotations, contour overlays, etc.) reset to their defaults. The active layer selection and any per-layer visibility toggles should be preserved across re-runs so the user doesn't have to re-enable them after every image switch.


## Project

- **`IProject` and `IFlow` naming** — `IFlow` gets a `Name` string. `IProject` owns an ordered collection of named `IFlow` instances and is the top-level document model. `MainViewModel` shifts from owning a single flow to owning an `IProject`. Serialization stores the project as a single file containing all flows.

- **Project view** — a home screen showing all flows in the project as a card grid. Each card displays the flow's name and its configured display image with annotation overlays (see *Flow display settings* below). Clicking a card navigates into the Flow view (the existing node graph editor). A breadcrumb or back button returns to the Project view.

- **Flow display settings** — each flow carries display metadata: `DisplayImageOperatorId` (which operator's output image to render on the project card) and `AnnotationOperatorIds[]` (which operators' detection results to overlay on that image as annotations). These settings are edited from within the Flow view — e.g. right-clicking an operator node offers "Set as display image" / "Toggle annotation". The project card re-renders whenever the flow is re-run.

- **Operator demo project** — a built-in `.iplproj` file (committed under `scripts/OperatorDemo/`) that ships with the repo and contains one flow per operator, each demonstrating its typical use with a small bundled sample image. Serves as both a test-drive for new users and a regression check when operators change. Each flow should be minimal — just enough operators to produce a visible, meaningful result (e.g. LoadImage → GaussianBlur for the blur demo, LoadImage → ConvertToGrayscale → Threshold → DetectCircles for the circle demo). The project opens via File → Open and is listed in a "Recent / Featured" section of the Project view. Bundled sample images live under `scripts/OperatorDemo/images/`.

## IPLab.Core

- **`DistortionCalibrationOperator` — auto kernel size and threshold** — when `KernelHalfSize=0` or `MinResponse=0`, derive the value automatically. For kernel size: run an initial pass with a small default (halfSize=7), use K=4 NN median on strict-threshold (0.55) peaks to estimate corner pitch P, then set `halfSize=round(P/2)` and recompute the saddle magnitude. For threshold: use 0.45 as a floor (ranked-threshold approaches caused calibration drift). Main open problem: with `halfSize≈P/2` the saddle blobs of adjacent corners overlap ~50%, weakening some border corners below threshold. An NMS-free approach — find all pixels above threshold, group nearby hits, keep the local max per group — may be more robust. Related: `Undistort_RectifiesCheckerboardToHorizontal` only covers small-tilt images; large-tilt undistortions create wide black borders whose edges generate false saddle responses that confuse InferGrid — auto mode would pick a better threshold adaptively.

- **TemplateMatch: use shared ROI support with angle** — replace the operator's custom
  `RoiCX`/`RoiCY`/`RoiW`/`RoiH` descriptors with `RoiParameters.Schema`, support rotated
  search ROIs via `WarpForRoi`, back-project match rectangles to original-image coordinates,
  and include `RoiParameters.OutputPorts`. Add rotated-ROI and image-edge tests as required
  for ROI-supporting detection operators.

- **Replace `GaussianBlur` with `NoiseFilter` operator** — retire `GaussianBlurOperator` and replace it with a `NoiseFilterOperator` that exposes a `Method` enum (`Gaussian` / `Median`). Gaussian keeps its existing `KernelSize` and `Sigma` parameters (shown always / shown when Method=Gaussian respectively); Median only needs `KernelSize`. Both support ROI via `ApplyImageFilter`. Existing `.ipl` files that reference type `GaussianBlur` will need migration (sed rename + add `Method=Gaussian`).

## IPLab.Core (Architecture)

- **Extract peak-finding into a shared algorithm service** — `TemplateMatchOperator` contains peak-finding logic over a response image (finding the N strongest local maxima above a threshold). Once that branch is merged, evaluate extracting this into a reusable internal helper (e.g. `PeakFinder` in `IPLab.Core.Algorithms`) and wiring it into `DistortionCalibrationOperator` as well, which does the same kind of saddle-response peak extraction. Deduplication also makes it easier to tune NMS radius and threshold strategies in one place.

- **Define a shared policy for operator-owned caches** — `TemplateMatchOperator` is currently an
  exception because it keeps its own decoded-template `Mat` cache in addition to participating in
  the `FlowEx` result cache. Decide whether file-backed resources should use a shared cache service,
  remain operator-owned behind a common interface, or be managed by `FlowEx`. The design must cover
  cache lifetime, thread safety, file-change invalidation, memory limits, and disposal of native
  resources.

- **Formalize operator success/failure signalling** — currently operators signal failure only by throwing an exception. For detection/calibration operators that "found nothing" this is a logical failure, not an error, but there is no typed contract for it. Consider a convention such as a well-known `bool` port (e.g. `Found`) checked by `FlowEx` to mark the node red without treating it as an unhandled exception, or an `IOperatorResult` wrapper that carries both the outputs and a success flag. Either approach should be decided together with the uniform return convention above so the two land as a single interface change.

- **Multiple LoadImage operators in one flow** — the current design assumes a single `LoadImageOperator` as the flow's image source. When multiple loaders exist (e.g. for a two-image comparison flow), the following are unresolved: which loader's thumbnail strip is shown and where; whether switching the active image on one loader triggers a full re-run or only the sub-graph downstream of that loader; and how `FlowEx` resolves execution order when two independent source nodes both feed the same downstream operator. Define the execution and UI model before adding multi-source flows.

## IPLab.Core

- **`RoiDef` as a single connectable port** — currently ROI parameters (XC, YC, Width, Height, Angle) are passed as five separate `object?` outputs between operators (e.g. a `CSharpScript` that builds a ROI from a detected line). Add `RoiDef` as a first-class connectable type: declare one `OutputPortDescriptor` with `DataType = typeof(RoiDef)` and let downstream operators accept it via a single `ConnectableType = typeof(RoiDef)` input parameter. `RoiParameters` should grow a convenience overload that reads the `RoiDef` directly from a wired parameter instead of requiring the five scalar parameters to be spread into `ParameterSchema`. This also allows scripts to output a fully-formed `RoiDef` on a single port rather than spreading it across Out1–Out5.

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

