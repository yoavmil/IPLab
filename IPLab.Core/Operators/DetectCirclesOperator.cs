using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class DetectCirclesOperator : IOperatorType
{
    public string TypeName  => "DetectCircles";
    public string Category  => "Detection";
    public string Icon      => "circle";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",     Label = "Image",              Type = ParameterType.Object, IsConnectable = true  },
        new() { Name = "MinDist",   Label = "Min Distance",       Type = ParameterType.Double, IsConnectable = false },
        new() { Name = "Param1",    Label = "Canny Threshold",    Type = ParameterType.Double, IsConnectable = false },
        new() { Name = "Param2",    Label = "Accumulator Thresh", Type = ParameterType.Double, IsConnectable = false },
        new() { Name = "MinRadius", Label = "Min Radius",         Type = ParameterType.Int,    IsConnectable = false },
        new() { Name = "MaxRadius", Label = "Max Radius",         Type = ParameterType.Int,    IsConnectable = false }
    ];
    public IReadOnlyList<string> OutputPorts => ["Circles"];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image     = (Mat)parameters["Image"]!;
        var minDist   = Convert.ToDouble(parameters["MinDist"]);
        var param1    = Convert.ToDouble(parameters["Param1"]);
        var param2    = Convert.ToDouble(parameters["Param2"]);
        var minRadius = Convert.ToInt32(parameters["MinRadius"]);
        var maxRadius = Convert.ToInt32(parameters["MaxRadius"]);

        return Cv2.HoughCircles(image, HoughModes.Gradient, dp: 1, minDist,
                                param1: param1, param2: param2,
                                minRadius: minRadius, maxRadius: maxRadius);
    }
}
