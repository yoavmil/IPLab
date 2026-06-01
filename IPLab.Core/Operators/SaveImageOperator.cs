using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class SaveImageOperator : IOperatorType
{
    public string TypeName  => "SaveImage";
    public string Category  => "I/O";
    public string Icon      => "save";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",    Label = "Image",     ConnectableType = typeof(Mat) },
        new() { Name = "FilePath", Label = "File Path", Type = ParameterType.String }
    ];
    public IReadOnlyList<OutputPortDescriptor> OutputPorts => [];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image    = (Mat)parameters["Image"]!;
        var filePath = (string)parameters["FilePath"]!;
        Cv2.ImWrite(filePath, image);
        return null;
    }
}
