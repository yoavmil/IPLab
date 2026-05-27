using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public static class RoiParameters
{
    public static IReadOnlyList<ParameterDescriptor> Schema =>
    [
        new() { Name = "RoiX", Label = "ROI X",      Type = ParameterType.Int, IsConnectable = true, DefaultValue = 0 },
        new() { Name = "RoiY", Label = "ROI Y",      Type = ParameterType.Int, IsConnectable = true, DefaultValue = 0 },
        new() { Name = "RoiW", Label = "ROI Width",  Type = ParameterType.Int, IsConnectable = true, DefaultValue = 0 },
        new() { Name = "RoiH", Label = "ROI Height", Type = ParameterType.Int, IsConnectable = true, DefaultValue = 0 },
    ];

    // These port names must be included in an operator's OutputPorts when it supports ROI,
    // so downstream operators can wire their own ROI params to the upstream operator's ROI values.
    public static IReadOnlyList<string> OutputPorts => ["RoiX", "RoiY", "RoiW", "RoiH"];

    // Returns null → no ROI requested; caller runs on the full image.
    // Returns a Rect → ROI requested and clamped to image bounds.
    //   Caller must still check Width <= 0 || Height <= 0 (ROI entirely outside the image).
    public static Rect? Clamp(IReadOnlyDictionary<string, object?> parameters,
                               int imageWidth, int imageHeight)
    {
        var roi = Extract(parameters);
        if (roi is null) return null;
        int x = Math.Max(0, (int)roi.X);
        int y = Math.Max(0, (int)roi.Y);
        int w = Math.Min((int)roi.Width,  imageWidth  - x);
        int h = Math.Min((int)roi.Height, imageHeight - y);
        return new Rect(x, y, w, h);
    }

    // For filter operators: crops the image to the valid ROI, applies `process` to the crop,
    // and composites the result back over a clone of the full image.
    // • No ROI set           → calls process(fullImage) directly.
    // • ROI entirely outside → returns image.Clone() unchanged.
    public static Mat ApplyImageFilter(Mat image,
                                       IReadOnlyDictionary<string, object?> parameters,
                                       Func<Mat, Mat> process)
    {
        var roiRect = Clamp(parameters, image.Width, image.Height);
        if (roiRect is null)                                        return process(image);
        if (roiRect.Value.Width <= 0 || roiRect.Value.Height <= 0) return image.Clone();

        var rect = roiRect.Value;
        using var crop   = new Mat(image, rect);
        var       patch  = process(crop);
        var       output = image.Clone();
        using var dst    = new Mat(output, rect);
        patch.CopyTo(dst);
        patch.Dispose();
        return output;
    }

    // Returns null when W=0 or H=0 — operator should run on the full image.
    public static RoiDef? Extract(IReadOnlyDictionary<string, object?> parameters)
    {
        var x = Convert.ToInt32(parameters.GetValueOrDefault("RoiX") ?? 0);
        var y = Convert.ToInt32(parameters.GetValueOrDefault("RoiY") ?? 0);
        var w = Convert.ToInt32(parameters.GetValueOrDefault("RoiW") ?? 0);
        var h = Convert.ToInt32(parameters.GetValueOrDefault("RoiH") ?? 0);
        return (w > 0 && h > 0) ? new RoiDef(x, y, w, h) : null;
    }

    // Copies the four ROI input values straight through to the output dictionary so that
    // downstream operators can wire their own ROI params to this operator's ROI ports.
    public static void AddToOutputs(Dictionary<string, object?> outputs,
                                    IReadOnlyDictionary<string, object?> parameters)
    {
        outputs["RoiX"] = parameters.GetValueOrDefault("RoiX");
        outputs["RoiY"] = parameters.GetValueOrDefault("RoiY");
        outputs["RoiW"] = parameters.GetValueOrDefault("RoiW");
        outputs["RoiH"] = parameters.GetValueOrDefault("RoiH");
    }
}
