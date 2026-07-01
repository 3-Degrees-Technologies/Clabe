namespace Clabe.Core;

/// <summary>
/// A financial institution participating in Mexico's SPEI system, identified by
/// its three-digit <see cref="BankCode"/>. Used to display a human-readable bank
/// name for a CLABE, in the same way a BIC/SWIFT code resolves to a bank name.
/// </summary>
public sealed record BankInstitution
{
    /// <summary>
    /// Gets the three-digit institution code (the CLABE's leading digits).
    /// </summary>
    public required BankCode Code { get; init; }

    /// <summary>
    /// Gets the short, display-friendly bank name (e.g. "BBVA BANCOMER").
    /// </summary>
    public required string ShortName { get; init; }

    /// <summary>
    /// Gets the long, official institution name (e.g. "BBVA Bancomer").
    /// </summary>
    public required string LongName { get; init; }
}
