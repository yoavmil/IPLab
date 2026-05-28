using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;
using IPLab.Core.Serialization;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class ThinningTests
{
    private static readonly string FlowPath =
        Path.Combine(AppContext.BaseDirectory, "TestFlows", "thinning.ipl");
    private static readonly string OutputPath =
        Path.Combine(AppContext.BaseDirectory, "TestImages", "thinning_result.jpg");

    [Fact]
    public async Task Thinning_Trenches_ProducesSkeletonImage()
    {
        var json     = await File.ReadAllTextAsync(FlowPath);
        var flow     = FlowDefSerializer.Deserialize(json, OperatorRegistry.CreateDefault());
        var executor = new FlowEx(flow.Def);

        await executor.RunAllAsync();

        var failed = executor.Statuses
            .Where(kv => kv.Value != OperatorStatus.Success)
            .Select(kv => $"{kv.Key}={kv.Value}");
        Assert.True(!failed.Any(), "Operators failed: " + string.Join(", ", failed));

        var result = executor.IntermediateResults["O5"] as Dictionary<string, object?>;
        var image  = result?["Image"] as Mat;

        Assert.NotNull(image);
        Assert.False(image.Empty());

        Cv2.ImWrite(OutputPath, image);

        int width  = image.Width;
        int height = image.Height;

        var wrongCount      = new List<string>();
        var isolatedPixels  = new List<string>();

        for (int col = 1; col < width - 1; col++)
        {
            var whiteRows = new List<int>();
            for (int row = 0; row < height; row++)
                if (image.At<byte>(row, col) > 0)
                    whiteRows.Add(row);

            if (whiteRows.Count != 3)
            {
                wrongCount.Add($"col={col}: {whiteRows.Count} white pixels");
                continue;
            }

            foreach (int row in whiteRows)
            {
                bool hasNeighbor = false;
                for (int dr = -1; dr <= 1 && !hasNeighbor; dr++)
                for (int dc = -1; dc <= 1 && !hasNeighbor; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    int nr = row + dr, nc = col + dc;
                    if ((uint)nr < height && (uint)nc < width)
                        hasNeighbor = image.At<byte>(nr, nc) > 0;
                }
                if (!hasNeighbor)
                    isolatedPixels.Add($"({row},{col})");
            }
        }

        Assert.True(wrongCount.Count == 0,
            $"{wrongCount.Count} columns with wrong pixel count (first 10): " +
            string.Join(", ", wrongCount.Take(10)));

        Assert.True(isolatedPixels.Count == 0,
            $"{isolatedPixels.Count} isolated white pixels (first 10): " +
            string.Join(", ", isolatedPixels.Take(10)));
    }
}
