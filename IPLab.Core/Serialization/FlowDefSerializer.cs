using System.Text.Json;
using System.Text.Json.Serialization;
using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;

namespace IPLab.Core.Serialization;

/// <summary>Serializes and deserializes <see cref="IFlow"/> to/from pretty-printed camelCase JSON.</summary>
public static class FlowDefSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        WriteIndented          = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters             = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    // ── DTOs ────────────────────────────────────────────────────────────────

    private record FlowDto(List<OperatorDto>? Operators);

    private record OperatorDto(
        string Id,
        string DisplayName,
        string Type,
        List<ParameterDto>?  Parameters,
        List<DependencyDto>? Dependencies,
        double? X = null,
        double? Y = null);

    private record ParameterDto(string Name, JsonElement? Value, SourceRefDto? Source);

    private record SourceRefDto(string OperatorId, string Port);

    private record DependencyDto(
        string DependencyId,
        string OperatorId,
        ConnectionSide? SourceSide = null,
        ConnectionSide? TargetSide = null);

    // ── Serialize ────────────────────────────────────────────────────────────

    /// <summary>Serializes a flow (definition + layout) to a JSON string.</summary>
    public static string Serialize(IFlow flow)
    {
        var opLayouts  = flow.Layout.Operators.ToDictionary(o => o.OperatorId);
        var depLayouts = flow.Layout.Dependencies.ToDictionary(d => d.DependencyId);

        var dto = new FlowDto(flow.Def.Operators.Select(op =>
        {
            opLayouts.TryGetValue(op.Id, out var ol);
            return new OperatorDto(
                op.Id,
                op.DisplayName,
                op.Type.TypeName,
                op.Parameters.Select(ToParameterDto).ToList(),
                op.Dependencies.Select(d =>
                {
                    depLayouts.TryGetValue(d.DependencyId, out var dl);
                    return new DependencyDto(d.DependencyId, d.OperatorId,
                        dl?.SourceSide, dl?.TargetSide);
                }).ToList(),
                ol?.Position.X,
                ol?.Position.Y);
        }).ToList());

        return JsonSerializer.Serialize(dto, Options);
    }

    private static ParameterDto ToParameterDto(ParameterValue p) =>
        p.Source is { } src
            ? new ParameterDto(p.Name, null, new SourceRefDto(src.OperatorId, src.Port))
            : new ParameterDto(p.Name, JsonSerializer.SerializeToElement(p.Value, Options), null);

    // ── Deserialize ──────────────────────────────────────────────────────────

    /// <summary>Deserializes a JSON string produced by <see cref="Serialize"/> back into a <see cref="Flow"/>, using <paramref name="registry"/> to resolve operator types.</summary>
    public static Flow Deserialize(string json, OperatorRegistry registry)
    {
        var dto = JsonSerializer.Deserialize<FlowDto>(json, Options)
            ?? throw new JsonException("Failed to deserialize flow.");

        var operators = (dto.Operators ?? []).Select(opDto =>
        {
            var type   = registry.Resolve(opDto.Type);
            var schema = type.ParameterSchema.ToDictionary(p => p.Name);

            var parameters = (opDto.Parameters ?? []).Select(p =>
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
                Dependencies = (opDto.Dependencies ?? [])
                    .Select(d => new Dependency(d.DependencyId, d.OperatorId)).ToList()
            };
        }).ToList();

        var opLayouts = (dto.Operators ?? [])
            .Where(o => o.X.HasValue && o.Y.HasValue)
            .Select(o => new OperatorLayout(o.Id, new LayoutPoint(o.X!.Value, o.Y!.Value)));

        var depLayouts = (dto.Operators ?? [])
            .SelectMany(o => o.Dependencies ?? [])
            .Where(d => d.SourceSide.HasValue && d.TargetSide.HasValue)
            .Select(d => new DependencyLayout(d.DependencyId, d.SourceSide!.Value, d.TargetSide!.Value));

        return new Flow(new FlowDef(operators), new FlowLayout(opLayouts, depLayouts));
    }

    private static object? CoerceValue(JsonElement? element,
        Dictionary<string, ParameterDescriptor> schema, string name)
    {
        if (element is not { } el) return null;

        var paramType = schema.TryGetValue(name, out var desc) ? desc.Type : ParameterType.String;
        return paramType switch
        {
            ParameterType.Int        => Convert.ToInt32(el.GetDouble()),
            ParameterType.Double     => el.GetDouble(),
            ParameterType.Bool       => el.GetBoolean(),
            ParameterType.StringList => el.ValueKind == JsonValueKind.Array
                ? el.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray()
                : new[] { el.GetString() ?? string.Empty },
            _                        => el.GetString()
        };
    }
}
