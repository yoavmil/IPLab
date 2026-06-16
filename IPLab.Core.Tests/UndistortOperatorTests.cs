using System.Text.Json;
using IPLab.Core.Operators;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class UndistortOperatorTests
{
    // Writes a new-schema calibration file with a regular grid of corners.
    // pitch=10, 3×3 grid centred at the image; enough for the operator to build a partial warp map.
    private static string WriteCalib(int w = 64, int h = 48, double anchorX = 0.5, double anchorY = 0.5)
    {
        float ax = (float)(anchorX * w);
        float ay = (float)(anchorY * h);
        const float pitch = 10f;

        var corners = new List<CornerRecord>();
        for (int col = 0; col <= 2; col++)
        for (int row = 0; row <= 2; row++)
        {
            corners.Add(new CornerRecord
            {
                Col  = col,
                Row  = row,
                ImgX = ax + (col - 1) * pitch,
                ImgY = ay - (row - 1) * pitch,   // J up → row increases upward
            });
        }

        var data = new CalibrationData
        {
            ImageWidth       = w,
            ImageHeight      = h,
            AnchorX          = anchorX,
            AnchorY          = anchorY,
            RotationAngleDeg = 0.0,
            Corners          = corners,
        };

        var path = Path.Combine(Path.GetTempPath(), $"iplab_calib_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented        = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }));
        return path;
    }

    [Fact]
    public void Undistort_ReturnsImageOfSameSizeAndType()
    {
        var path = WriteCalib();
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
        var path = WriteCalib(w: 64, h: 48);
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
        var path = WriteCalib();
        try
        {
            var op = new UndistortOperator();
            var p  = new Dictionary<string, object?> { ["CalibFilePath"] = path };

            var token1 = op.GetCacheTokens(p).Single().Value;
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));
            var token2 = op.GetCacheTokens(p).Single().Value;

            Assert.NotEqual(token1, token2);
        }
        finally { File.Delete(path); }
    }
}
