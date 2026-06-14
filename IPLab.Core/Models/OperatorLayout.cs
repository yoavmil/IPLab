namespace IPLab.Core.Models;

/// <summary>Visual position of a single operator node on the flow canvas.</summary>
/// <param name="OperatorId">ID of the operator this layout entry describes.</param>
/// <param name="Position">Top-left position of the node in canvas space.</param>
public record OperatorLayout(string OperatorId, LayoutPoint Position);
