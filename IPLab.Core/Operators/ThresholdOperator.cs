using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class ThresholdOperator : IOperatorType
{
    public string TypeName  => "Threshold";
    public string Category  => "Filters";
    public string Icon      => "threshold";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",  Label = "Image",     Type = ParameterType.Object, IsConnectable = true  },
        new() { Name = "Method", Label = "Method",    Type = ParameterType.Enum,   IsConnectable = false,
                DefaultValue = "Fixed", Options = ["Fixed", "Otsu", "Triangle"] },
        new() { Name = "Type",   Label = "Output Type", Type = ParameterType.Enum,   IsConnectable = false,
                DefaultValue = "Binary", Options = ["Binary", "BinaryInv", "Trunc", "ToZero", "ToZeroInv"] },
        new() { Name = "Thresh", Label = "Threshold",  Type = ParameterType.Double, IsConnectable = false, DefaultValue = 128.0, Min = 0.0, Max = 255.0 },
        ..RoiParameters.Schema,
    ];
    public IReadOnlyList<string> OutputPorts => ["Image", ..RoiParameters.OutputPorts];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image  = (Mat)parameters["Image"]!;
        var thresh = Convert.ToDouble(parameters.GetValueOrDefault("Thresh") ?? 128.0);
        var method = parameters.GetValueOrDefault("Method") as string ?? "Fixed";
        var type   = parameters.GetValueOrDefault("Type")   as string ?? "Binary";

        var threshType = type switch
        {
            "BinaryInv" => ThresholdTypes.BinaryInv,
            "Trunc"     => ThresholdTypes.Trunc,
            "ToZero"    => ThresholdTypes.Tozero,
            "ToZeroInv" => ThresholdTypes.TozeroInv,
            _           => ThresholdTypes.Binary,
        };
        if (method == "Otsu")     threshType |= ThresholdTypes.Otsu;
        if (method == "Triangle") threshType |= ThresholdTypes.Triangle;

        var result = RoiParameters.ApplyImageFilter(image, parameters,
            src => { var r = new Mat(); Cv2.Threshold(src, r, thresh, 255.0, threshType); return r; });

        var outputs = new Dictionary<string, object?> { ["Image"] = result };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }
}
