using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;
using IPLab.Core.Serialization;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class SerializationTests
{
    private static readonly string ImagePath =
        Path.Combine(AppContext.BaseDirectory, "TestImages", "RGBCircles.png");

    [Fact]
    public void RoiParams_WithValues_SurviveRoundTrip()
    {
        var flowDef = new FlowDef(
        [
            new Operator
            {
                Id           = "O1",
                DisplayName  = "Morph",
                Type         = new MorphologyOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "RoiX", Value = 10 },
                    new ParameterValue { Name = "RoiY", Value = 20 },
                    new ParameterValue { Name = "RoiW", Value = 50 },
                    new ParameterValue { Name = "RoiH", Value = 40 },
                ],
                Dependencies = []
            }
        ]);

        var json     = FlowDefSerializer.Serialize(new Flow(flowDef, new FlowLayout([], [])));
        var restored = FlowDefSerializer.Deserialize(json, OperatorRegistry.CreateDefault());

        var p = restored.Def.Operators.Single().Parameters.ToDictionary(v => v.Name);
        Assert.Equal(10, Convert.ToInt32(p["RoiX"].Value));
        Assert.Equal(20, Convert.ToInt32(p["RoiY"].Value));
        Assert.Equal(50, Convert.ToInt32(p["RoiW"].Value));
        Assert.Equal(40, Convert.ToInt32(p["RoiH"].Value));
    }

    [Fact]
    public void RoiParams_Absent_ExtractReturnsNull()
    {
        var flowDef = new FlowDef(
        [
            new Operator
            {
                Id           = "O1",
                DisplayName  = "Morph",
                Type         = new MorphologyOperator(),
                Parameters   = [], // no ROI params
                Dependencies = []
            }
        ]);

        var json     = FlowDefSerializer.Serialize(new Flow(flowDef, new FlowLayout([], [])));
        var restored = FlowDefSerializer.Deserialize(json, OperatorRegistry.CreateDefault());

        var paramDict = restored.Def.Operators.Single().Parameters
            .ToDictionary(v => v.Name, v => v.Value);
        Assert.Null(RoiParameters.Extract(paramDict!));
    }

    [Fact]
    public void Deserialize_EmptyString_Throws()
    {
        Assert.Throws<System.Text.Json.JsonException>(
            () => FlowDefSerializer.Deserialize(string.Empty, OperatorRegistry.CreateDefault()));
    }

    [Fact]
    public void Deserialize_EmptyObject_ReturnsEmptyFlow()
    {
        var flow = FlowDefSerializer.Deserialize("{}", OperatorRegistry.CreateDefault());
        Assert.Empty(flow.Def.Operators);
        Assert.Empty(flow.Layout.Operators);
        Assert.Empty(flow.Layout.Dependencies);
    }

    [Fact]
    public async Task FlowRoundTrip_HsvGrayscale_DetectsAllCircles()
    {
        using var probe = Cv2.ImRead(ImagePath, ImreadModes.Color);
        var minDist = probe.Width / 10.0;

        // Build flow: Load → ConvertToGrayscale(HsvValue) → Threshold → DetectCircles
        var flowDef = new FlowDef(
        [
            new Operator
            {
                Id           = "O1",
                DisplayName  = "Load",
                Type         = new LoadImageOperator(),
                Parameters   = [new ParameterValue { Name = "FilePath", Value = ImagePath }],
                Dependencies = []
            },
            new Operator
            {
                Id           = "O2",
                DisplayName  = "ToGray",
                Type         = new ConvertToGrayscaleOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",  Source = new SourceRef("O1", "Image") },
                    new ParameterValue { Name = "Method", Value  = "HsvValue" }
                ],
                Dependencies = [new Dependency("D_O1_O2", "O1")]
            },
            new Operator
            {
                Id           = "O3",
                DisplayName  = "Thresh",
                Type         = new ThresholdOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",  Source = new SourceRef("O2", "Image") },
                    new ParameterValue { Name = "Thresh", Value  = 128.0 }
                ],
                Dependencies = [new Dependency("D_O2_O3", "O2")]
            },
            new Operator
            {
                Id           = "O4",
                DisplayName  = "Detect",
                Type         = new DetectCirclesOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",     Source = new SourceRef("O3", "Image") },
                    new ParameterValue { Name = "MinDist",   Value  = minDist },
                    new ParameterValue { Name = "Param1",    Value  = 150.0   },
                    new ParameterValue { Name = "Param2",    Value  = 10.0    },
                    new ParameterValue { Name = "MinRadius", Value  = 30      },
                    new ParameterValue { Name = "MaxRadius", Value  = 50      }
                ],
                Dependencies = [new Dependency("D_O3_O4", "O3")]
            }
        ]);

        // Non-default positions and sides to make the round-trip checks meaningful
        var layout = new FlowLayout(
            operators:
            [
                new OperatorLayout("O1", new LayoutPoint(40,  40)),
                new OperatorLayout("O2", new LayoutPoint(40,  220)),
                new OperatorLayout("O3", new LayoutPoint(280, 400)),
                new OperatorLayout("O4", new LayoutPoint(140, 580)),
            ],
            dependencies:
            [
                new DependencyLayout("D_O1_O2", ConnectionSide.Bottom, ConnectionSide.Top),
                new DependencyLayout("D_O2_O3", ConnectionSide.Right,  ConnectionSide.Left),
                new DependencyLayout("D_O3_O4", ConnectionSide.Bottom, ConnectionSide.Top),
            ]);

        // Round-trip through JSON
        var json      = FlowDefSerializer.Serialize(new Flow(flowDef, layout));
        var restored  = FlowDefSerializer.Deserialize(json, OperatorRegistry.CreateDefault());

        // ── Execution check ──────────────────────────────────────────────────
        var executor = new FlowEx(restored.Def);
        await executor.RunAllAsync();

        var circles = (CircleSegment[])executor.IntermediateResults["O4"]!;
        Assert.Equal(8, circles.Length); // 2 red + 3 green + 3 blue

        // ── Layout round-trip check ──────────────────────────────────────────
        var restoredOps  = restored.Layout.Operators.ToDictionary(o => o.OperatorId);
        var restoredDeps = restored.Layout.Dependencies.ToDictionary(d => d.DependencyId);

        Assert.Equal(layout.Operators.Count,    restoredOps.Count);
        Assert.Equal(layout.Dependencies.Count, restoredDeps.Count);

        foreach (var orig in layout.Operators)
        {
            Assert.True(restoredOps.TryGetValue(orig.OperatorId, out var r));
            Assert.Equal(orig.Position.X, r.Position.X);
            Assert.Equal(orig.Position.Y, r.Position.Y);
        }

        foreach (var orig in layout.Dependencies)
        {
            Assert.True(restoredDeps.TryGetValue(orig.DependencyId, out var r));
            Assert.Equal(orig.SourceSide, r.SourceSide);
            Assert.Equal(orig.TargetSide, r.TargetSide);
        }
    }
}
