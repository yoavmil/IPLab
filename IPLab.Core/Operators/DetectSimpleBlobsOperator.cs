using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class DetectSimpleBlobsOperator : IOperatorType
{
    public string TypeName  => "DetectSimpleBlobs";
    public string Category  => "Detection";
    public string Icon      => "blob";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",              Label = "Image",                Type = ParameterType.Object, IsConnectable = true  },
        new() { Name = "Polarity",           Label = "Polarity",             Type = ParameterType.Enum,   IsConnectable = false,
                DefaultValue = "Light on Dark", Options = ["Light on Dark", "Dark on Light"] },
        new() { Name = "MinCircularity",     Label = "Min Circularity",      Type = ParameterType.Double, IsConnectable = false, DefaultValue = 0.8,   Min = 0.0, Max = 1.0 },
        new() { Name = "MinArea",            Label = "Min Area (px²)",       Type = ParameterType.Double, IsConnectable = false, DefaultValue = 50.0   },
        new() { Name = "MaxArea",            Label = "Max Area (px²)",       Type = ParameterType.Double, IsConnectable = false, DefaultValue = 50000.0 },
        new() { Name = "MinDistBetweenBlobs",Label = "Min Dist Between Blobs",Type = ParameterType.Double, IsConnectable = false, DefaultValue = 10.0  },
        new() { Name = "MinThreshold",       Label = "Min Threshold",         Type = ParameterType.Double, IsConnectable = false, DefaultValue = 100.0 },
        new() { Name = "MaxThreshold",       Label = "Max Threshold",         Type = ParameterType.Double, IsConnectable = false, DefaultValue = 200.0 },
        new() { Name = "ThresholdStep",      Label = "Threshold Step",        Type = ParameterType.Double, IsConnectable = false, DefaultValue = 10.0  },
        new() { Name = "MinRepeatability",   Label = "Min Repeatability",     Type = ParameterType.Int,    IsConnectable = false, DefaultValue = 2     }
    ];
    public IReadOnlyList<string> OutputPorts => ["Blobs"];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image               = (Mat)parameters["Image"]!;
        var polarity            = (string?)parameters.GetValueOrDefault("Polarity") ?? "Light on Dark";
        var minCircularity      = (float)Convert.ToDouble(parameters["MinCircularity"]);
        var minArea             = (float)Convert.ToDouble(parameters["MinArea"]);
        var maxArea             = (float)Convert.ToDouble(parameters["MaxArea"]);
        var minDistBetweenBlobs = (float)Convert.ToDouble(parameters["MinDistBetweenBlobs"]);
        var minThreshold        = (float)Convert.ToDouble(parameters["MinThreshold"]);
        var maxThreshold        = (float)Convert.ToDouble(parameters["MaxThreshold"]);
        var thresholdStep       = (float)Convert.ToDouble(parameters.GetValueOrDefault("ThresholdStep") ?? 10.0);
        var minRepeatability    = (uint)Convert.ToInt32(parameters.GetValueOrDefault("MinRepeatability") ?? 2);

        var p = new SimpleBlobDetector.Params
        {
            FilterByColor        = true,
            BlobColor            = (byte)(polarity == "Dark on Light" ? 0 : 255),
            FilterByCircularity  = true,
            MinCircularity       = minCircularity,
            FilterByArea         = true,
            MinArea              = minArea,
            MaxArea              = maxArea,
            FilterByConvexity    = false,
            FilterByInertia      = false,
            MinDistBetweenBlobs  = minDistBetweenBlobs,
            MinThreshold         = minThreshold,
            MaxThreshold         = maxThreshold,
            ThresholdStep        = thresholdStep,
            MinRepeatability     = minRepeatability
        };

        using var detector = SimpleBlobDetector.Create(p);
        return detector.Detect(image);
    }
}
