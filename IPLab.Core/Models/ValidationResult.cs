namespace IPLab.Core.Models;

/// <summary>Result of a flow validation check.</summary>
/// <param name="IsValid"><see langword="true"/> when no errors were found.</param>
/// <param name="Errors">List of human-readable error messages; empty when valid.</param>
public record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    /// <summary>Returns a valid result with no errors.</summary>
    public static ValidationResult Ok() => new(true, []);
    /// <summary>Returns an invalid result with the given error messages.</summary>
    public static ValidationResult Fail(params string[] errors) => new(false, errors);
}
