using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;
using System.Runtime.InteropServices;

namespace IPLab.Core.Operators;

public class ConnectedComponentsOperator : IOperatorType
{
    public string TypeName  => "ConnectedComponents";
    public string Category  => "Detection";
    public string Icon      => "component";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",            Label = "Image",              ConnectableType = typeof(Mat) },
        new() { Name = "Connectivity",     Label = "Connectivity",       Type = ParameterType.Enum,
                DefaultValue = "8", Options = ["4", "8"] },
        new() { Name = "OutputLabelImage", Label = "Output Label Image", Type = ParameterType.Bool,
                DefaultValue = true },
        ..RoiParameters.Schema,
    ];
    public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "Count",      DataType = typeof(int) },
        new() { Name = "Labels",     DataType = typeof(Mat) },
        new() { Name = "Stats",      DataType = typeof(Mat) },
        new() { Name = "Centroids",  DataType = typeof(Mat) },
        new() { Name = "LabelImage", DataType = typeof(Mat), IsDisplayImage = true },
        ..RoiParameters.OutputPorts,
    ];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image            = (Mat)parameters["Image"]!;
        var connectivity     = (string?)parameters.GetValueOrDefault("Connectivity") ?? "8";
        var outputLabelImage = parameters.GetValueOrDefault("OutputLabelImage") is not false;

        var conn    = connectivity == "4" ? PixelConnectivity.Connectivity4 : PixelConnectivity.Connectivity8;
        var roiRect = RoiParameters.Clamp(parameters, image.Width, image.Height);

        if (roiRect is { Width: <= 0 } or { Height: <= 0 })
        {
            var empty = new Dictionary<string, object?>
            {
                ["Count"]      = 0,
                ["Labels"]     = new Mat(image.Rows, image.Cols, MatType.CV_32S, Scalar.All(0)),
                ["Stats"]      = new Mat(0, 5, MatType.CV_32S),
                ["Centroids"]  = new Mat(0, 2, MatType.CV_64F),
                ["LabelImage"] = outputLabelImage ? new Mat(image.Rows, image.Cols, MatType.CV_8UC3, Scalar.Black) : null,
            };
            RoiParameters.AddToOutputs(empty, parameters);
            return empty;
        }

        var rect    = roiRect ?? default;
        int offsetX = rect.X;
        int offsetY = rect.Y;

        using var crop = roiRect.HasValue ? new Mat(image, rect) : null;
        var       src  = crop ?? image;

        var       labels   = new Mat();
        var       stats    = new Mat();
        var       centroids = new Mat();

        int count = Cv2.ConnectedComponentsWithStats(src, labels, stats, centroids, conn);
        int n     = count - 1; // exclude background label 0

        if (roiRect.HasValue)
        {
            // Translate bounding-box origin (LEFT, TOP) for all rows (including background row 0) directly in the Mat buffer.
            for (int i = 0; i < count; i++)
            {
                nint left = stats.Data + (i * 5 + 0) * sizeof(int);
                nint top  = stats.Data + (i * 5 + 1) * sizeof(int);
                Marshal.WriteInt32(left, Marshal.ReadInt32(left) + offsetX);
                Marshal.WriteInt32(top,  Marshal.ReadInt32(top)  + offsetY);
            }

            // Translate centroids for all rows (including background row 0) directly in the Mat buffer.
            for (int i = 0; i < count; i++)
            {
                nint cx = centroids.Data + (i * 2 + 0) * sizeof(double);
                nint cy = centroids.Data + (i * 2 + 1) * sizeof(double);
                Marshal.WriteInt64(cx, BitConverter.DoubleToInt64Bits(BitConverter.Int64BitsToDouble(Marshal.ReadInt64(cx)) + offsetX));
                Marshal.WriteInt64(cy, BitConverter.DoubleToInt64Bits(BitConverter.Int64BitsToDouble(Marshal.ReadInt64(cy)) + offsetY));
            }
        }

        Mat? labelImg = null;
        if (outputLabelImage)
        {
            var cropLabel = BuildLabelImage(labels, n);
            if (crop is not null)
            {
                labelImg = new Mat(image.Rows, image.Cols, MatType.CV_8UC3, Scalar.Black);
                using var dst = new Mat(labelImg, rect);
                cropLabel.CopyTo(dst);
                cropLabel.Dispose();
            }
            else
            {
                labelImg = cropLabel;
            }
        }

        var outputs = new Dictionary<string, object?>
        {
            ["Count"]      = count,
            ["Labels"]     = labels,
            ["Stats"]      = stats,
            ["Centroids"]  = centroids,
            ["LabelImage"] = labelImg,
        };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }

    private static Mat BuildLabelImage(Mat labels, int componentCount)
    {
        int n = componentCount;

        // Build label→colorByte lookup — O(n) where n = component count.
        // Index 0 = background, stays 0.
        var colorByteOf = new byte[n + 1];
        var ranks       = Enumerable.Range(1, n).ToArray();
        new Random(n).Shuffle(ranks);
        for (int i = 0; i < n; i++)
            colorByteOf[i + 1] = (byte)(ranks[i] * 255 / n);

        // Single pass over all pixels: label int32 → color byte — O(W*H).
        int pixelCount = labels.Rows * labels.Cols;
        var labelArray = new int[pixelCount];
        Marshal.Copy(labels.Data, labelArray, 0, pixelCount);

        var rankArray = new byte[pixelCount];
        for (int i = 0; i < pixelCount; i++)
            rankArray[i] = colorByteOf[labelArray[i]];

        using var rank8 = new Mat(labels.Rows, labels.Cols, MatType.CV_8U);
        Marshal.Copy(rankArray, 0, rank8.Data, pixelCount);

        // Jet: 0=blue (small) → 255=red (large), no wrap-around.
        var colored = new Mat();
        Cv2.ApplyColorMap(rank8, colored, ColormapTypes.Jet);

        // Zero out background (colorByte=0 still gets a Jet color).
        using var bgMask = new Mat();
        Cv2.Compare(labels, Scalar.All(0), bgMask, CmpTypes.EQ);
        colored.SetTo(Scalar.Black, bgMask);

        return colored;
    }
}
