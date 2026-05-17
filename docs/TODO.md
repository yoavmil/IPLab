# IPLab TODO

## Docs

- **README.md** — root-level project introduction: what IPLab is, who it's for,
  quick-start (how to build and run), and links to OPERATORS.md

## IPLab.Core.Tests

- **Test: circular dependency is caught by `Validate()`**
  Build a flow with a cycle (e.g. O1 → O2 → O3 → O1) and assert that
  `Validate()` returns an invalid result containing a circular dependency error.

- **Test: wired parameter without a declared dependency is caught by `Validate()`**
  Build a flow where a parameter `Source` points to an operator that is not listed
  in the operator's `Dependencies`, and assert that `Validate()` catches the mismatch.

## IPLab.Core

- **Blob detection operator** — implement `DetectBlobsOperator` using `SimpleBlobDetector`
  with circularity filtering once the correct OpenCvSharp API is confirmed.
  Mirror the `ChannelCircleTests` test structure for the blob version.

- **Type-safe output ports on `IOperatorType`**
  Currently `OutputPorts` is `IReadOnlyList<string>` (names only). Each port should
  also declare its data type (e.g. `Mat`, `CircleSegment[]`, `int`) so that
  `FlowDef.Validate()` can verify that a wired `ParameterValue.Source` port type
  is compatible with the target `ParameterDescriptor` type — the same way input
  parameters are already typed via `ParameterDescriptor`.
