using IPLab.Core.Models;
using IPLab.Core.Runtime;
using OperatorStatus = IPLab.Core.Models.OperatorStatus;

namespace IPLab.UI.Services;

public class ExecutionService
{
    private FlowEx? _executor;
    private CancellationTokenSource? _cts;

    public bool HasResults => _executor is not null;

    public IReadOnlyDictionary<string, object?> IntermediateResults
        => _executor?.IntermediateResults ?? _empty;

    private static readonly Dictionary<string, object?> _empty = [];

    public event Action<string, OperatorStatus, Exception?>? StatusChanged;

    public async Task<bool> RunAsync(FlowDef flow)
    {
        if (_executor is null)
        {
            _executor = new FlowEx(flow);
            _executor.StatusChanged += (id, status, ex) => StatusChanged?.Invoke(id, status, ex);
        }
        else
        {
            _executor.UpdateFlow(flow);
        }

        _cts = new CancellationTokenSource();
        try
        {
            await _executor.RunAllAsync(_cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    public void Stop() => _cts?.Cancel();

    public void Clear()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts      = null;
        _executor = null;
    }
}
