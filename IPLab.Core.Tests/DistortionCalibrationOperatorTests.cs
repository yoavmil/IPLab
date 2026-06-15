using System.Text.Json;
using IPLab.Core.Operators;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class DistortionCalibrationOperatorTests
{
    private static readonly string TestImagesDir =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestImages");

    private static Dictionary<string, object?> Exec(
        string file,
        int halfSize = 7,
        double minResponse = 0.45,
        string outputFilePath = "")
    {
        using var source = Cv2.ImRead(Path.Combine(TestImagesDir, file), ImreadModes.Grayscale);
        Assert.False(source.Empty(), $"Could not load test image {file}");

        return (Dictionary<string, object?>)new DistortionCalibrationOperator().Execute(
            new Dictionary<string, object?>
            {
                ["Image"] = source,
                ["KernelHalfSize"] = halfSize,
                ["MinResponse"] = minResponse,
                ["OutputFilePath"] = outputFilePath,
            })!;
    }

    [Theory]
    [InlineData("CB_1.jpg")]
    [InlineData("CB_2.jpg")]
    [InlineData("CB_3.jpg")]
    [InlineData("CB_4.jpg")]
    public void Calibration_FindsSparseGrid(string file)
    {
        var result = Exec(file);

        Assert.True((bool)result["Found"]!, $"{file}: grid not found");
        Assert.True((int)result["InlierCount"]! >= 100, $"{file}: too few grid corners");
        Assert.NotEmpty((Point2f[])result["Corners"]!);
        Assert.NotEmpty((LineSegmentPoint[])result["GridLines"]!);
        Assert.True(double.IsFinite((double)result["RotationAngleDeg"]!));
    }

    [Theory]
    [InlineData("CB_1.jpg")]
    [InlineData("CB_2.jpg")]
    [InlineData("CB_3.jpg")]
    [InlineData("CB_4.jpg")]
    public void Correspondences_ImageAndObjectPointsAlign(string file)
    {
        var result = Exec(file);
        var imagePoints = (Point2f[])result["GridCorners"]!;
        var objectPoints = (Point3f[])result["ObjectPoints"]!;
        var inlierCount = (int)result["InlierCount"]!;

        Assert.Equal(inlierCount, imagePoints.Length);
        Assert.Equal(inlierCount, objectPoints.Length);
        Assert.All(objectPoints, point => Assert.Equal(0f, point.Z));
        Assert.Equal(0f, objectPoints.Min(point => point.X));
        Assert.Equal(0f, objectPoints.Min(point => point.Y));
        Assert.Equal(objectPoints.Length, objectPoints.Distinct().Count());
    }

    [Theory]
    [InlineData("CB_1.jpg")]
    [InlineData("CB_2.jpg")]
    [InlineData("CB_3.jpg")]
    [InlineData("CB_4.jpg")]
    public void Calibration_WritesSparseCorrespondenceFile(string file)
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"iplab_calib_{Guid.NewGuid():N}.json");
        try
        {
            var result = Exec(file, outputFilePath: outputPath);
            var imagePoints = (Point2f[])result["GridCorners"]!;
            var objectPoints = (Point3f[])result["ObjectPoints"]!;

            Assert.True((bool)result["Found"]!, $"{file}: grid not found");
            Assert.Equal(outputPath, result["CalibFilePath"]);
            Assert.True(File.Exists(outputPath), $"{file}: calibration file not written");

            var data = JsonSerializer.Deserialize<CalibrationData>(
                File.ReadAllText(outputPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(data);
            Assert.Equal(imagePoints.Length, data.Corners.Count);
            Assert.Equal(0.5, data.AnchorX);
            Assert.Equal(0.5, data.AnchorY);
            Assert.True(double.IsFinite(data.RotationAngleDeg));

            for (int i = 0; i < data.Corners.Count; i++)
            {
                Assert.Equal((int)objectPoints[i].X, data.Corners[i].Col);
                Assert.Equal((int)objectPoints[i].Y, data.Corners[i].Row);
                Assert.Equal(imagePoints[i].X, data.Corners[i].ImgX, 4);
                Assert.Equal(imagePoints[i].Y, data.Corners[i].ImgY, 4);
            }
        }
        finally
        {
            File.Delete(outputPath);
        }
    }
}
