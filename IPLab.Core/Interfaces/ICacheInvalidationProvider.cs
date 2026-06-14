namespace IPLab.Core.Interfaces;

/// <summary>
/// Optional hook for operators whose result depends on external state that is not visible in their
/// parameter values — most commonly a file whose <em>contents</em> can change while its path stays
/// the same. <see cref="Runtime.FlowEx"/> folds the returned tokens into the cache key it compares
/// between runs, so a change in any token forces re-execution even when every parameter is unchanged.
/// </summary>
public interface ICacheInvalidationProvider
{
    /// <summary>
    /// Extra values to include in the operator's cache key. When any token differs from the previous
    /// run, the operator re-executes. Use stable, cheap-to-read values (e.g. a file's last-write
    /// time in ticks). Called with the same resolved parameters that are passed to Execute.
    /// </summary>
    IEnumerable<KeyValuePair<string, object?>> GetCacheTokens(IReadOnlyDictionary<string, object?> parameters);
}
