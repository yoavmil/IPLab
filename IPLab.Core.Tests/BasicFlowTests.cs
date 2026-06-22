using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class BasicFlowTests
{
    [Fact]
    public async Task LoadImage_OutputPorts_ReflectImageDimensions()
    {
        var path = Path.Combine(Path.GetTempPath(), "iplab-test-loadimage-ports.png");
        using (var mat = new Mat(60, 80, MatType.CV_8UC3, Scalar.All(128)))
            Cv2.ImWrite(path, mat);

        try
        {
            var flow = new FlowDef(
            [
                new Operator
                {
                    Id           = "O1",
                    DisplayName  = "Input",
                    Type         = new LoadImageOperator(),
                    Parameters   = [new ParameterValue { Name = "FilePaths", Value = new string[] { path } }],
                    Dependencies = []
                }
            ]);

            var executor = new FlowEx(flow);
            await executor.RunAllAsync();

            var result = executor.IntermediateResults["O1"] as Dictionary<string, object?>;
            Assert.NotNull(result);
            Assert.Equal(80,   result["Width"]);
            Assert.Equal(60,   result["Height"]);
            Assert.Equal(3,    result["Channels"]);
            Assert.Equal("U8", result["Depth"]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadColorImage_ConvertToGrayscale_Save_OutputIsGrayscale()
    {
        var inputPath  = Path.Combine(Path.GetTempPath(), "iplab-test-input.jpg");
        var outputPath = Path.Combine(Path.GetTempPath(), "iplab-test-output.jpg");

        using (var color = new Mat(100, 100, MatType.CV_8UC3, new Scalar(50, 128, 200)))
            Cv2.ImWrite(inputPath, color);

        try
        {
            var flow = new FlowDef(
            [
                new Operator
                {
                    Id           = "O1",
                    DisplayName  = "Input",
                    Type         = new LoadImageOperator(),
                    Parameters   = [new ParameterValue { Name = "FilePaths", Value = new string[] { inputPath } }],
                    Dependencies = []
                },
                new Operator
                {
                    Id           = "O2",
                    DisplayName  = "Grayscale",
                    Type         = new ConvertToGrayscaleOperator(),
                    Parameters   = [new ParameterValue { Name = "Image", Source = new SourceRef("O1", "Image") }],
                    Dependencies = [new Dependency("D1", "O1")]
                },
                new Operator
                {
                    Id           = "O3",
                    DisplayName  = "Save",
                    Type         = new SaveImageOperator(),
                    Parameters   =
                    [
                        new ParameterValue { Name = "Image",    Source = new SourceRef("O2", "Image") },
                        new ParameterValue { Name = "FilePath", Value  = outputPath }
                    ],
                    Dependencies = [new Dependency("D2", "O2")]
                }
            ]);

            var executor = new FlowEx(flow);
            await executor.RunAllAsync();

            Assert.True(File.Exists(outputPath), "Output file was not created.");

            using var result = Cv2.ImRead(outputPath, ImreadModes.Unchanged);
            Assert.Equal(1, result.Channels());
        }
        finally
        {
            if (File.Exists(inputPath))  File.Delete(inputPath);
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }
}
