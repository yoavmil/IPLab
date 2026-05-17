using System.Text.Json;
using System.Text.Json.Serialization;
using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;

namespace IPLab.Core.Serialization;

public static class FlowDefSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        WriteIndented               = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull
    };

    // ── DTOs ────────────────────────────────────────────────────────────────

    private record FlowDto(List<OperatorDto> Operators);

    private record OperatorDto(
        string Id,
        string DisplayName,
        string Type,
        List<ParameterDto> Parameters,
        List<DependencyDto> Dependencies);

    private record ParameterDto(string Name, JsonElement? Value, SourceRefDto? Source);

    private record SourceRefDto(string OperatorId, string Port);

    private record DependencyDto(string DependencyId, string OperatorId);

    // ── Serialize ────────────────────────────────────────────────────────────

    public static string Serialize(IFlowDef flow)
    {
        var dto = new FlowDto(flow.Operators.Select(op => new OperatorDto(
            op.Id,
            op.DisplayName,
            op.Type.TypeName,
            op.Parameters.Select(ToParameterDto).ToList(),
            op.Dependencies.Select(d => new DependencyDto(d.DependencyId, d.OperatorId)).ToList()
        )).ToList());

        return JsonSerializer.Serialize(dto, Options);
    }

    private static ParameterDto ToParameterDto(ParameterValue p) =>
        p.Source is { } src
            ? new ParameterDto(p.Name, null, new SourceRefDto(src.OperatorId, src.Port))
            : new ParameterDto(p.Name, JsonSerializer.SerializeToElement(p.Value, Options), null);

    // ── Deserialize ──────────────────────────────────────────────────────────

    public static FlowDef Deserialize(string json, OperatorRegistry registry)
    {
        var dto = JsonSerializer.Deserialize<FlowDto>(json, Options)
            ?? throw new JsonException("Failed to deserialize flow.");

        return new FlowDef(dto.Operators.Select(opDto =>
        {
            var type   = registry.Resolve(opDto.Type);
            var schema = type.ParameterSchema.ToDictionary(p => p.Name);

            var parameters = opDto.Parameters.Select(p =>
                p.Source is { } src
                    ? new ParameterValue { Name = p.Name, Source = new SourceRef(src.OperatorId, src.Port) }
                    : new ParameterValue { Name = p.Name, Value  = CoerceValue(p.Value, schema, p.Name) }
            ).ToList();

            return (IOperator)new Operator
            {
                Id           = opDto.Id,
                DisplayName  = opDto.DisplayName,
                Type         = type,
                Parameters   = parameters,
                Dependencies = opDto.Dependencies.Select(d => new Dependency(d.DependencyId, d.OperatorId)).ToList()
            };
        }));
    }

    private static object? CoerceValue(JsonElement? element, Dictionary<string, ParameterDescriptor> schema, string name)
    {
        if (element is not { } el) return null;

        var paramType = schema.TryGetValue(name, out var desc) ? desc.Type : ParameterType.String;
        return paramType switch
        {
            ParameterType.Int    => Convert.ToInt32(el.GetDouble()),
            ParameterType.Double => el.GetDouble(),
            ParameterType.Bool   => el.GetBoolean(),
            _                    => el.GetString()
        };
    }
}
