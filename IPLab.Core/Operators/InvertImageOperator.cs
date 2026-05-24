using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class InvertImageOperator : IOperatorType
{
    public string TypeName  => "InvertImage";
    public string Category  => "Color & Channels";
    public string Icon      => "invert";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image", Label = "Image", Type = ParameterType.Object, IsConnectable = true }
    ];
    public IReadOnlyList<string> OutputPorts => ["Image"];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image = (Mat)parameters["Image"]!;
        var result = new Mat();
        Cv2.BitwiseNot(image, result);
        return result;
    }
}
