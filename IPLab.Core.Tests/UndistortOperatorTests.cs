using System.Text.Json;
using IPLab.Core.Models;
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
            Pitch            = pitch,
            MeanCol          = 1f,   // average of col indices 0, 1, 2
            MeanRow          = 1f,   // average of row indices 0, 1, 2
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

    // Smoke test: operator runs without error and the output Mat has the same
    // dimensions and channel count as the input.
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

    // Passing an image whose dimensions don't match the calibration file's recorded size
    // must be rejected — silently remapping would produce garbage output.
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

    // Fewer than 4 distinct grid corners cannot form even one bilinear cell (needs a 2×2
    // arrangement). Must throw with a message that names the problem.
    [Fact]
    public void Undistort_ThrowsOnTooFewCorners()
    {
        var data = new CalibrationData
        {
            ImageWidth = 64, ImageHeight = 48, AnchorX = 0.5, AnchorY = 0.5,
            Corners =
            [
                new() { Col = 0, Row = 0, ImgX = 22, ImgY = 14 },
                new() { Col = 1, Row = 0, ImgX = 32, ImgY = 14 },
                new() { Col = 0, Row = 1, ImgX = 22, ImgY = 24 },
            ],
        };
        var path = Path.Combine(Path.GetTempPath(), $"iplab_calib_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(data,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));

            using var src = new Mat(48, 64, MatType.CV_8UC1, Scalar.All(128));
            var ex = Assert.Throws<InvalidDataException>(() => new UndistortOperator().Execute(
                new Dictionary<string, object?> { ["Image"] = src, ["CalibFilePath"] = path }));
            Assert.Contains("non-empty", ex.Message);
        }
        finally { File.Delete(path); }
    }

    // Duplicate (col, row) entries in the JSON must throw with a descriptive message rather
    // than silently discarding one entry and producing a subtly wrong warp map.
    [Fact]
    public void Undistort_ThrowsOnDuplicateCornerKeys()
    {
        var data = new CalibrationData
        {
            ImageWidth = 64, ImageHeight = 48, AnchorX = 0.5, AnchorY = 0.5,
            Corners =
            [
                new() { Col = 0, Row = 0, ImgX = 22, ImgY = 14 },
                new() { Col = 1, Row = 0, ImgX = 32, ImgY = 14 },
                new() { Col = 0, Row = 1, ImgX = 22, ImgY = 24 },
                new() { Col = 1, Row = 1, ImgX = 32, ImgY = 24 },
                new() { Col = 0, Row = 0, ImgX = 99, ImgY = 99 },   // duplicate key
            ],
        };
        var path = Path.Combine(Path.GetTempPath(), $"iplab_calib_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(data,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));

            using var src = new Mat(48, 64, MatType.CV_8UC1, Scalar.All(128));
            var ex = Assert.Throws<InvalidDataException>(() => new UndistortOperator().Execute(
                new Dictionary<string, object?> { ["Image"] = src, ["CalibFilePath"] = path }));
            Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { File.Delete(path); }
    }

    // ICacheInvalidationProvider contract: the token must change when the calibration file
    // is overwritten so FlowEx re-executes the operator instead of serving stale output.
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
