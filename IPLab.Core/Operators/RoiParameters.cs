using IPLab.Core.Models;

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
