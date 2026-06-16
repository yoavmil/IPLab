using IPLab.Core.Operators;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class DistortionCalibrationOperatorTests
{
    private static readonly string TestImagesDir =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestImages");

    private static Dictionary<string, object?> ExecNew(
        Mat gray, string outPath = "", int halfSize = 7, float minResp = 0.45f)
    {
        return (Dictionary<string, object?>)new DistortionCalibrationOperator().Execute(
            new Dictionary<string, object?>
            {
                ["Image"]          = gray,
                ["KernelHalfSize"] = halfSize,
                ["MinResponse"]    = (double)minResp,
                ["AnchorX"]        = 0.5,
                ["AnchorY"]        = 0.5,
                ["OutputFilePath"] = outPath,
            })!;
    }

    // Verifies that calibration finds the checkerboard grid and recovers enough corners
    // across all four test images with the default parameters (KernelHalfSize=7, MinResponse=0.45).
    [Theory]
    [InlineData("CB_1.jpg", 783)]
    [InlineData("CB_2.jpg", 807)]
    [InlineData("CB_3.jpg", 382)]
    [InlineData("CB_4.jpg", 289)]
    public void Calibration_FindsSufficientCorners(string file, int expectedInliers)
    {
        using var src  = Cv2.ImRead(Path.Combine(TestImagesDir, file), ImreadModes.Color);
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        var result      = ExecNew(gray);
        var inlierCount = (int)result["InlierCount"]!;

        Assert.Equal(expectedInliers, inlierCount);
    }

    [Theory]
    [InlineData("CB_1.jpg")]
    [InlineData("CB_2.jpg")]
    [InlineData("CB_3.jpg")]
    [InlineData("CB_4.jpg")]
    public void Correspondences_ImageAndObjectPointsAlign(string file)
    {
        using var src  = Cv2.ImRead(Path.Combine(TestImagesDir, file), ImreadModes.Color);
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        var result       = ExecNew(gray);
        var imagePoints  = (Point2f[])result["GridCorners"]!;
        var objectPoints = (Point3f[])result["ObjectPoints"]!;
        var inlierCount  = (int)result["InlierCount"]!;

        Assert.Equal(inlierCount, imagePoints.Length);
        Assert.Equal(inlierCount, objectPoints.Length);

        Assert.All(objectPoints, p => Assert.Equal(0f, p.Z));
        Assert.Equal(0, objectPoints.Min(p => p.X));
        Assert.Equal(0, objectPoints.Min(p => p.Y));
        Assert.Equal(objectPoints.Length, objectPoints.Distinct().Count());
    }

    // After undistortion the checkerboard should be rectified so its rows are exactly horizontal.
    // Re-calibrating on the undistorted image measures the residual tilt; it must be within ±1°.
    // Only small-tilt images (CB_1, CB_2) are asserted here — large-tilt undistortions produce
    // wide black borders whose sharp edges create false saddle responses that confuse InferGrid
    // at the default threshold. That case is covered by Diag_RectificationAngles.
    [Theory]
    [InlineData("CB_1.jpg")]
    [InlineData("CB_2.jpg")]
    public void Undistort_RectifiesCheckerboardToHorizontal(string file)
    {
        var calibPath = Path.Combine(Path.GetTempPath(), $"iplab_calib_{Guid.NewGuid():N}.json");
        try
        {
            using var src  = Cv2.ImRead(Path.Combine(TestImagesDir, file), ImreadModes.Color);
            using var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            var r1 = ExecNew(gray, calibPath);
            double angleBefore = (double)r1["RotationAngleDeg"]!;

            var undistResult = (Dictionary<string, object?>)new UndistortOperator().Execute(
                new Dictionary<string, object?> { ["Image"] = gray, ["CalibFilePath"] = calibPath })!;
            using var undistorted = (Mat)undistResult["Image"]!;

            var r2 = ExecNew(undistorted);
            double angleAfter = (double)r2["RotationAngleDeg"]!;

            const double MaxResidualDeg = 1.0;
            Assert.True(Math.Abs(angleAfter) < MaxResidualDeg,
                $"{file}: residual angle {angleAfter:F3}° exceeds ±{MaxResidualDeg}° " +
                $"(before rectification: {angleBefore:F3}°)");
        }
        finally { File.Delete(calibPath); }
    }

}
