# IPLab TODO

## Docs

- **README.md** — root-level project introduction: what IPLab is, who it's for,
  quick-start (how to build and run), and links to OPERATORS.md

## IPLab (UI project)

- **Introduce a DI container and service layer for inter-ViewModel communication** — currently, callbacks like `onOpenSettings` and `onSelected` are threaded through `MainViewModel → FlowViewModel → BuildNodes → OperatorNodeViewModel` constructors. Replace with an `INodeInteractionService` (or similar) that ViewModels take as a constructor dependency, removing the callback parameters from the chain. This also makes it easier to add new cross-ViewModel interactions without touching intermediate classes.

- **Multi-image input in the image source operator** — extend the `LoadImageOperator` (or add a dedicated `ImageSourceOperator`) to hold a list of image file paths rather than a single path. The operator's result panel should display thumbnails of all listed images beneath the main preview; clicking a thumbnail loads that image as the operator's active output, triggering a full re-run of the flow so all downstream operators update. The parameter editor should allow adding/removing files from the list (e.g. via a file-picker or drag-and-drop). This lets the user quickly toggle between images and visually compare how the same pipeline handles different inputs.

- **Delete connection by clicking on it** — clicking a connection (edge) in the graph view should select and delete it. Nodify's `BaseConnection` supports mouse events; hook `MouseRightButtonDown` or a dedicated delete gesture to remove the corresponding `Dependency` from the `FlowDef` and re-run validation.

- **Prevent invalid connections (e.g. cycles)** — when the user drags a new connection, validate it before committing: reject cycles, self-connections, and any other constraint checked by `FlowDef.Validate()`. If the connection would be invalid, cancel the drop silently (no partial state).

- **Color operator border red on execution failure; show error on click** — when `FlowEx` reports `Failed` status for an operator (e.g. type mismatch — RGB image fed to a grayscale-only input), bind the `NodeViewModel`'s border brush to the operator status so the node turns red. Clicking the red node should surface the exception message — either in the existing result/status panel on the right, or in a small tooltip/popup attached to the node itself — so the user can read what went wrong without leaving the graph view.

- **Image overlay in `RControls.ImageViewer`** — the `ImageViewer` control supports a single `SourceImage`. Add multi-image overlay support so several processed results (or the original + result) can be blended and displayed together. Options: pre-composite via `DrawingVisual`/`RenderTargetBitmap` in the VM, or add a second image layer inside `ImageCanvas` in the RControls library itself.

- **Rename `IPLab` folder and project to `IPLab.UI`** — the folder, `.csproj`, namespace root, and any ProjectReference entries in the solution should all be updated to reflect that this is the WPF UI layer, not the whole product.

## IPLab.Core (Architecture)

- **Add `Name` to `IFlow`; introduce `IProject`** — `IFlow` should carry a unique name so that multiple flows can be loaded simultaneously and addressed by name. Once `IFlow` has a name, wrap it inside an `IProject` interface that owns one or more named flows and serves as the top-level document model for the desktop app (replacing the current single-flow assumption in `MainViewModel`).

## IPLab.Core.Tests



## IPLab.Core

- **Add `CancellationToken` to `FlowEx`**
  `RunAllAsync` and `RunSingleAsync` have no cancellation support. Pass a token
  through to `Task.Run` calls and honour it between operator steps so long-running
  flows can be stopped by the user.

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
