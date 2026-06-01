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
        new() { Name = "Image", Label = "Image", ConnectableType = typeof(Mat) },
        ..RoiParameters.Schema,
    ];
    public IReadOnlyList<OutputPortDescriptor> OutputPorts => [new() { Name = "Image", DataType = typeof(Mat) }, ..RoiParameters.OutputPorts];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image = (Mat)parameters["Image"]!;

        var result = RoiParameters.ApplyImageFilter(image, parameters,
            src => { var r = new Mat(); Cv2.BitwiseNot(src, r); return r; });

        var outputs = new Dictionary<string, object?> { ["Image"] = result };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }
}
