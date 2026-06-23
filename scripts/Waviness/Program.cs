using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;
using IPLab.Core.Serialization;
using OpenCvSharp;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: Waviness <image-path|folder>");
    return 1;
}

var input = args[0];
HashSet<string> imageExts = new(StringComparer.OrdinalIgnoreCase)
    { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff" };

string[] imagePaths;
if (Directory.Exists(input))
{
    imagePaths = Directory.GetFiles(input)
        .Where(f => imageExts.Contains(Path.GetExtension(f))
                 && !Path.GetFileNameWithoutExtension(f).Contains("debug", StringComparison.OrdinalIgnoreCase))
        .OrderBy(f => f)
        .ToArray();
    if (imagePaths.Length == 0)
    {
        Console.Error.WriteLine($"No images found in folder: {input}");
        return 1;
    }
}
else if (File.Exists(input))
{
    if (Path.GetFileNameWithoutExtension(input).Contains("debug", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine($"Skipping debug image: {input}");
        return 1;
    }
    imagePaths = [input];
}
else
{
    Console.Error.WriteLine($"Path not found: {input}");
    return 1;
}

var flowPath = Path.Combine(AppContext.BaseDirectory, "thinning.ipl");
var json     = await File.ReadAllTextAsync(flowPath);

const int minLineLen = 40;

int failCount = 0;
var results   = new (string Name, int LineCount, double Waviness)[imagePaths.Length];

await Parallel.ForEachAsync(
    Enumerable.Range(0, imagePaths.Length),
    new ParallelOptions { MaxDegreeOfParallelism = 4 },
    async (i, _) =>
    {
        var imagePath = imagePaths[i];
        Console.WriteLine($"{imagePath} ({i + 1}/{imagePaths.Length})");

        // Each task gets its own flow instance — the flow object is mutated per image.
        var flow           = FlowDefSerializer.Deserialize(json, OperatorRegistry.CreateDefault());
        var loadImg        = flow.Def.Operators.First(o => o.Type.TypeName == "LoadImage");
        var filePathsParam = loadImg.Parameters.FirstOrDefault(p => p.Name == "FilePaths");
        var filePathParam  = loadImg.Parameters.FirstOrDefault(p => p.Name == "FilePath");

        if (filePathsParam is not null)
            filePathsParam.Value = new string[] { imagePath };
        else
            filePathParam!.Value = imagePath;

        var executor = new FlowEx(flow.Def);
        await executor.RunAllAsync(CancellationToken.None);

        if (executor.Statuses.Values.Any(s => s != OperatorStatus.Success))
            Interlocked.Increment(ref failCount);

        var thinning = flow.Def.Operators.First(o => o.Type.TypeName == "Thinning");
        var result   = executor.IntermediateResults[thinning.Id] as Dictionary<string, object?>;
        if (result?["Image"] is not Mat image || image.Empty())
        {
            Console.Error.WriteLine($"Thinning produced no image for {imagePath}.");
            Interlocked.Increment(ref failCount);
            results[i] = (Path.GetFileName(imagePath), 0, double.NaN);
            return;
        }

        // Cluster skeleton pixels via connected components (8-connectivity).
        // Any gap in the skeleton produces a separate component — no direction assumptions.
        Mat labels = new(), stats = new(), centroids = new();
        Cv2.ConnectedComponentsWithStats(image, labels, stats, centroids);

        Dictionary<int, List<Point>> clusters = [];
        for (int r = 0; r < labels.Rows; r++)
            for (int c = 0; c < labels.Cols; c++)
            {
                int lbl = labels.At<int>(r, c);
                if (lbl == 0) continue;  // background
                if (!clusters.TryGetValue(lbl, out var pts)) clusters[lbl] = pts = [];
                pts.Add(new Point(c, r));
            }

        var lines = clusters.Values.Where(l => l.Count >= minLineLen).ToList();

        // Layer 1: original image as background.
        var originalId  = flow.Def.Operators.First(o => o.Type.TypeName == "LoadImage").Id;
        var loadOutputs = executor.IntermediateResults[originalId] as Dictionary<string, object?>;
        var debug       = (loadOutputs?["Image"] as Mat)!.Clone();

        // Layer 2: skeleton pixels in green.
        for (int r = 0; r < image.Rows; r++)
            for (int c = 0; c < image.Cols; c++)
                if (image.At<byte>(r, c) > 0)
                    debug.Set(r, c, new Vec3b(0, 255, 0));

        double total_distance = 0;
        int    total_pixels   = 0;

        // Layer 3: per-segment PCA line fit, drawn in red.
        // Cv2.FitLine L2 minimizes sum of squared perpendicular distances — works at any angle.
        foreach (var pts in lines)
        {
            if (pts.Count < 2) continue;

            var fitOut = new Mat();
            Cv2.FitLine(InputArray.Create(pts.Select(p => new Point2f(p.X, p.Y)).ToArray()),
                        fitOut, DistanceTypes.L2, 0, 0.01, 0.01);

            float vx = fitOut.At<float>(0);
            float vy = fitOut.At<float>(1);
            float x0 = fitOut.At<float>(2);
            float y0 = fitOut.At<float>(3);

            // Project all points onto the fit direction to find segment endpoints.
            double tMin = double.MaxValue, tMax = double.MinValue;
            foreach (var pt in pts)
            {
                double t = (pt.X - x0) * vx + (pt.Y - y0) * vy;
                if (t < tMin) tMin = t;
                if (t > tMax) tMax = t;
            }
            var p1 = new Point((int)Math.Round(x0 + tMin * vx), (int)Math.Round(y0 + tMin * vy));
            var p2 = new Point((int)Math.Round(x0 + tMax * vx), (int)Math.Round(y0 + tMax * vy));
            
            foreach (var pt in pts)
            {
                total_distance += DistancePointToSegment(pt, p1, p2);
                total_pixels++;
            }

            Cv2.Line(debug, p1, p2, Scalar.Red, 2);
        }

        double waviness = total_pixels > 0 ? total_distance / total_pixels : double.NaN;

        var resultsDir = Path.Combine(Path.GetDirectoryName(imagePath)!, "results");
        Directory.CreateDirectory(resultsDir);

        string wavinessLabel = double.IsNaN(waviness) ? "Waviness: N/A" : $"Waviness: {waviness:F4}";
        Cv2.PutText(debug, wavinessLabel, new Point(10, 30),
                    HersheyFonts.HersheySimplex, 1.0, Scalar.White, 2, LineTypes.AntiAlias);

        var debugPath = Path.Combine(resultsDir, Path.GetFileNameWithoutExtension(imagePath) + "_debug.jpg");
        Cv2.ImWrite(debugPath, debug);
        results[i] = (Path.GetFileName(imagePath), lines.Count, waviness);
    }
);

// Summary table
Console.WriteLine();
int nameW     = Math.Max("Name".Length,     results.Max(r => r.Name.Length));
int linesW    = Math.Max("# Lines".Length,  results.Max(r => r.LineCount.ToString().Length));
int wavinessW = Math.Max("Waviness".Length, results.Max(r => double.IsNaN(r.Waviness) ? 3 : r.Waviness.ToString("F4").Length));

string Header(string name, int w) => name.PadRight(w);
string Sep(int w) => new('-', w);

Console.WriteLine($"{Header("Name", nameW)} | {Header("# Lines", linesW)} | {Header("Waviness", wavinessW)}");
Console.WriteLine($"{Sep(nameW)}-|-{Sep(linesW)}-|-{Sep(wavinessW)}");

foreach (var (name, lineCount, waviness) in results)
{
    string wStr = double.IsNaN(waviness) ? "N/A" : waviness.ToString("F4");
    Console.WriteLine($"{name.PadRight(nameW)} | {lineCount.ToString().PadLeft(linesW)} | {wStr.PadLeft(wavinessW)}");
}

return failCount > 0 ? 1 : 0;

static double DistancePointToSegment(Point pt, Point p1, Point p2)
{
    double dx = p2.X - p1.X;
    double dy = p2.Y - p1.Y;
    double lenSq = dx * dx + dy * dy;
    if (lenSq < 1e-10)
        return Math.Sqrt((pt.X - p1.X) * (double)(pt.X - p1.X) + (pt.Y - p1.Y) * (double)(pt.Y - p1.Y));
    double t = Math.Clamp(((pt.X - p1.X) * dx + (pt.Y - p1.Y) * dy) / lenSq, 0.0, 1.0);
    double cx = p1.X + t * dx;
    double cy = p1.Y + t * dy;
    return Math.Sqrt((pt.X - cx) * (pt.X - cx) + (pt.Y - cy) * (pt.Y - cy));
}
