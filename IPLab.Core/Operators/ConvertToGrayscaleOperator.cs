using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class ConvertToGrayscaleOperator : IOperatorType
{
    public string TypeName => "ConvertToGrayscale";
    public string Icon => "palette";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image", Label = "Image", Type = ParameterType.Object, IsConnectable = true }
    ];
    public IReadOnlyList<string> OutputPorts => ["Image"];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image = (Mat)parameters["Image"]!;
        var gray = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }
}
