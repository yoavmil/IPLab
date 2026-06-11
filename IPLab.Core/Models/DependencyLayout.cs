namespace IPLab.Core.Models;

/// <summary>Visual routing metadata for a single dependency edge in the flow canvas.</summary>
/// <param name="DependencyId">ID matching the corresponding <see cref="Dependency"/>.</param>
/// <param name="SourceSide">Which side of the source node the connection leaves from.</param>
/// <param name="TargetSide">Which side of the target node the connection arrives at.</param>
public record DependencyLayout(string DependencyId, ConnectionSide SourceSide, ConnectionSide TargetSide);
