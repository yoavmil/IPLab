using System.Collections.Concurrent;
using System.Diagnostics;
using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;

namespace IPLab.Core.Tests;

/// <summary>Tests for serial, discrete, and parallel loop execution modes.</summary>
public class FlowExLoopTests
{
    // Produces a fixed-length int[] so LoopStart can count its items.
    private sealed class ArraySourceOp(int length) : IOperatorType
    {
        public string TypeName => "ArraySource";
        public string Category => "Test";
        public string Icon     => "";
        public IReadOnlyList<ParameterDescriptor> ParameterSchema => [];
        public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
            [new() { Name = "Items", DataType = typeof(int[]) }];

        public object? Execute(IReadOnlyDictionary<string, object?> _)
            => Enumerable.Range(0, length).ToArray();
    }

    // Body op: reads Index from LoopStart, records when it ran, outputs index^2.
    private sealed class SquareOp(
        int delayMs,
        ConcurrentDictionary<int, (long Start, long End)> log) : IOperatorType
    {
        public string TypeName => "Square";
        public string Category => "Test";
        public string Icon     => "";
        public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
            [new() { Name = "Index", Label = "Index", ConnectableType = typeof(int) }];
        public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
            [new() { Name = "Out", DataType = typeof(int) }];

        public object? Execute(IReadOnlyDictionary<string, object?> parameters)
        {
            int index = Convert.ToInt32(parameters["Index"]);
            long start = Stopwatch.GetTimestamp();
            Thread.Sleep(delayMs);
            log[index] = (start, Stopwatch.GetTimestamp());
            return index * index;
        }
    }

