using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace IPLab.UI.Services;

// All UI-facing operations specific to the CSharpScript operator type.
internal static class CSharpScriptService
{
    public static void BrowseScript(IReadOnlyList<ViewModels.ParameterEditViewModel> parameters)
    {
        var pathParam = parameters.FirstOrDefault(p => p.Name == "ScriptPath");
        var current   = pathParam?.ValueText ?? string.Empty;

        var dlg = new SaveFileDialog
        {
            Title           = "Select or Create Script File",
            Filter          = "C# Script|*.cs|All files|*.*",
            DefaultExt      = "cs",
            OverwritePrompt = false,
        };

        if (!string.IsNullOrWhiteSpace(current))
        {
            try
            {
                dlg.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(current));
                dlg.FileName         = Path.GetFileName(current);
            }
            catch { /* bad path — let dialog open at default location */ }
        }

        if (dlg.ShowDialog() != true) return;

        var selected = dlg.FileName;

        if (!File.Exists(selected))
        {
            var addSample = MessageBox.Show(
                $"'{Path.GetFileName(selected)}' does not exist yet.\n\nAdd sample code to get started?",
                "New Script", MessageBoxButton.YesNo, MessageBoxImage.Question);

            File.WriteAllText(selected,
                addSample == MessageBoxResult.Yes ? SampleCode() : string.Empty);
        }

        if (pathParam != null)
            pathParam.ValueText = selected;
    }

    public static void ScaffoldDebugProject(IReadOnlyList<ViewModels.ParameterEditViewModel> parameters)
    {
        var scriptPath = parameters.FirstOrDefault(p => p.Name == "ScriptPath")?.ValueText ?? string.Empty;

        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            MessageBox.Show("Set Script Path first.", "Scaffold Debug Project",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var scriptDir  = Path.GetDirectoryName(Path.GetFullPath(scriptPath)) ?? string.Empty;
            var scriptName = Path.GetFileNameWithoutExtension(scriptPath);
            var debugDir   = Path.Combine(scriptDir, scriptName + "_debug");

            Directory.CreateDirectory(debugDir);
            File.WriteAllText(Path.Combine(debugDir, scriptName + "_debug.csproj"), Csproj());
            File.WriteAllText(Path.Combine(debugDir, "Program.cs"),                 Program(scriptName));

            MessageBox.Show(
                $"Debug project created at:\n{debugDir}\n\nOpen the folder and run: dotnet run",
                "Scaffold Debug Project", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to scaffold debug project:\n{ex.Message}",
                "Scaffold Debug Project", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string SampleCode() =>
        """
        // Inputs:  In1, In2, In3, In4  (object? — cast to the expected type)
        // Outputs: Image (Mat?), Out1, Out2, Out3, Out4  (object?)

        var src = (Mat)In1;
        var dst = new Mat();
        Cv2.GaussianBlur(src, dst, new Size(5, 5), 0);
        Image = dst;
        """;

    private static string Csproj() =>
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="OpenCvSharp4.Windows" Version="4.13.0.20260302" />
            <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Scripting" Version="5.3.0" />
          </ItemGroup>
        </Project>
        """;

    private static string Program(string scriptName) =>
        $$"""
        // Auto-generated debug runner for {{scriptName}}.cs
        // Set up test inputs below, then: dotnet run
        using System;
        using System.IO;
        using System.Linq;
        using OpenCvSharp;
        using Microsoft.CodeAnalysis.CSharp.Scripting;
        using Microsoft.CodeAnalysis.Scripting;

        var globals = new ScriptGlobals
        {
            In1 = Cv2.ImRead("test.png", ImreadModes.Color), // TODO: set your test input
            In2 = null, // TODO: set if used
            In3 = null,
            In4 = null,
        };

        var scriptPath = Path.GetFullPath("../{{scriptName}}.cs");
        var code = File.ReadAllText(scriptPath);
        var options = ScriptOptions.Default
            .AddImports("System", "System.Linq", "System.Collections.Generic",
                        "OpenCvSharp", "OpenCvSharp.Features2D")
            .AddReferences(typeof(Mat).Assembly, typeof(object).Assembly,
                           typeof(Enumerable).Assembly);

        await CSharpScript.RunAsync(code, options, globals);

        if (globals.Image is Mat img) { Cv2.ImWrite("out_Image.png", img); Console.WriteLine("Saved out_Image.png"); }
        if (globals.Out1 != null) Console.WriteLine($"Out1 ({globals.Out1.GetType().Name}): {globals.Out1}");
        if (globals.Out2 != null) Console.WriteLine($"Out2 ({globals.Out2.GetType().Name}): {globals.Out2}");
        if (globals.Out3 != null) Console.WriteLine($"Out3 ({globals.Out3.GetType().Name}): {globals.Out3}");
        if (globals.Out4 != null) Console.WriteLine($"Out4 ({globals.Out4.GetType().Name}): {globals.Out4}");

        public class ScriptGlobals
        {
            public object? In1, In2, In3, In4;
            public Mat? Image;
            public object? Out1, Out2, Out3, Out4;
        }
        """;
}
