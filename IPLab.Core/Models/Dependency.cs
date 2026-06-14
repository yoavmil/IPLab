namespace IPLab.Core.Models;

/// <summary>Declares that one operator must complete before another in the execution graph.</summary>
/// <param name="DependencyId">Unique ID of this dependency edge (used for layout lookup).</param>
/// <param name="OperatorId">ID of the upstream operator that must run first.</param>
public record Dependency(string DependencyId, string OperatorId);
