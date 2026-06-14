using System.Text.Json;
using IPLab.Core.Operators;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class UndistortOperatorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string WriteCalib(double k1, int w = 64, int h = 48)
    {
        var data = new CalibrationData
        {
            ImageWidth  = w,
            ImageHeight = h,
            CameraMatrix = [[(double)w, 0, w / 2.0], [0, h, h / 2.0], [0, 0, 1]],
            DistCoeffs   = [k1, 0, 0, 0, 0],
        };
        var path = Path.Combine(Path.GetTempPath(), $"iplab_calib_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions));
        return path;
    }

    [Fact]
    public void Undistort_ReturnsImageOfSameSizeAndType()
    {
        var path = WriteCalib(-0.2);
        try
        {
            using var src = new Mat(48, 64, MatType.CV_8UC3, Scalar.All(128));
            var result = (Dictionary<string, object?>)new UndistortOperator().Execute(
                new Dictionary<string, object?> { ["Image"] = src, ["CalibFilePath"] = path })!;

            var dst = (Mat)result["Image"]!;
            Assert.Equal(src.Size(), dst.Size());
            Assert.Equal(src.Type(), dst.Type());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Undistort_ThrowsOnSizeMismatch()
    {
        var path = WriteCalib(-0.2, w: 64, h: 48);
        try
        {
            using var src = new Mat(100, 100, MatType.CV_8UC1, Scalar.All(0));
            Assert.Throws<ArgumentException>(() => new UndistortOperator().Execute(
                new Dictionary<string, object?> { ["Image"] = src, ["CalibFilePath"] = path }));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CacheToken_ChangesWithFileWriteTime()
    {
        var path = WriteCalib(-0.2);
        try
        {
            var op = new UndistortOperator();
            var p  = new Dictionary<string, object?> { ["CalibFilePath"] = path };

            var token1 = op.GetCacheTokens(p).Single().Value;
            // Rewrite the file with different contents and a newer timestamp.
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));
            var token2 = op.GetCacheTokens(p).Single().Value;

            Assert.NotEqual(token1, token2);
        }
        finally { File.Delete(path); }
    }
}
