namespace IPLab.Core.Models;

/// <summary>Type-compatibility rules for connecting output ports to input parameters.</summary>
public static class PortTypeCompat
{
    /// <summary>
    /// Returns true when a port with <paramref name="portDataType"/> can feed a parameter
    /// of <paramref name="paramConnectableType"/>.
    /// Object is a wildcard (accepts any port type).
    /// Double accepts both double and int (widening).
    /// All other scalar types require an exact match.
    /// </summary>
    /// <param name="paramConnectableType">
    /// <see cref="ParameterDescriptor.ConnectableType"/> — the CLR type the parameter declares it accepts.
    /// Null means wildcard (accepts any port).
    /// </param>
    /// <param name="portDataType">
    /// <see cref="OutputPortDescriptor.DataType"/> — the CLR type the port actually emits.
    /// <c>typeof(object)</c> is the dynamic/script sentinel and is always accepted.
    /// </param>
    public static bool IsCompatible(Type? paramConnectableType, Type portDataType)
    {
        if (portDataType        == typeof(object)) return true;  // dynamic port → always ok
        if (paramConnectableType == null
         || paramConnectableType == typeof(object)) return true;  // wildcard param → always ok
        if (paramConnectableType.IsAssignableFrom(portDataType)) return true;
        // Numeric widening: an int port can feed a double parameter.
        if (paramConnectableType == typeof(double) && portDataType == typeof(int)) return true;
        return false;
    }
}
