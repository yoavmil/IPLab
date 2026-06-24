using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;
using OpenCvSharp.XImgProc;

namespace IPLab.Core.Operators;

/// <summary>Applies morphological skeleton thinning (Zhang-Suen or Guo-Hall) to reduce binary objects to single-pixel-wide skeletons, with optional ROI support.</summary>
/// <seealso href="https://docs.opencv.org/4.x/df/d2d/group__ximgproc.html#ga37002c6ca80c978b4d1c3c91694d3cd9">OpenCV ximgproc: thinning</seealso>
/// <seealso href="https://github.com/yoavmil/IPLab/blob/master/docs/OPERATORS.md#thinning">Operator reference</seealso>
public class ThinningOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName => "Thinning";
    /// <inheritdoc/>
    public string Category => "Filters";
    /// <inheritdoc/>
    public string Icon     => "thinning";

    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",        Label = "Image",         ConnectableType = typeof(Mat) },
        new() { Name = "ThinningType", Label = "Thinning Type", Type = ParameterType.Enum,
                DefaultValue = "ZhangSuen", Options = ["ZhangSuen", "GuoHall"] },
        ..RoiParameters.Schema,
    ];

    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts => [new() { Name = "Image", DataType = typeof(Mat), IsDisplayImage = true }, ..RoiParameters.OutputPorts];

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image        = (Mat)parameters["Image"]!;
        var thinningType = parameters.GetValueOrDefault("ThinningType") as string ?? "ZhangSuen";

        var cvType = thinningType == "GuoHall" ? ThinningTypes.GUOHALL : ThinningTypes.ZHANGSUEN;

        var result = RoiParameters.ApplyImageFilter(image, parameters, src =>
        {
            var dst = new Mat();
            CvXImgProc.Thinning(src, dst, cvType);
            return dst;
        });

        var outputs = new Dictionary<string, object?> { ["Image"] = result };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }
}
