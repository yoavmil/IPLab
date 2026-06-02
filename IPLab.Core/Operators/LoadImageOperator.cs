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
        new() { Name = "FilePaths",   Label = "File Paths",   Type = ParameterType.StringList, IsHidden = true, DefaultValue = Array.Empty<string>() },
        new() { Name = "ActiveIndex", Label = "Active Index", Type = ParameterType.Int,IsHidden = true, DefaultValue = 0, Min = 0 }
    ];
    public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "Image",    DataType = typeof(Mat),    IsDisplayImage = true },
        new() { Name = "FilePath", DataType = typeof(string), IsDisplayImage = false }
    ];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var filePaths   = parameters["FilePaths"] as string[] ?? [];
        var activeIndex = Convert.ToInt32(parameters.GetValueOrDefault("ActiveIndex") ?? 0);
        if (filePaths.Length == 0) return null;
        var idx      = Math.Clamp(activeIndex, 0, filePaths.Length - 1);
        var path     = filePaths[idx];
        return new Dictionary<string, object?>
        {
            ["Image"]    = Cv2.ImRead(path, ImreadModes.Color),
            ["FilePath"] = path
        };
    }
}
