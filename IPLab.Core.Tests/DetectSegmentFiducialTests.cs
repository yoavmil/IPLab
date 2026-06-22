using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;
using IPLab.Core.Spatial;
using OpenCvSharp;

namespace IPLab.Core.Tests;

/// <summary>
/// Integration test against a real fiducial image (TestImages/fiducial.png).
/// Two DetectLinearEdge operators scan the bottom edge (angle=270°) and the left edge
/// (angle=180°) of the L-shaped fiducial. Both segments should be found and perpendicular.
/// </summary>
public class DetectSegmentFiducialTests
{
    private static string ImagePath => Path.Combine(
        Path.GetDirectoryName(typeof(DetectSegmentFiducialTests).Assembly.Location)!,
        "TestImages", "fiducial.png");

    private static FlowDef BuildFlow(string imagePath)
    {
        ParameterValue[] segmentParams =
        [
            new() { Name = "Image",          Source = new SourceRef("O2", "Image") },
            new() { Name = "RoiCX",          Value  = 40.0 },
            new() { Name = "RoiCY",          Value  = 40.0 },
            new() { Name = "RoiW",           Value  = 80.0 },
            new() { Name = "RoiH",           Value  = 80.0 },
            new() { Name = "StripeCount",    Value  = 20 },
            new() { Name = "StripeWidth",    Value  = 10 },
            new() { Name = "FilterSize",     Value  = 13 },
            new() { Name = "Threshold",      Value  = "Auto" },
            new() { Name = "Polarity",       Value  = "DarkToBright" },
            new() { Name = "EdgeSelect",     Value  = "Last" },
            new() { Name = "MinScore",       Value  = 0.5 },
        ];

        return new FlowDef(
        [
            new Operator
            {
                Id          = "O1",
                DisplayName = "LoadImage",
                Type        = new LoadImageOperator(),
                Parameters  = [new() { Name = "FilePaths", Value = new string[] { imagePath } }],
                Dependencies = [],
            },
            new Operator
            {
                Id          = "O2",
                DisplayName = "Grayscale",
                Type        = new ConvertToGrayscaleOperator(),
                Parameters  = [
                    new() { Name = "Image",  Source = new SourceRef("O1", "Image") },
                    new() { Name = "Method", Value  = "Luminance" },
                ],
                Dependencies = [new Dependency("D_O1_O2", "O1")],
            },
            new Operator
            {
                Id          = "O3",
                DisplayName = "Bottom edge",
                Type        = new DetectLinearEdgeOperator(),
                Parameters  = [.. segmentParams, new() { Name = "RoiAngle", Value = 270.0 }],
                Dependencies = [new Dependency("D_O2_O3", "O2")],
            },
            new Operator
            {
                Id          = "O4",
                DisplayName = "Left edge",
                Type        = new DetectLinearEdgeOperator(),
                Parameters  = [.. segmentParams, new() { Name = "RoiAngle", Value = 180.0 }],
                Dependencies = [new Dependency("D_O2_O4", "O2")],
            },
        ]);
    }

    [Fact]
    public async Task FiducialSegments_BothFound()
    {
        var (r3, r4) = await RunFlow();
        Assert.True((bool)r3["Found"]!,  "Bottom edge not found");
        Assert.True((bool)r4["Found"]!,  "Left edge not found");
    }

    [Fact]
    public async Task FiducialSegments_ArePerpendicular()
    {
        var (r3, r4) = await RunFlow();
        var (seg3, seg4) = Segments(r3, r4);
        var (d3, d4)     = Directions(seg3, seg4);

        double dot = Math.Abs(d3.x * d4.x + d3.y * d4.y);
        Assert.InRange(dot, 0.0, 0.005); // ≤ 6° from perpendicular
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static async Task<(Dictionary<string, object?> r3, Dictionary<string, object?> r4)> RunFlow()
    {
        var flow     = BuildFlow(ImagePath);
        var executor = new FlowEx(flow);
        await executor.RunAllAsync();
        var r3 = (Dictionary<string, object?>)executor.IntermediateResults["O3"]!;
        var r4 = (Dictionary<string, object?>)executor.IntermediateResults["O4"]!;
        return (r3, r4);
    }

    private static (LineSegment2f, LineSegment2f) Segments(
        Dictionary<string, object?> r3, Dictionary<string, object?> r4) =>
        ((LineSegment2f)r3["Line"]!, (LineSegment2f)r4["Line"]!);

    private static ((double x, double y) d3, (double x, double y) d4) Directions(
        LineSegment2f s3, LineSegment2f s4)
    {
        double dx3 = s3.P2.X - s3.P1.X, dy3 = s3.P2.Y - s3.P1.Y, l3 = Math.Sqrt(dx3*dx3 + dy3*dy3);
        double dx4 = s4.P2.X - s4.P1.X, dy4 = s4.P2.Y - s4.P1.Y, l4 = Math.Sqrt(dx4*dx4 + dy4*dy4);
        return ((dx3/l3, dy3/l3), (dx4/l4, dy4/l4));
    }
}
