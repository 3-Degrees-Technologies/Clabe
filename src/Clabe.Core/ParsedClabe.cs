namespace Clabe.Core;

/// <summary>
/// Represents a parsed CLABE with its constituent parts.
/// </summary>
/// <remarks>
/// A CLABE is 18 digits: a 3-digit bank code, a 3-digit plaza (branch/city) code,
/// an 11-digit account number, and a single control digit.
/// </remarks>
public readonly record struct ParsedClabe
{
    /// <summary>
    /// Gets the three-digit bank (institution) code.
    /// </summary>
    public required BankCode BankCode { get; init; }

    /// <summary>
    /// Gets the three-digit plaza (branch/city) code.
    /// </summary>
    public required string PlazaCode { get; init; }

    /// <summary>
    /// Gets the eleven-digit account number.
    /// </summary>
    public required string AccountNumber { get; init; }

    /// <summary>
    /// Gets the control (check) digit.
    /// </summary>
    public required char CheckDigit { get; init; }

    /// <summary>
    /// Gets the normalized 18-digit CLABE (no spaces or separators).
    /// </summary>
    public required string NormalizedClabe { get; init; }
}
