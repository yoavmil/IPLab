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
        new() { Name = "Thresh", Label = "Threshold", Type = ParameterType.Double, IsConnectable = false, DefaultValue = 128.0, Min = 0.0, Max = 255.0 },
        ..RoiParameters.Schema,
    ];
    public IReadOnlyList<string> OutputPorts => ["Image", ..RoiParameters.OutputPorts];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image  = (Mat)parameters["Image"]!;
        var thresh = Convert.ToDouble(parameters["Thresh"]);

        var result = RoiParameters.ApplyImageFilter(image, parameters,
            src => { var r = new Mat(); Cv2.Threshold(src, r, thresh, 255.0, ThresholdTypes.Binary); return r; });

        var outputs = new Dictionary<string, object?> { ["Image"] = result };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }
}
