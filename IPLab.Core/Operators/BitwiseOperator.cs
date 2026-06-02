using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class BitwiseOperator : IOperatorType
{
    public string TypeName => "Bitwise";
    public string Category => "Filters";
    public string Icon     => "bitwise";

    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "ImageA",    Label = "Image A",   ConnectableType = typeof(Mat) },
        new() { Name = "ImageB",    Label = "Image B",   ConnectableType = typeof(Mat) },
        new() { Name = "Operation", Label = "Operation", Type = ParameterType.Enum,
                DefaultValue = "And", Options = ["And", "Or", "Xor"] },
    ];

    public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "Image", DataType = typeof(Mat), IsDisplayImage = true },
    ];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var imageA    = (Mat)parameters["ImageA"]!;
        var imageB    = (Mat)parameters["ImageB"]!;
        var operation = parameters.GetValueOrDefault("Operation") as string ?? "And";

        if (imageA.Size() != imageB.Size())
            throw new InvalidOperationException(
                $"Image sizes do not match: A is {imageA.Width}x{imageA.Height}, B is {imageB.Width}x{imageB.Height}.");

        if (imageA.Depth() != imageB.Depth())
            throw new InvalidOperationException(
                $"Image depths do not match: A is {imageA.Depth()}, B is {imageB.Depth()}.");

        int channels = imageA.Channels();
        if (channels != imageB.Channels())
            throw new InvalidOperationException(
                $"Channel counts do not match: A has {channels} channel(s), B has {imageB.Channels()}.");

        if (channels != 1 && channels != 3)
            throw new InvalidOperationException(
                $"Unsupported channel count: {channels}. Only 1 or 3 channels are supported.");

        Action<Mat, Mat, Mat> op = operation switch
        {
            "Or"  => (a, b, dst) => Cv2.BitwiseOr(a,  b, dst),
            "Xor" => (a, b, dst) => Cv2.BitwiseXor(a, b, dst),
            _     =>  (a, b, dst) => Cv2.BitwiseAnd(a, b, dst),
        };

        if (channels == 1)
        {
            var result = new Mat();
            op(imageA, imageB, result);
            return result;
        }

        // 3-channel: apply op per channel then merge.
        Mat[] chA = Cv2.Split(imageA);
        Mat[] chB = Cv2.Split(imageB);
        Mat[] chR = new Mat[3];
        for (int i = 0; i < 3; i++)
        {
            chR[i] = new Mat();
            op(chA[i], chB[i], chR[i]);
        }

        var merged = new Mat();
        Cv2.Merge(chR, merged);

        foreach (var m in chA) m.Dispose();
        foreach (var m in chB) m.Dispose();
        foreach (var m in chR) m.Dispose();

        return merged;
    }
}
