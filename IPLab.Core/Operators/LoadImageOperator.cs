using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>Loads one image from a file list and outputs the image and its path.</summary>
/// <seealso href="https://docs.opencv.org/4.x/d4/da8/group__imgcodecs.html#ga288b8b3da0892bd651fce07b3bbd3a56">OpenCV: imread</seealso>
public class LoadImageOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName  => "LoadImage";
    /// <inheritdoc/>
    public string Category  => "I/O";
    /// <inheritdoc/>
    public string Icon      => "folder";
    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "FilePaths",   Label = "File Paths",   Type = ParameterType.StringList, IsHidden = true, DefaultValue = Array.Empty<string>() },
        new() { Name = "ActiveIndex", Label = "Active Index", Type = ParameterType.Int,IsHidden = true, DefaultValue = 0, Min = 0 }
    ];
    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "Image",    DataType = typeof(Mat),    IsDisplayImage = true },
        new() { Name = "FilePath", DataType = typeof(string), IsDisplayImage = false },
        new() { Name = "Width",    DataType = typeof(int),    IsDisplayImage = false },
        new() { Name = "Height",   DataType = typeof(int),    IsDisplayImage = false },
        new() { Name = "Channels", DataType = typeof(int),    IsDisplayImage = false },
        new() { Name = "Depth",    DataType = typeof(string), IsDisplayImage = false }
    ];

    /// <inheritdoc/>
    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var filePaths   = parameters["FilePaths"] as string[] ?? [];
        var activeIndex = Convert.ToInt32(parameters.GetValueOrDefault("ActiveIndex") ?? 0);
        if (filePaths.Length == 0) return null;
        var idx   = Math.Min(filePaths.Length - 1, Math.Max(0, activeIndex));
        var path  = filePaths[idx];
        var image = Cv2.ImRead(path, ImreadModes.Color);
        return new Dictionary<string, object?>
        {
            ["Image"]    = image,
            ["FilePath"] = path,
            ["Width"]    = image.Width,
            ["Height"]   = image.Height,
            ["Channels"] = image.Channels(),
            ["Depth"]    = DepthToString(image.Depth())
        };
    }

    private static string DepthToString(int depth) => depth switch
    {
        0 => "U8",
        1 => "S8",
        2 => "U16",
        3 => "S16",
        4 => "S32",
        5 => "F32",
        6 => "F64",
        _ => $"D{depth}"
    };
}
