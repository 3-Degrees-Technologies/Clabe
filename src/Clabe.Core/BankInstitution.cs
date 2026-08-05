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

    /// <summary>
    /// Gets the institution's known SWIFT/BIC codes (ISO 9362). Empty when the
    /// institution has none: SPEI-only participants (fintechs, transfer
    /// institutions such as STP) are not SWIFT members, so an empty list is
    /// meaningful — callers must not guess. An entry with a null
    /// <see cref="SwiftBicEntry.City"/> is the head office.
    /// </summary>
    public IReadOnlyList<SwiftBicEntry> SwiftBics { get; init; } = Array.Empty<SwiftBicEntry>();

    /// <summary>
    /// Gets the single unambiguously identifiable SWIFT/BIC, or null. A BIC is
    /// identifiable when the institution has exactly one entry, or exactly one
    /// head-office entry among several. Anything else (no entries, multiple
    /// head offices, branch-only lists) yields null — for payment routing a
    /// wrong pick is worse than no pick. Use
    /// <see cref="IClabeValidationService.ResolveSwiftBic"/> to disambiguate
    /// branch entries with caller-held extra information.
    /// </summary>
    public string? SwiftBic
    {
        get
        {
            if (SwiftBics.Count == 1)
                return SwiftBics[0].Bic;

            var headOffices = SwiftBics.Where(e => e.City is null).Take(2).ToArray();
            return headOffices.Length == 1 ? headOffices[0].Bic : null;
        }
    }
}

/// <summary>
/// One SWIFT/BIC code belonging to a <see cref="BankInstitution"/>, optionally
/// qualified by the branch location it serves.
/// </summary>
public sealed record SwiftBicEntry
{
    /// <summary>
    /// Gets the ISO 9362 code: 8 characters for a head office, 11 for a branch
    /// (e.g. "BNMXMXMM", "BNMXMXMMMTY").
    /// </summary>
    public required string Bic { get; init; }

    /// <summary>
    /// Gets the branch city this code serves (e.g. "Monterrey"), or null for the
    /// institution's head office.
    /// </summary>
    public string? City { get; init; }
}
