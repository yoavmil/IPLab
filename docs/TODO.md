# IPLab TODO

## Docs

- **README.md** — root-level project introduction: what IPLab is, who it's for,
  quick-start (how to build and run), and links to OPERATORS.md

## IPLab (UI project)

- **Delete connection by clicking on it** — clicking a connection (edge) in the graph view should select and delete it. Nodify's `BaseConnection` supports mouse events; hook `MouseRightButtonDown` or a dedicated delete gesture to remove the corresponding `Dependency` from the `FlowDef` and re-run validation.

- **Prevent invalid connections (e.g. cycles)** — when the user drags a new connection, validate it before committing: reject cycles, self-connections, and any other constraint checked by `FlowDef.Validate()`. If the connection would be invalid, cancel the drop silently (no partial state).

- **Color operator border red on execution failure** — when `FlowEx` reports `Failed` status for an operator (e.g. type mismatch — RGB image fed to a grayscale-only input), bind the `NodeViewModel`'s border brush to the operator status so the node turns red. The failure message from the exception should remain accessible in the existing status/error display.

- **Image overlay in `RControls.ImageViewer`** — the `ImageViewer` control supports a single `SourceImage`. Add multi-image overlay support so several processed results (or the original + result) can be blended and displayed together. Options: pre-composite via `DrawingVisual`/`RenderTargetBitmap` in the VM, or add a second image layer inside `ImageCanvas` in the RControls library itself.

- **Rename `IPLab` folder and project to `IPLab.UI`** — the folder, `.csproj`, namespace root, and any ProjectReference entries in the solution should all be updated to reflect that this is the WPF UI layer, not the whole product.

## IPLab.Core.Tests

- **Test: `ConvertToGrayscale` throws when input is already grayscale**
  Both `Luminance` and `HsvValue` paths call `CvtColor` with a 3-channel code, so
  passing a single-channel `Mat` will throw an OpenCV exception at runtime.
  Test should assert that `FlowEx.RunAllAsync()` throws (or sets status `Failed`)
  when a grayscale image is wired into `ConvertToGrayscale`.

- **Test: circular dependency is caught by `Validate()`**
  Build a flow with a cycle (e.g. O1 → O2 → O3 → O1) and assert that
  `Validate()` returns an invalid result containing a circular dependency error.

- **Test: wired parameter without a declared dependency is caught by `Validate()`**
  Build a flow where a parameter `Source` points to an operator that is not listed
  in the operator's `Dependencies`, and assert that `Validate()` catches the mismatch.

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
