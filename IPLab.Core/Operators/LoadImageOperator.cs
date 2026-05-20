using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class LoadImageOperator : IOperatorType
{
    public string TypeName  => "LoadImage";
    public string Category  => "I/O";
    public string Icon      => "folder";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "FilePath", Label = "File Path", Type = ParameterType.String, IsConnectable = false }
    ];
    public IReadOnlyList<string> OutputPorts => ["Image"];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var filePath = (string)parameters["FilePath"]!;
        return Cv2.ImRead(filePath, ImreadModes.Color);
    }
}
