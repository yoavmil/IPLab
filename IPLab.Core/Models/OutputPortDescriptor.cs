namespace IPLab.Core.Models;

public record OutputPortDescriptor
{
    public required string Name           { get; init; }
    public required Type   DataType       { get; init; }
    public          bool   IsDisplayImage { get; init; } = false;
}