    // Builds the standard 4-operator loop: ArraySource → LoopStart → SquareOp → LoopEnd.
    private static FlowDef BuildLoopFlow(string modeName, int itemCount, IOperatorType squareOp) =>
        new([
            new Operator
            {
                Id           = "O1",
                DisplayName  = "Source",
                Type         = new ArraySourceOp(itemCount),
                Parameters   = [],
                Dependencies = [],
            },
            new Operator
            {
                Id           = "O2",
                DisplayName  = "LoopStart",
                Type         = new LoopStartOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Source", Source = new SourceRef("O1", "Items") },
                    new ParameterValue { Name = "Index",  Value  = 0 },
                    new ParameterValue { Name = "Mode",   Value  = modeName },
                ],
                Dependencies = [new Dependency("D_O1_O2", "O1")],
            },
            new Operator
            {
                Id           = "O3",
                DisplayName  = "Square",
                Type         = squareOp,
                Parameters   = [new ParameterValue { Name = "Index", Source = new SourceRef("O2", "Index") }],
                Dependencies = [new Dependency("D_O2_O3", "O2")],
            },
            new Operator
            {
                Id           = "O4",
                DisplayName  = "LoopEnd",
                Type         = new LoopEndOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Index", Source = new SourceRef("O2", "Index") },
                    new ParameterValue { Name = "In1",   Value  = null },
                    new ParameterValue { Name = "In2",   Source = new SourceRef("O3", "Out") },
                    new ParameterValue { Name = "In3",   Value  = null },
                    new ParameterValue { Name = "In4",   Value  = null },
                ],
                Dependencies = [new Dependency("D_O3_O4", "O3")],
            },
        ]);

    // Extracts the Out2 array from LoopEnd's accumulated result.
    private static object?[] GetOut2(FlowEx ex) =>
        (object?[])((IReadOnlyDictionary<string, object?>)ex.IntermediateResults["O4"]!)["Out2"]!;

    // ---- Serial ----

    /// <summary>Serial mode must run all iterations in order and produce correct squared values.</summary>
    [Fact]
    public async Task Serial_RunsAllIterationsInOrder_CorrectResults()
    {
        const int n = 4;
        var log  = new ConcurrentDictionary<int, (long Start, long End)>();
        var flow = BuildLoopFlow(nameof(LoopMode.Serial), n, new SquareOp(20, log));
        var ex   = new FlowEx(flow);

        await ex.RunAllAsync();

        var out2 = GetOut2(ex);
        Assert.Equal(n, out2.Length);
        for (int i = 0; i < n; i++)
            Assert.Equal(i * i, out2[i]);

        // Serial: iteration i must finish before iteration i+1 starts.
        for (int i = 0; i < n - 1; i++)
            Assert.True(log[i].End <= log[i + 1].Start,
                $"Serial: iteration {i} ended after iteration {i + 1} started.");
    }

    // ---- Discrete ----

    /// <summary>Discrete mode must run exactly once at the user-set index and populate only that slot.</summary>
    [Fact]
    public async Task Discrete_RunsOnlyAtSelectedIndex_OtherSlotsNull()
    {
        const int n     = 4;
        const int index = 2;
        var log  = new ConcurrentDictionary<int, (long Start, long End)>();
        var flow = BuildLoopFlow(nameof(LoopMode.Discrete), n, new SquareOp(0, log));

        // Override Index to 2 by mutating the ParameterValue in place (Value has a setter).
        var indexParam = flow.Operators.First(o => o.Id == "O2")
            .Parameters.First(p => p.Name == "Index");
        indexParam.Value = index;

        var ex = new FlowEx(flow);
        await ex.RunAllAsync();
        var out2 = GetOut2(ex);

        Assert.Equal(n, out2.Length);
        Assert.Equal(index * index, out2[index]);
        for (int i = 0; i < n; i++)
            if (i != index) Assert.Null(out2[i]);

        Assert.Single(log); // only one body execution
        Assert.True(log.ContainsKey(index));
    }

    // ---- Failure propagation ----

    private sealed class AlwaysFailsOp : IOperatorType
    {
        public string TypeName => "AlwaysFails";
        public string Category => "Test";
        public string Icon     => "";
        public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
            [new() { Name = "Index", Label = "Index", ConnectableType = typeof(int) }];
        public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
            [new() { Name = "Out", DataType = typeof(int) }];

        public object? Execute(IReadOnlyDictionary<string, object?> _) =>
            throw new InvalidOperationException("body op failed on purpose");
    }

    /// <summary>LoopStart must turn Failed when a body operator throws.</summary>
    [Fact]
    public async Task Serial_BodyOpFails_LoopStartStatusIsFailed()
    {
        var flow = BuildLoopFlow(nameof(LoopMode.Serial), 3, new AlwaysFailsOp());
        var ex   = new FlowEx(flow);

        await ex.RunAllAsync();

        Assert.Equal(OperatorStatus.Failed, ex.Statuses["O2"]); // O2 = LoopStart
    }

    /// <summary>LoopStart must turn Failed in Discrete mode when the body operator throws.</summary>
    [Fact]
    public async Task Discrete_BodyOpFails_LoopStartStatusIsFailed()
    {
        var flow = BuildLoopFlow(nameof(LoopMode.Discrete), 3, new AlwaysFailsOp());
        var ex   = new FlowEx(flow);

        await ex.RunAllAsync();

        Assert.Equal(OperatorStatus.Failed, ex.Statuses["O2"]);
    }

    // ---- Parallel ----

    /// <summary>Parallel mode must run all iterations concurrently and produce correct results in index order.</summary>
    [Fact]
    public async Task Parallel_RunsAllIterationsConcurrently_CorrectResults()
    {
        const int n     = 4;
        const int delay = 80;
        var log  = new ConcurrentDictionary<int, (long Start, long End)>();
        var flow = BuildLoopFlow(nameof(LoopMode.Parallel), n, new SquareOp(delay, log));
        var ex   = new FlowEx(flow);

        var sw = Stopwatch.StartNew();
        await ex.RunAllAsync();
        sw.Stop();

        // Correctness: Out2[i] == i*i for all i.
        var out2 = GetOut2(ex);
        Assert.Equal(n, out2.Length);
        for (int i = 0; i < n; i++)
            Assert.Equal(i * i, out2[i]);

        // Parallelism: all iterations should fit inside roughly one delay window.
        // Serial would take ≥ n*delay; parallel takes ≈ 1*delay.
        Assert.True(sw.ElapsedMilliseconds < delay * 2.5,
            $"Expected parallel execution (~{delay} ms) but took {sw.ElapsedMilliseconds} ms.");

        // Direct overlap check: every iteration must overlap with at least one other.
        for (int i = 0; i < n; i++)
        {
            bool overlapsAny = Enumerable.Range(0, n)
                .Where(j => j != i)
                .Any(j => log[i].Start < log[j].End && log[j].Start < log[i].End);
            Assert.True(overlapsAny, $"Iteration {i} did not overlap with any other iteration.");
        }
    }
}
