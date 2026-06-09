# IPLab.Core – Notes for Claude

This project (`IPLab.Core`) contains all operator implementations, the flow data model, and the runtime executor.
See the root [CLAUDE.md](../CLAUDE.md) for project-level context.

**Rule: whenever a new operator is added, update the operator table in [README.md](README.md) to keep the NuGet-facing docs in sync.**

## Operator implementation rules

**Rule: whenever a new operator is added to `IPLab.Core.Operators`, add a corresponding entry to [docs/OPERATORS.md](../docs/OPERATORS.md).**

**Rule: whenever a new operator is added, decide explicitly whether it supports ROI and discuss with the user before implementing. If it does, follow the pattern in `RoiParameters` — filter operators use `ApplyImageFilter`, detection operators use `Clamp` + coordinate translation. See [docs/OPERATORS.md#roi](../docs/OPERATORS.md) for the full list of ROI-supporting operators.**

**Rule: whenever a new operator is added, its `OutputPorts` must use `IReadOnlyList<OutputPortDescriptor>` with an explicit `DataType` per port (e.g. `typeof(Mat)`, `typeof(CircleSegment[])`, `typeof(int)`). Set `IsDisplayImage = true` on the port the inspector should render as an image (omit or leave `false` on data ports like stats matrices). Script/dynamic operators use `typeof(object)` as a wildcard. Connectable input parameters declare `ConnectableType` — the C# type they accept; `null` (omitted) means not connectable; `typeof(object)` means connectable wildcard. Omit `Type` for wire-only sockets (defaults to `ParameterType.Object` = no UI control). There is no `IsConnectable` flag — connectability is implied by `ConnectableType != null`. See `PortTypeCompat.IsCompatible` for the full compatibility rules.**

**Rule: after any meaningful change to `IPLab.Core` or its tests, run the full test suite (`dotnet test`) and confirm it passes before considering the task done.**

**Rule: every operator that supports ROI must have tests covering the rotated-ROI path (angle ≠ 0), not just axis-aligned. The key invariant to assert: all Mat outputs that represent full-image data (display images, label maps, coordinate arrays) must have the same width/height as the input image, even when a rotated ROI is active. Detection operators must also verify that back-projected coordinates fall within the original image bounds.**

## OpenCV usage patterns

### Prefer bulk OpenCV operations over C# loops

OpenCV's native functions are compiled with SIMD intrinsics (SSE/AVX/NEON) and run as optimised native code. Always prefer them over hand-written C# loops for operations on whole Mats.

| Instead of… | Use… |
|---|---|
| Manual column-average double loop | `Cv2.Reduce(mat, dst, ReduceDimension.Row, ReduceTypes.Avg, MatType.CV_64F)` |
| Manual box-filter / convolution loop | `Cv2.Filter2D(src, dst, dtype, kernel, anchor, borderType)` |
| Manual per-pixel arithmetic | `Cv2.Subtract`, `Cv2.Multiply`, `Cv2.Threshold`, etc. |

### Element-wise access in C# loops: arrays beat `Mat.At<T>()`

`Mat.At<T>()` called per-iteration in a C# loop carries managed overhead on every call and is typically slower than a plain `double[]`. When C# logic must iterate element-by-element:

1. **Bulk operation on the whole Mat** — OpenCV function (SIMD path).
2. **Extract to a C# array** — then loop over the array with no overhead.

`GetGenericIndexer<T>()` is an acceptable middle ground: it uses direct pointer arithmetic and is roughly as fast as array access, without a full extraction copy.

Never call `Mat.At<T>()` inside a hot loop.
