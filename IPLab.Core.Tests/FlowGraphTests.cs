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

    // ── TopologicalSort ──────────────────────────────────────────────────

    [Fact]
    public void TopologicalSort_LinearChain_IsInOrder()
    {
        var result = FlowGraph.TopologicalSort(["A", "B", "C", "D"], Linear);
        Assert.Equal(["A", "B", "C", "D"], result);
    }

    [Fact]
    public void TopologicalSort_Diamond_CBeforeD_ABBeforeC()
    {
        var result = FlowGraph.TopologicalSort(["A", "B", "C", "D"], Diamond).ToList();
        Assert.True(result.IndexOf("A") < result.IndexOf("C"));
        Assert.True(result.IndexOf("B") < result.IndexOf("C"));
        Assert.True(result.IndexOf("C") < result.IndexOf("D"));
    }

    [Fact]
    public void TopologicalSort_SubsetOfNodes_IgnoresOutsideEdges()
    {
        // Only sort B, C, D — edges referencing A are ignored.
        var result = FlowGraph.TopologicalSort(["B", "C", "D"], Linear).ToList();
        Assert.True(result.IndexOf("B") < result.IndexOf("C"));
        Assert.True(result.IndexOf("C") < result.IndexOf("D"));
        Assert.DoesNotContain("A", result);
    }

    [Fact]
    public void TopologicalSort_DisconnectedNodes_AllPresent()
    {
        var result = FlowGraph.TopologicalSort(["X", "Y"], Linear);
        Assert.Equal(2, result.Count);
        Assert.Contains("X", result);
        Assert.Contains("Y", result);
    }

    [Fact]
    public void TopologicalSort_SingleNode_ReturnsThatNode()
    {
        var result = FlowGraph.TopologicalSort(["A"], Linear);
        Assert.Equal(["A"], result);
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
