# IPLab TODO

## Docs

- **README.md** — root-level project introduction: what IPLab is, who it's for,
  quick-start (how to build and run), and links to OPERATORS.md

## IPLab (UI project)

- **Ribbon polish and unified dark theme** — the current `RibbonControl` is a plain `ToolBarTray` with no visual styling. Replace it with a properly styled ribbon: grouped sections with icons, separator lines, and hover/press states. Adopt the Nodify built-in dark theme (merge its `ResourceDictionary` into `App.xaml`) so that the node editor, ribbon, panels, and all standard WPF controls share the same dark palette and accent color rather than each being styled ad-hoc.

- **Introduce a DI container and service layer for inter-ViewModel communication** — currently, callbacks like `onOpenSettings` and `onSelected` are threaded through `MainViewModel → FlowViewModel → BuildNodes → OperatorNodeViewModel` constructors. Replace with an `INodeInteractionService` (or similar) that ViewModels take as a constructor dependency, removing the callback parameters from the chain. This also makes it easier to add new cross-ViewModel interactions without touching intermediate classes.

- **Multi-image input in the image source operator** — extend the `LoadImageOperator` (or add a dedicated `ImageSourceOperator`) to hold a list of image file paths rather than a single path. The operator's result panel should display thumbnails of all listed images beneath the main preview; clicking a thumbnail loads that image as the operator's active output, triggering a full re-run of the flow so all downstream operators update. The parameter editor should allow adding/removing files from the list (e.g. via a file-picker or drag-and-drop). This lets the user quickly toggle between images and visually compare how the same pipeline handles different inputs.

- **Output display settings per operator** — let the user configure how detection results are visualised in the inspector. For annotation color, offer three modes: a single fixed color (color-picker), a random-per-entity color (stable hash of entity index so colors don't shuffle on re-run), and a heatmap (map a scalar — e.g. circle radius or blob response — to a gradient). Store the chosen mode and parameters inside the operator's display metadata so settings persist with the saved flow. Start with circle/blob annotations; apply the same system to any future operator that produces non-image output.

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
