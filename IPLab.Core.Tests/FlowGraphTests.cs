using IPLab.Core.Utilities;

namespace IPLab.Core.Tests;

public class FlowGraphTests
{
    // A→B→C→D (linear chain)
    private static readonly (string, string)[] Linear =
    [
        ("A", "B"), ("B", "C"), ("C", "D")
    ];

    // A→C, B→C, C→D (diamond)
    private static readonly (string, string)[] Diamond =
    [
        ("A", "C"), ("B", "C"), ("C", "D")
    ];

    // ── GetAncestors ─────────────────────────────────────────────────────

    [Fact]
    public void GetAncestors_LinearChain_ReturnsAllUpstream()
    {
        var result = FlowGraph.GetAncestors("D", Linear);
        Assert.Equal(new HashSet<string> { "A", "B", "C" }, result);
    }

    [Fact]
    public void GetAncestors_Middle_ReturnsOnlyUpstream()
    {
        var result = FlowGraph.GetAncestors("C", Linear);
        Assert.Equal(new HashSet<string> { "A", "B" }, result);
    }

    [Fact]
    public void GetAncestors_Root_ReturnsEmpty()
    {
        var result = FlowGraph.GetAncestors("A", Linear);
        Assert.Empty(result);
    }

    [Fact]
    public void GetAncestors_DoesNotIncludeNodeItself()
    {
        var result = FlowGraph.GetAncestors("B", Linear);
        Assert.DoesNotContain("B", result);
    }

    [Fact]
    public void GetAncestors_Diamond_ReturnsBothBranches()
    {
        var result = FlowGraph.GetAncestors("D", Diamond);
        Assert.Equal(new HashSet<string> { "A", "B", "C" }, result);
    }

    [Fact]
    public void GetAncestors_AfterEdgeRemoved_ExcludesOrphanedAncestor()
    {
        // Simulates deleting C→D from the linear chain: only A→B and B→C remain.
        var edges = new[] { ("A", "B"), ("B", "C") };
        var result = FlowGraph.GetAncestors("D", edges);
        Assert.Empty(result);
    }

    // ── GetSelfAndDescendants ─────────────────────────────────────────────

    [Fact]
    public void GetSelfAndDescendants_LinearChain_IncludesSelfAndAll()
    {
        var result = FlowGraph.GetSelfAndDescendants("B", Linear);
        Assert.Equal(["B", "C", "D"], result);
    }

    [Fact]
    public void GetSelfAndDescendants_Leaf_ReturnsSelfOnly()
    {
        var result = FlowGraph.GetSelfAndDescendants("D", Linear);
        Assert.Equal(["D"], result);
    }

    [Fact]
    public void GetSelfAndDescendants_Root_ReturnsAll()
    {
        var result = FlowGraph.GetSelfAndDescendants("A", Linear);
        Assert.Equal(["A", "B", "C", "D"], result);
    }

    [Fact]
    public void GetSelfAndDescendants_Diamond_NoDuplicates()
    {
        var result = FlowGraph.GetSelfAndDescendants("A", Diamond);
        Assert.Equal(result.Distinct().Count(), result.Count);
        Assert.Contains("C", result);
        Assert.Contains("D", result);
    }
}
