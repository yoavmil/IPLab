using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>
/// Globals host for <see cref="CSharpScriptOperator"/>. Public fields are visible as top-level variables inside the user's C# script.
/// The script reads from <see cref="In1"/>–<see cref="In4"/> and writes to <see cref="Image"/> and <see cref="Out1"/>–<see cref="Out4"/>.
/// </summary>
public class ScriptGlobals
{
    /// <summary>First wired input, passed through from the operator's <c>In1</c> parameter.</summary>
    public object? In1;
    /// <summary>Second wired input, passed through from the operator's <c>In2</c> parameter.</summary>
    public object? In2;
    /// <summary>Third wired input, passed through from the operator's <c>In3</c> parameter.</summary>
    public object? In3;
    /// <summary>Fourth wired input, passed through from the operator's <c>In4</c> parameter.</summary>
    public object? In4;

    /// <summary>Primary image output. The script should assign the result <see cref="Mat"/> here.</summary>
    public Mat?    Image;
    /// <summary>First extra output, available on the operator's <c>Out1</c> port.</summary>
    public object? Out1;
    /// <summary>Second extra output, available on the operator's <c>Out2</c> port.</summary>
    public object? Out2;
    /// <summary>Third extra output, available on the operator's <c>Out3</c> port.</summary>
    public object? Out3;
    /// <summary>Fourth extra output, available on the operator's <c>Out4</c> port.</summary>
    public object? Out4;
}
