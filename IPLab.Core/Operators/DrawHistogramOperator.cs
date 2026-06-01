using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class DrawHistogramOperator : IOperatorType
{
    public string TypeName  => "DrawHistogram";
    public string Category  => "Visualization";
    public string Icon      => "histogram";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",  Label = "Image",  ConnectableType = typeof(Mat) },
        new() { Name = "Height", Label = "Height", Type = ParameterType.Int,DefaultValue = 200, Min = 64.0,  Max = 1024.0 },
        new() { Name = "Width",  Label = "Width",  Type = ParameterType.Int,DefaultValue = 512, Min = 64.0,  Max = 2048.0 },
        new() { Name = "Color",  Label = "Color",  Type = ParameterType.Enum,
                DefaultValue = "Green", Options = ["White", "Green", "Cyan", "Red", "Yellow"] },
    ];
    public IReadOnlyList<OutputPortDescriptor> OutputPorts => [new() { Name = "Image", DataType = typeof(Mat) }];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image  = (Mat)parameters["Image"]!;
        var height = Convert.ToInt32(parameters.GetValueOrDefault("Height") ?? 200);
        var width  = Convert.ToInt32(parameters.GetValueOrDefault("Width")  ?? 512);
        var color  = parameters.GetValueOrDefault("Color") as string ?? "Green";

        if (image.Channels() != 1)
            throw new ArgumentException("DrawHistogram requires a single-channel (grayscale) image.");

        var barColor = color switch
        {
            "White"  => Scalar.White,
            "Cyan"   => new Scalar(255, 255, 0),
            "Red"    => new Scalar(0, 0, 255),
            "Yellow" => new Scalar(0, 255, 255),
            _        => new Scalar(0, 200, 0), // Green
        };

        using var hist = new Mat();
        Cv2.CalcHist([image], [0], null, hist, 1, [256], [[0f, 256f]]);
        Cv2.Normalize(hist, hist, 0, height, NormTypes.MinMax);

        var canvas = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);
        for (int i = 0; i < 256; i++)
        {
            int barH = (int)hist.At<float>(i);
            int x0   = i * width / 256;
            int x1   = (i + 1) * width / 256;
            if (x1 <= x0) x1 = x0 + 1;
            Cv2.Rectangle(canvas, new Point(x0, height - barH), new Point(x1, height), barColor, -1);
        }

        return canvas;
    }
}
