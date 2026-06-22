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
    public void Correspondences_GridCornersMatchInlierCount(string file)
    {
        using var src  = Cv2.ImRead(Path.Combine(TestImagesDir, file), ImreadModes.Color);
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        var result      = ExecNew(gray);
        var imagePoints = (Point2f[])result["GridCorners"]!;
        var inlierCount = (int)result["InlierCount"]!;

        Assert.Equal(inlierCount, imagePoints.Length);
        Assert.True(inlierCount >= 12);
    }

    // Undistorting a solid-white image should produce near-zero black pixels — the extended
    // grid must cover the full frame. Uses CB_1/CB_2 (small tilt) where the source image
    // covers nearly the whole frame; fewer than 2% black pixels is the acceptance threshold.
    [Theory]
    [InlineData("CB_1.jpg")]
    [InlineData("CB_2.jpg")]
    public void Undistort_MinimalBlackPixels(string file)
    {
        var calibPath = Path.Combine(Path.GetTempPath(), $"iplab_calib_{Guid.NewGuid():N}.json");
        try
        {
            using var src  = Cv2.ImRead(Path.Combine(TestImagesDir, file), ImreadModes.Color);
            using var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            ExecNew(gray, calibPath);

            using var white = new Mat(src.Size(), src.Type(), Scalar.All(255));
            var result = (Dictionary<string, object?>)new UndistortOperator().Execute(
                new Dictionary<string, object?> { ["Image"] = white, ["CalibFilePath"] = calibPath })!;
            using var dst = (Mat)result["Image"]!;

            using var grayDst = new Mat();
            Cv2.CvtColor(dst, grayDst, ColorConversionCodes.BGR2GRAY);
            using var blackMask = new Mat();
            Cv2.Threshold(grayDst, blackMask, 5, 255, ThresholdTypes.BinaryInv);
            int blackPixels = Cv2.CountNonZero(blackMask);
            int total       = dst.Width * dst.Height;

            Assert.True(blackPixels < total * 0.02,
                $"{file}: {blackPixels} black pixels ({100.0 * blackPixels / total:F1}% of {total})");
        }
        finally { File.Delete(calibPath); }
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
