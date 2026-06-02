using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;
using OpenCvSharp.XImgProc;

namespace IPLab.Core.Operators;

public class ThinningOperator : IOperatorType
{
    public string TypeName => "Thinning";
    public string Category => "Filters";
    public string Icon     => "thinning";

    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",        Label = "Image",         ConnectableType = typeof(Mat) },
        new() { Name = "ThinningType", Label = "Thinning Type", Type = ParameterType.Enum,
                DefaultValue = "ZhangSuen", Options = ["ZhangSuen", "GuoHall"] },
        ..RoiParameters.Schema,
    ];

    public IReadOnlyList<OutputPortDescriptor> OutputPorts => [new() { Name = "Image", DataType = typeof(Mat), IsDisplayImage = true }, ..RoiParameters.OutputPorts];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
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
