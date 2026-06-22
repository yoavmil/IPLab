# IPLab.Core – Notes for Claude

This project (`IPLab.Core`) contains all operator implementations, the flow data model, and the runtime executor.
See the root [CLAUDE.md](../CLAUDE.md) for project-level context.

**Rule: whenever a new operator is added, update the operator table in [README.md](README.md) to keep the NuGet-facing docs in sync.**

## XML documentation rules

**Rule: every public type and member must have an XML doc comment (`///`) — the project has `<GenerateDocumentationFile>true</GenerateDocumentationFile>` and treats CS1591 as a warning. Use these conventions:**

- **Classes/interfaces/records/enums:** `<summary>` one sentence describing what it is and does.
- **Positional record parameters:** use `<param>` tags on the record declaration (not on the properties).
- **Interface members:** write the full doc on the interface. Implementing class members use `/// <inheritdoc/>` to inherit it — never duplicate.
- **Operator classes** (`IOperatorType` implementations): write a class-level `<summary>` that names the operation and notes ROI support if applicable. All six interface members (`TypeName`, `Category`, `Icon`, `ParameterSchema`, `OutputPorts`, `Execute`) use `/// <inheritdoc/>`.
- **Operators that wrap a single OpenCV function:** add a `<seealso href="https://docs.opencv.org/4.x/...">OpenCV: functionName</seealso>` tag pointing to the OpenCV 4.x documentation for that function. Use the function-specific anchor (the `#ga…` hash) from `docs.opencv.org/4.x`. For operators that implement a custom algorithm rather than wrapping one function, omit the `<seealso>` link.
- **Utility/static methods that already have summaries:** just ensure the class itself also has a `<summary>`.
- **Do not** write doc comments on `private` or `internal` members.
- **Do not** add `<remarks>`, `<param>`, or `<returns>` unless the information is non-obvious from the signature and summary alone.

## Operator implementation rules

**Rule: whenever a new operator is added to `IPLab.Core.Operators`, add a corresponding entry to [docs/OPERATORS.md](../docs/OPERATORS.md).**

**Rule: whenever a new operator is added, decide explicitly whether it supports ROI and discuss with the user before implementing. If it does, spread-include `RoiParameters.Schema` and `RoiParameters.OutputPorts`, and call `RoiParameters.AddToOutputs`. Filter operators use `ApplyImageFilter`. Detection operators use `Extract`, optional `WarpForRoi`, `Clamp`, `BuildTransform`, and `BackProject` so rotated-ROI results return to original-image coordinates. See [docs/OPERATORS.md#roi](../docs/OPERATORS.md) for the full list of ROI-supporting operators. `TemplateMatchOperator` is a temporary axis-aligned exception tracked in [docs/TODO.md](../docs/TODO.md); do not copy its custom four-parameter schema into new operators.**

**Rule: whenever a new operator is added, its `OutputPorts` must use `IReadOnlyList<OutputPortDescriptor>` with an explicit `DataType` per port (e.g. `typeof(Mat)`, `typeof(CircleSegment[])`, `typeof(int)`). Set `IsDisplayImage = true` on the port the inspector should render as an image (omit or leave `false` on data ports like stats matrices). Script/dynamic operators use `typeof(object)` as a wildcard. Connectable input parameters declare `ConnectableType` — the C# type they accept; `null` (omitted) means not connectable; `typeof(object)` means connectable wildcard. Omit `Type` for wire-only sockets (defaults to `ParameterType.Object` = no UI control). There is no `IsConnectable` flag — connectability is implied by `ConnectableType != null`. See `PortTypeCompat.IsCompatible` for the full compatibility rules.**

**Rule: after any meaningful change to `IPLab.Core` or its tests, run the full test suite (`dotnet test`) and confirm it passes before considering the task done.**

**Rule: every operator that supports ROI must have tests covering the rotated-ROI path (angle ≠ 0), not just axis-aligned. The key invariant to assert: all Mat outputs that represent full-image data (display images, label maps, coordinate arrays) must have the same width/height as the input image, even when a rotated ROI is active. Detection operators must also verify that back-projected coordinates fall within the original image bounds.**

**Rule: when an operator back-projects crop coordinates to image space, always use `rect.Width` / `rect.Height` (the clamped dimensions returned by `RoiParameters.Clamp`) — never `roi.Width` / `roi.Height` (the original unclamped dimensions). This applies to both the centre-row/column passed to `BackProject` and to any perpendicular-extent (`perpHalf`) used to compute line endpoints. When the ROI is partially outside the image, `rect` is smaller than the full ROI; using `roi` dimensions causes the back-projected centre and the line endpoints to land outside the visible image area. Tests must cover ROI-at-edge cases (e.g. `cy=0`, `cy=imageHeight`, and for rotated stripes the equivalent axis).**

**Rule: operators with file-backed parameters that affect execution must implement `ICacheInvalidationProvider` and include stable file metadata in their cache tokens. If decoded file content is also cached inside the operator, key it by full path plus modification time and length, and dispose replaced `Mat` instances. `TemplateMatchOperator` is the reference implementation.**

## .NET Framework 4.8 compatibility

`IPLab.Core` targets `net8.0;net48`. All new code must compile for both TFMs. Key rules:

- **No `Math.Clamp`** — use `Math.Min(max, Math.Max(min, x))` instead.
- **No `float.IsFinite`** — use `!float.IsNaN(x) && !float.IsInfinity(x)` instead.
- **No `MathF`** — PolySharp generates a file-scoped shadow that breaks access from other files on net48. Use `Math.Abs`, `(float)Math.Sqrt(...)`, etc.
- **No `Dictionary<K,V>(IReadOnlyDictionary<K,V>)` constructor** — use `.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)` instead.
- **No `Random.Shuffle`** — the `Polyfill` package provides it as an extension method; the call site is unchanged.
- Language features (records, `init`, `required`, collection expressions, pattern matching) are handled by `PolySharp` and `Polyfill` source generators — no `#if NETFRAMEWORK` guards needed for language features.
- When in doubt, check whether the API exists in .NET Standard 2.0; if it does, it's safe.

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
