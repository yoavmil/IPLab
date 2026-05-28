using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;
using IPLab.Core.Serialization;
using MathNet.Numerics;
using OpenCvSharp;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: Waviness <image-path>");
    return 1;
}

var imagePath = args[0];
if (!File.Exists(imagePath))
{
    Console.Error.WriteLine($"Image not found: {imagePath}");
    return 1;
}

var flowPath = Path.Combine(AppContext.BaseDirectory, "thinning.ipl");
var json     = await File.ReadAllTextAsync(flowPath);
var flow     = FlowDefSerializer.Deserialize(json, OperatorRegistry.CreateDefault());

// Override the LoadImage FilePath with the command-line argument
var loadImg = flow.Def.Operators.First(o => o.Type.TypeName == "LoadImage");
loadImg.Parameters.First(p => p.Name == "FilePath").Value = imagePath;

var executor = new FlowEx(flow.Def);
await executor.RunAllAsync();

foreach (var (id, status) in executor.Statuses.OrderBy(kv => kv.Key))
    Console.WriteLine($"{id}: {status}");

var thinning = flow.Def.Operators.First(o => o.Type.TypeName == "Thinning");
var result   = executor.IntermediateResults[thinning.Id] as Dictionary<string, object?>;
var image    = result?["Image"] as Mat;

if (image == null || image.Empty())
{
    Console.Error.WriteLine("Thinning produced no image.");
    return 1;
}

// Cluster white skeleton pixels into lines by scanning column by column.
// A pixel joins an existing line if its row is within 2 of that line's last point.
List<List<Point>> lines = new();
for (int c = 1; c < image.Cols - 1; c++)
{
    for (int r = 0; r < image.Height; r++)
    {
        if (image.At<byte>(r, c) > 0)
        {
            var line = lines.FirstOrDefault(l => Math.Abs(l.Last().Y - r) < 2);
            if (line == null)
                lines.Add(new List<Point> { new Point(c, r) });
            else
                line.Add(new Point(c, r));
        }
    }
}

// Layer 1: original image as background.
var originalId = flow.Def.Operators.First(o => o.Type.TypeName == "LoadImage").Id;
var debug      = (executor.IntermediateResults[originalId] as Mat)!.Clone();

// Layer 2: skeleton pixels in green.
for (int r = 0; r < image.Rows; r++)
    for (int c = 1; c < image.Cols-1; c++)
        if (image.At<byte>(r, c) > 0)
            debug.Set(r, c, new Vec3b(0, 255, 0));

double total_distance = 0;
int total_pixels = 0;

// Layer 3: per-line regression, drawn in red.
foreach (var pts in lines)
{
    if (pts.Count < 2) continue;

    double[] xs = pts.Select(p => (double)p.X).ToArray();
    double[] ys = pts.Select(p => (double)p.Y).ToArray();
    (double b, double m) = Fit.Line(xs, ys);

    foreach (var pt in pts)
    {
        double lin_reg_y = m*pt.X + b;
        total_distance += Math.Abs(lin_reg_y - pt.Y);
        total_pixels++;
    }

    int xMin = pts.Min(p => p.X);
    int xMax = pts.Max(p => p.X);
    var p1 = new Point(xMin, (int)Math.Round(m * xMin + b));
    var p2 = new Point(xMax, (int)Math.Round(m * xMax + b));

    Cv2.Line(debug, p1, p2, Scalar.Red, 2);
}

var debugPath = Path.ChangeExtension(imagePath, null) + "_debug.jpg";
Cv2.ImWrite(debugPath, debug);
Console.WriteLine($"Lines found: {lines.Count}");
Console.WriteLine($"Saved: {debugPath}");
Console.WriteLine($"Waviness {total_distance/total_pixels}");

return executor.Statuses.Values.Any(s => s != OperatorStatus.Success) ? 1 : 0;
