namespace IPLab.Core.Models;

public record SourceRef(string OperatorId, string Port);

public record ParameterValue
{
    public required string Name { get; init; }
    public object? Value { get; set; }
    public SourceRef? Source { get; init; }
}
