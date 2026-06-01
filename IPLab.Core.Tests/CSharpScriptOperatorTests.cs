using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;
using IPLab.Core.Serialization;
using OpenCvSharp;

namespace IPLab.Core.Tests;

public class CSharpScriptOperatorTests : IDisposable
{
    private readonly CSharpScriptOperator _op = new();
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            try { File.Delete(f); } catch { /* best-effort */ }
    }

    private string WriteTempScript(string code)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, code);
        _tempFiles.Add(path);
        return path;
    }

    private IReadOnlyDictionary<string, object?> Params(string path, object? in1 = null,
        object? in2 = null, object? in3 = null, object? in4 = null) =>
        new Dictionary<string, object?>
        {
            ["ScriptPath"] = path,
            ["In1"] = in1, ["In2"] = in2, ["In3"] = in3, ["In4"] = in4,
        };

    private Dictionary<string, object?> Run(string path, object? in1 = null,
        object? in2 = null, object? in3 = null, object? in4 = null) =>
        (Dictionary<string, object?>)_op.Execute(Params(path, in1, in2, in3, in4))!;

    // --- Basic execution ---

    [Fact]
    public void EmptyScriptPath_ReturnsEmptyOutputs()
    {
        var result = (Dictionary<string, object?>)_op.Execute(
            new Dictionary<string, object?> { ["ScriptPath"] = "" })!;

        Assert.Null(result["Image"]);
        Assert.Null(result["Out1"]);
    }

    [Fact]
    public void Script_SetsOut1_ReturnsValue()
    {
        var path = WriteTempScript("Out1 = 42;");
        var result = Run(path);
        Assert.Equal(42, result["Out1"]);
    }

    [Fact]
    public void Script_SetsImageOutput_ReturnsMat()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC1, Scalar.White);
        var path = WriteTempScript("Image = (Mat)In1;");

        var result = Run(path, in1: src);

        Assert.IsType<Mat>(result["Image"]);
    }

    [Fact]
    public void Script_ReadsIn1_AndTransforms()
    {
        var path = WriteTempScript("Out1 = (int)In1 * 2;");
        var result = Run(path, in1: 7);
        Assert.Equal(14, result["Out1"]);
    }

    [Fact]
    public void Script_MultipleOutputs_AllReturned()
    {
        var path = WriteTempScript("Out1 = 1; Out2 = \"hello\"; Out3 = 3.14;");
        var result = Run(path);

        Assert.Equal(1, result["Out1"]);
        Assert.Equal("hello", result["Out2"]);
        Assert.Equal(3.14, result["Out3"]);
    }

    [Fact]
    public void Script_UsesLinq_NoImportError()
    {
        var path = WriteTempScript(
            "var nums = new[] { 1, 2, 3, 4 };" +
            "Out1 = nums.Where(n => n > 2).Sum();");
        var result = Run(path);
        Assert.Equal(7, result["Out1"]);
    }

    // --- Compile errors ---

    [Fact]
    public void CompileError_ThrowsInvalidOperationException()
    {
        var path = WriteTempScript("this is not valid C# !!!;");
        var ex = Assert.Throws<InvalidOperationException>(() => Run(path));
        Assert.Contains("compile", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // --- Missing file ---

    [Fact]
    public void MissingFile_ThrowsFileNotFoundException()
    {
        var nonExistent = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".cs");
        Assert.Throws<FileNotFoundException>(() => Run(nonExistent));
    }

    // --- Compile cache + stale-file recompile ---

    [Fact]
    public void StaleFile_Recompiles_WithNewResult()
    {
        var path = WriteTempScript("Out1 = 1;");
        var r1 = Run(path);
        Assert.Equal(1, r1["Out1"]);

        // Overwrite with new script and advance mod time by 1 second
        File.WriteAllText(path, "Out1 = 99;");
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(1));

        var r2 = Run(path);
        Assert.Equal(99, r2["Out1"]);
    }

    [Fact]
    public void UnchangedFile_DoesNotRecompile_SameResult()
    {
        var path = WriteTempScript("Out1 = 7;");
        var r1 = Run(path);
        var r2 = Run(path);  // second call — should hit cache
        Assert.Equal(r1["Out1"], r2["Out1"]);
    }

    // --- Serialization round-trip ---

    [Fact]
    public void ScriptPath_SurvivesJsonRoundTrip()
    {
        const string scriptPath = @"C:\scripts\my_filter.cs";

        var flowDef = new FlowDef(
        [
            new Operator
            {
                Id           = "O1",
                DisplayName  = "Script",
                Type         = new CSharpScriptOperator(),
                Parameters   = [new ParameterValue { Name = "ScriptPath", Value = scriptPath }],
                Dependencies = [],
            }
        ]);

        var json     = FlowDefSerializer.Serialize(new Flow(flowDef, new FlowLayout([], [])));
        var restored = FlowDefSerializer.Deserialize(json, OperatorRegistry.CreateDefault());

        var p = restored.Def.Operators.Single().Parameters.ToDictionary(v => v.Name);
        Assert.Equal(scriptPath, (string)p["ScriptPath"].Value!);
    }
}
