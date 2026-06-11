namespace IPLab.Core.Models;

/// <summary>Determines which UI control is rendered for a parameter and how its value is serialized.</summary>
public enum ParameterType
{
    /// <summary>Integer spin-box.</summary>
    Int,
    /// <summary>Floating-point spin-box.</summary>
    Double,
    /// <summary>Checkbox.</summary>
    Bool,
    /// <summary>Free-text input field.</summary>
    String,
    /// <summary>Drop-down list with a fixed set of choices declared in <see cref="ParameterDescriptor.Options"/>.</summary>
    Enum,
    /// <summary>Wire-only socket with no editable UI control. Default when <see cref="ParameterDescriptor.Type"/> is omitted.</summary>
    Object,
    /// <summary>Multi-value string list (e.g. a list of file paths).</summary>
    StringList
}

/// <summary>Execution state of an operator in the runtime.</summary>
public enum OperatorStatus
{
    /// <summary>Has not been executed in the current run.</summary>
    NotRun,
    /// <summary>Currently executing.</summary>
    Running,
    /// <summary>Completed successfully.</summary>
    Success,
    /// <summary>Threw an exception during execution.</summary>
    Failed,
    /// <summary>Excluded from execution (skipped by the executor).</summary>
    Disabled
}

/// <summary>Which edge of a node the visual connection wire attaches to.</summary>
public enum ConnectionSide
{
    /// <summary>Left edge.</summary>
    Left,
    /// <summary>Top edge.</summary>
    Top,
    /// <summary>Right edge.</summary>
    Right,
    /// <summary>Bottom edge.</summary>
    Bottom
}
