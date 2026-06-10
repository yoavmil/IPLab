# IPLab.Core

The core library for [IPLab](https://github.com/yoavmil/IPLab) — a C# image-processing pipeline engine built on [OpenCvSharp4](https://github.com/shimat/opencvsharp).

It provides:
- Built-in image-processing **operators** (load, blur, threshold, detect circles, find contours, etc.)
- A **flow data model** (`FlowDef`) for defining operator graphs as JSON
- A **runtime executor** (`FlowEx`) that resolves dependencies, runs operators in topological order, and stores intermediate results

## Install

```
dotnet add package IPLab.Core
```

## Quick start

```csharp
using IPLab.Core;

// Load a saved flow from JSON
var flow = FlowDefSerializer.Deserialize(File.ReadAllText("my-flow.ipl"));

// Execute it
var executor = new FlowEx(flow);
await executor.RunAllAsync(CancellationToken.None);

// Read a result
var image = executor.IntermediateResults["O1"]["Image"] as Mat;
```

## Building flows in code

See [`scripts/Waviness/`](../scripts/Waviness/) for a complete example: it defines and runs an IPLab flow entirely from C# code, executes it via `FlowEx`, and reads the intermediate results.

## Built-in operators

| Category | Operators |
|---|---|
| I/O | LoadImage, SaveImage |
| Color | ConvertToGrayscale, SplitChannels, InvertImage |
| Filters | Threshold, HistogramEqualization, GaussianBlur, Morphology, Thinning, Bitwise |
| Visualization | DrawHistogram |
| Detection | DetectCircles, DetectSimpleBlobs, ConnectedComponents, FindContours, FindStripeEdges |
| Scripting | CSharpScript |

Full documentation: [docs/OPERATORS.md](https://github.com/yoavmil/IPLab/blob/master/docs/OPERATORS.md)

## Dependencies

- [OpenCvSharp4.Windows](https://www.nuget.org/packages/OpenCvSharp4.Windows) — OpenCV bindings for .NET
- [NetTopologySuite](https://www.nuget.org/packages/NetTopologySuite) — geometry primitives
- [Microsoft.CodeAnalysis.CSharp.Scripting](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp.Scripting) — Roslyn scripting for the CSharpScript operator
