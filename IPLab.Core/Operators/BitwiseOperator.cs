using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class BitwiseOperator : IOperatorType
{
    public string TypeName => "Bitwise";
    public string Category => "Filters";
    public string Icon     => "bitwise";

    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "ImageA",    Label = "Image A",   ConnectableType = typeof(Mat) },
        new() { Name = "ImageB",    Label = "Image B",   ConnectableType = typeof(Mat) },
        new() { Name = "Operation", Label = "Operation", Type = ParameterType.Enum,
                DefaultValue = "And", Options = ["And", "Or", "Xor"] },
    ];

    public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "Image", DataType = typeof(Mat), IsDisplayImage = true },
    ];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var imageA    = (Mat)parameters["ImageA"]!;
        var imageB    = (Mat)parameters["ImageB"]!;
        var operation = parameters.GetValueOrDefault("Operation") as string ?? "And";

        var result = new Mat();

        switch (operation)
        {
            case "Or":  Cv2.BitwiseOr(imageA,  imageB, result); break;
            case "Xor": Cv2.BitwiseXor(imageA, imageB, result); break;
            default:    Cv2.BitwiseAnd(imageA, imageB, result); break;
        }

        return new Dictionary<string, object?> { ["Image"] = result };
    }
}
