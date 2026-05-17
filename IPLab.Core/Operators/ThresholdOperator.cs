using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class ThresholdOperator : IOperatorType
{
    public string TypeName => "Threshold";
    public string Icon => "threshold";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",  Label = "Image",     Type = ParameterType.Object, IsConnectable = true  },
        new() { Name = "Thresh", Label = "Threshold", Type = ParameterType.Double, IsConnectable = false, DefaultValue = 128.0, Min = 0.0, Max = 255.0 },
        new() { Name = "MaxVal", Label = "Max Value",  Type = ParameterType.Double, IsConnectable = false, DefaultValue = 255.0, Min = 0.0, Max = 255.0 }
    ];
    public IReadOnlyList<string> OutputPorts => ["Image"];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image  = (Mat)parameters["Image"]!;
        var thresh = Convert.ToDouble(parameters["Thresh"]);
        var maxVal = Convert.ToDouble(parameters["MaxVal"]);

        var output = new Mat();
        Cv2.Threshold(image, output, thresh, maxVal, ThresholdTypes.Binary);
        return output;
    }
}
