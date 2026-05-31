using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class HistogramEqualizationOperator : IOperatorType
{
    public string TypeName  => "HistogramEqualization";
    public string Category  => "Filters";
    public string Icon      => "histogram";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",        Label = "Image",          Type = ParameterType.Object, IsConnectable = true  },
        new() { Name = "Method",       Label = "Method",         Type = ParameterType.Enum,   IsConnectable = false,
                DefaultValue = "Equalize", Options = ["Equalize", "CLAHE"] },
        new() { Name = "ClipLimit",    Label = "Clip Limit",     Type = ParameterType.Double, IsConnectable = false, DefaultValue = 2.0, Min = 0.0, Max = 100.0,
                ShowWhenParam = "Method", ShowWhenValues = ["CLAHE"] },
        new() { Name = "TileGridSize", Label = "Tile Grid Size", Type = ParameterType.Int,    IsConnectable = false, DefaultValue = 8,   Min = 1.0, Max = 64.0,
                ShowWhenParam = "Method", ShowWhenValues = ["CLAHE"] },
    ];
    public IReadOnlyList<string> OutputPorts => ["Image"];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image        = (Mat)parameters["Image"]!;
        var method       = parameters.GetValueOrDefault("Method") as string ?? "Equalize";
        var clipLimit    = Convert.ToDouble(parameters.GetValueOrDefault("ClipLimit")    ?? 2.0);
        var tileGridSize = Convert.ToInt32(parameters.GetValueOrDefault("TileGridSize") ?? 8);

        var result = new Mat();
        if (method == "CLAHE")
        {
            using var clahe = Cv2.CreateCLAHE(clipLimit, new Size(tileGridSize, tileGridSize));
            clahe.Apply(image, result);
        }
        else
        {
            Cv2.EqualizeHist(image, result);
        }

        return result;
    }
}
