namespace IPLab.Core.Models;

public record ParameterDescriptor
{
    // Stable programmatic key — used in code, JSON, and ParameterValue matching. Never rename after release.
    public required string Name { get; init; }
    // Human-readable string shown in the UI. Can be changed freely and localized.
    public required string Label { get; init; }
    public required ParameterType Type { get; init; }
    public bool IsConnectable { get; init; }
    public bool IsHidden { get; init; }
    public object? DefaultValue { get; init; }
    public object? Min { get; init; }
    public object? Max { get; init; }
    public string[]? Options { get; init; }
    public IReadOnlyList<ParameterDescriptor>? Children { get; init; }
    // When set, this parameter is visible only when the named sibling has one of the listed values.
    public string? ShowWhenParam { get; init; }
    public string[]? ShowWhenValues { get; init; }
}
