using OpenCvSharp;

namespace IPLab.Core.Operators;

// Globals host for CSharpScriptOperator — fields are visible as top-level variables in the script.
public class ScriptGlobals
{
    public object? In1;
    public object? In2;
    public object? In3;
    public object? In4;

    public Mat?    Image;  // primary image output
    public object? Out1;
    public object? Out2;
    public object? Out3;
    public object? Out4;
}
