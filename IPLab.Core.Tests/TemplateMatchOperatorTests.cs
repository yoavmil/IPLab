using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class TemplateMatchOperatorTests
{
    [Fact]
    public void Schema_MaximumMatchesIsConnectableAsInt()
    {
        var parameter = new TemplateMatchOperator().ParameterSchema.Single(p => p.Name == "MaxMatches");

        Assert.Equal(typeof(int), parameter.ConnectableType);
    }

    [Theory]
    [InlineData(0.60)]
    [InlineData(0.80)]
    [InlineData(0.95)]
    public void Execute_CheckerboardFixture_FindsEightBySixGrid(double minScore)
    {
        var imagePath = Path.Combine(AppContext.BaseDirectory, "TestImages", "CB_undistorted.bmp");
        var templatePath = Path.Combine(AppContext.BaseDirectory, "TestImages", "CB_Template.png");
        using var image = Cv2.ImRead(imagePath, ImreadModes.Color);

        var result = Execute(image, templatePath, minScore, new Dictionary<string, object?>
        {
            ["MaxMatches"] = 0,
            ["OverlapThreshold"] = 0.3,
        });
        using var matches = (Mat)result["Matches"]!;
        var index = matches.GetGenericIndexer<double>();
        var rectangles = (Rect[])result["Rectangles"]!;

        Assert.Equal(48, rectangles.Length);
        Assert.Equal(48, matches.Rows);
        Assert.Equal(5, matches.Cols);
        Assert.All(Enumerable.Range(0, matches.Rows), row => Assert.InRange(index[row, 4], minScore, 1.0001));

        // The fixture contains 8 horizontal positions and 6 vertical positions. Some coordinates
        // differ by one pixel because the source image was resampled during undistortion.
        var xPositions = ClusterCoordinates(rectangles.Select(r => r.X), tolerance: 2);
        var yPositions = ClusterCoordinates(rectangles.Select(r => r.Y), tolerance: 2);
        Assert.Equal(8, xPositions.Count);
        Assert.Equal(6, yPositions.Count);

        foreach (var x in xPositions)
        foreach (var y in yPositions)
            Assert.Contains(rectangles, r => Math.Abs(r.X - x) <= 2 && Math.Abs(r.Y - y) <= 2);
    }

    [Fact]
    public void Execute_FindsMultipleMatchesAndReturnsMatrixAndRectangles()
    {
        using var template = CreatePattern();
        using var source = new Mat(50, 70, MatType.CV_8UC3, Scalar.Black);
        CopyTemplate(template, source, 8, 10);
        CopyTemplate(template, source, 42, 30);
        var path = WriteTemplate(template);

        try
        {
            var result = Execute(source, path, minScore: 0.999);
            using var matches = (Mat)result["Matches"]!;
            var rectangles = (Rect[])result["Rectangles"]!;

            Assert.Equal(2, matches.Rows);
            Assert.Equal(5, matches.Cols);
            Assert.Equal(MatType.CV_64FC1, matches.Type());
            Assert.Contains(new Rect(8, 10, template.Width, template.Height), rectangles);
            Assert.Contains(new Rect(42, 30, template.Width, template.Height), rectangles);

            var index = matches.GetGenericIndexer<double>();
            Assert.True(index[0, 4] >= index[1, 4]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Execute_UsesAlphaMask()
    {
        using var template = CreatePattern();
        using var source = new Mat(30, 40, MatType.CV_8UC3, Scalar.Black);
        CopyTemplate(template, source, 12, 9);
        using (var changed = new Mat(source, new Rect(12, 9, 3, template.Height)))
            changed.SetTo(new Scalar(255, 0, 255));
        var path = WriteTemplate(template, new Rect(0, 0, 3, template.Height));

        try
        {
            var result = Execute(source, path, minScore: 0.999);
            var rectangles = (Rect[])result["Rectangles"]!;
            Assert.Contains(new Rect(12, 9, template.Width, template.Height), rectangles);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Execute_GrayscaleTemplateFindsMultipleMatches()
    {
        using var template = CreateGrayscalePattern();
        using var source = new Mat(40, 60, MatType.CV_8UC1, Scalar.Black);
        CopyTemplate(template, source, 7, 8);
        CopyTemplate(template, source, 38, 24);
        var path = WriteTemplate(template);

        try
        {
            var result = Execute(source, path, minScore: 0.999);
            var rectangles = (Rect[])result["Rectangles"]!;
            Assert.Equal(2, rectangles.Length);
            Assert.Contains(new Rect(7, 8, template.Width, template.Height), rectangles);
            Assert.Contains(new Rect(38, 24, template.Width, template.Height), rectangles);

            using var encoded = Cv2.ImRead(path, ImreadModes.Unchanged);
            Assert.Equal(MatType.CV_8UC4, encoded.Type());
            var channels = Cv2.Split(encoded);
            try
            {
                using var bgDiff = new Mat();
                using var brDiff = new Mat();
                Cv2.Absdiff(channels[0], channels[1], bgDiff);
                Cv2.Absdiff(channels[0], channels[2], brDiff);
                Assert.Equal(0, Cv2.CountNonZero(bgDiff));
                Assert.Equal(0, Cv2.CountNonZero(brDiff));
            }
            finally
            {
                foreach (var channel in channels) channel.Dispose();
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Execute_RestrictsSearchToAxisAlignedRoi()
    {
        using var template = CreatePattern();
        using var source = new Mat(50, 70, MatType.CV_8UC3, Scalar.Black);
        CopyTemplate(template, source, 8, 10);
        CopyTemplate(template, source, 42, 30);
        var path = WriteTemplate(template);

        try
        {
            var result = Execute(source, path, minScore: 0.999, new Dictionary<string, object?>
            {
                ["RoiCX"] = 50.0, ["RoiCY"] = 35.0, ["RoiW"] = 30.0, ["RoiH"] = 25.0,
            });
            var rectangles = (Rect[])result["Rectangles"]!;
            Assert.Equal([new Rect(42, 30, template.Width, template.Height)], rectangles);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Execute_ThrowsWhenInputTypeDoesNotMatchTemplate()
    {
        using var template = CreatePattern();
        using var source = new Mat(30, 40, MatType.CV_8UC1, Scalar.Black);
        var path = WriteTemplate(template);
        try
        {
            Assert.Throws<ArgumentException>(() => Execute(source, path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Execute_NoMatchesReturnsEmptyMatrixAndRectangleArray()
    {
        using var template = CreatePattern();
        using var source = new Mat(30, 40, MatType.CV_8UC3, new Scalar(5, 10, 15));
        var path = WriteTemplate(template);
        try
        {
            var result = Execute(source, path, minScore: 0.999);
            using var matches = (Mat)result["Matches"]!;
            Assert.Equal(0, matches.Rows);
            Assert.Empty((Rect[])result["Rectangles"]!);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task FlowCache_InvalidatesWhenTemplateFileChanges()
    {
        using var template = CreatePattern();
        using var source = new Mat(30, 40, MatType.CV_8UC3, Scalar.Black);
        CopyTemplate(template, source, 12, 9);
        var path = WriteTemplate(template);
        try
        {
            var op = new TemplateMatchOperator();
            var flow = new FlowDef(
            [
                new Operator
                {
                    Id = "O1", DisplayName = "Match", Type = op,
                    Parameters =
                    [
                        new ParameterValue { Name = "Image", Value = source },
                        new ParameterValue { Name = "TemplatePath", Value = path },
                        new ParameterValue { Name = "MinScore", Value = 0.999 },
                    ],
                    Dependencies = [],
                },
            ]);
            var executor = new FlowEx(flow, enableCaching: true);

            await executor.RunAllAsync();
            var first = executor.IntermediateResults["O1"];

            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));
            await executor.RunAllAsync();
            var second = executor.IntermediateResults["O1"];

            Assert.False(ReferenceEquals(first, second));
        }
        finally { File.Delete(path); }
    }

    private static Dictionary<string, object?> Execute(
        Mat source, string path, double minScore = 0.8,
        IReadOnlyDictionary<string, object?>? extra = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Image"] = source,
            ["TemplatePath"] = path,
            ["MinScore"] = minScore,
            ["MaxMatches"] = 100,
            ["OverlapThreshold"] = 0.3,
        };
        if (extra is not null)
            foreach (var (key, value) in extra) parameters[key] = value;
        return (Dictionary<string, object?>)new TemplateMatchOperator().Execute(parameters)!;
    }

    private static Mat CreatePattern()
    {
        var mat = new Mat(7, 9, MatType.CV_8UC3);
        var random = new Random(12345);
        var index = mat.GetGenericIndexer<Vec3b>();
        for (int row = 0; row < mat.Rows; row++)
        for (int col = 0; col < mat.Cols; col++)
            index[row, col] = new Vec3b(
                (byte)random.Next(20, 236),
                (byte)random.Next(20, 236),
                (byte)random.Next(20, 236));
        Cv2.Line(mat, new Point(0, 0), new Point(8, 0), new Scalar(40, 180, 250), 2);
        Cv2.Line(mat, new Point(8, 0), new Point(8, 6), new Scalar(40, 180, 250), 2);
        return mat;
    }

    private static Mat CreateGrayscalePattern()
    {
        var mat = new Mat(6, 8, MatType.CV_8UC1);
        var random = new Random(54321);
        var index = mat.GetGenericIndexer<byte>();
        for (int row = 0; row < mat.Rows; row++)
        for (int col = 0; col < mat.Cols; col++)
            index[row, col] = (byte)random.Next(20, 236);
        Cv2.Line(mat, new Point(0, 0), new Point(7, 0), Scalar.White, 1);
        Cv2.Line(mat, new Point(7, 0), new Point(7, 5), Scalar.White, 1);
        return mat;
    }

    private static void CopyTemplate(Mat template, Mat target, int x, int y)
    {
        using var destination = new Mat(target, new Rect(x, y, template.Width, template.Height));
        template.CopyTo(destination);
    }

    private static string WriteTemplate(Mat template, Rect? excluded = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"iplab-template-{Guid.NewGuid():N}.png");
        var channels = template.Channels() == 1
            ? new List<Mat> { template.Clone(), template.Clone(), template.Clone() }
            : Cv2.Split(template).ToList();
        using var alpha = new Mat(template.Size(), MatType.CV_8UC1, Scalar.White);
        if (excluded is { } rect)
        {
            using var masked = new Mat(alpha, rect);
            masked.SetTo(Scalar.Black);
        }
        channels.Add(alpha.Clone());
        using var rgba = new Mat();
        Cv2.Merge(channels.ToArray(), rgba);
        foreach (var channel in channels) channel.Dispose();
        Cv2.ImWrite(path, rgba);
        return path;
    }

    private static List<int> ClusterCoordinates(IEnumerable<int> values, int tolerance)
    {
        var clusters = new List<List<int>>();
        foreach (var value in values.Order())
        {
            var cluster = clusters.LastOrDefault();
            if (cluster is null || value - cluster.Average() > tolerance)
                clusters.Add([value]);
            else
                cluster.Add(value);
        }
        return clusters.Select(c => (int)Math.Round(c.Average())).ToList();
    }
}
