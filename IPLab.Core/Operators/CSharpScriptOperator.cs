using System.Collections.Concurrent;
using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class CSharpScriptOperator : IOperatorType
{
    public string TypeName => "CSharpScript";
    public string Category => "Scripting";
    public string Icon     => "code";

    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "ScriptPath", Label = "Script Path", Type = ParameterType.String, DefaultValue = "" },
        new() { Name = "In1", Label = "In 1", ConnectableType = typeof(object) },
        new() { Name = "In2", Label = "In 2", ConnectableType = typeof(object) },
        new() { Name = "In3", Label = "In 3", ConnectableType = typeof(object) },
        new() { Name = "In4", Label = "In 4", ConnectableType = typeof(object) },
    ];

    public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "Image", DataType = typeof(Mat)    },
        new() { Name = "Out1",  DataType = typeof(object) },
        new() { Name = "Out2",  DataType = typeof(object) },
        new() { Name = "Out3",  DataType = typeof(object) },
        new() { Name = "Out4",  DataType = typeof(object) },
    ];

    private readonly ConcurrentDictionary<string, (DateTime ModTime, Script<object?> Compiled)> _cache = new();

    private static readonly ScriptOptions _scriptOptions = ScriptOptions.Default
        .AddImports(
            "System",
            "System.Linq",
            "System.Collections.Generic",
            "OpenCvSharp",
            "OpenCvSharp.Features2D",
            "IPLab.Core.Models"
        )
        .AddReferences(
            typeof(object).Assembly,        // System.Private.CoreLib
            typeof(Enumerable).Assembly,    // System.Linq
            typeof(Mat).Assembly,           // OpenCvSharp
            typeof(ScriptGlobals).Assembly  // IPLab.Core (ConnectedComponentInfo, etc.)
        );

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var path = parameters.GetValueOrDefault("ScriptPath") as string ?? "";

        var emptyResult = new Dictionary<string, object?>
        {
            ["Image"] = null, ["Out1"] = null, ["Out2"] = null, ["Out3"] = null, ["Out4"] = null,
        };

        if (string.IsNullOrWhiteSpace(path))
            return emptyResult;

        var compiled = GetCompiledScript(path);

        var globals = new ScriptGlobals
        {
            In1 = parameters.GetValueOrDefault("In1"),
            In2 = parameters.GetValueOrDefault("In2"),
            In3 = parameters.GetValueOrDefault("In3"),
            In4 = parameters.GetValueOrDefault("In4"),
        };

        compiled.RunAsync(globals).GetAwaiter().GetResult();

        return new Dictionary<string, object?>
        {
            ["Image"] = globals.Image,
            ["Out1"]  = globals.Out1,
            ["Out2"]  = globals.Out2,
            ["Out3"]  = globals.Out3,
            ["Out4"]  = globals.Out4,
        };
    }

    private Script<object?> GetCompiledScript(string path)
    {
        var modTime = File.GetLastWriteTimeUtc(path);

        if (_cache.TryGetValue(path, out var entry) && entry.ModTime == modTime)
            return entry.Compiled;

        var code   = File.ReadAllText(path);
        var script = CSharpScript.Create<object?>(code, _scriptOptions, typeof(ScriptGlobals));

        var diagnostics = script.Compile();
        var errors      = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Script compile errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));

        _cache[path] = (modTime, script);
        return script;
    }
}
