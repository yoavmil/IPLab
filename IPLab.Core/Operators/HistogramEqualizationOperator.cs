using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>Enhances contrast using global histogram equalization or CLAHE (Contrast Limited Adaptive Histogram Equalization).</summary>
/// <seealso href="https://docs.opencv.org/4.x/d6/dc7/group__imgproc__hist.html#ga7e54091f0c937d49bf84152a16f76d6e">OpenCV: equalizeHist</seealso>
/// <seealso href="https://docs.opencv.org/4.x/d1/d5c/classcv_1_1CLAHE.html">OpenCV: CLAHE</seealso>
public class HistogramEqualizationOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName  => "HistogramEqualization";
    /// <inheritdoc/>
    public string Category  => "Filters";
    /// <inheritdoc/>
    public string Icon      => "histogram";
    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",        Label = "Image",          ConnectableType = typeof(Mat) },
        new() { Name = "Method",       Label = "Method",         Type = ParameterType.Enum,
                DefaultValue = "Equalize", Options = ["Equalize", "CLAHE"] },
        new() { Name = "ClipLimit",    Label = "Clip Limit",     Type = ParameterType.Double, DefaultValue = 2.0, Min = 0.0, Max = 100.0,
                ShowWhenParam = "Method", ShowWhenValues = ["CLAHE"] },
        new() { Name = "TileGridSize", Label = "Tile Grid Size", Type = ParameterType.Int,DefaultValue = 8,   Min = 1.0, Max = 64.0,
                ShowWhenParam = "Method", ShowWhenValues = ["CLAHE"] },
    ];
    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts => [new() { Name = "Image", DataType = typeof(Mat), IsDisplayImage = true }];

    /// <inheritdoc/>
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
