namespace Clabe.Core;

/// <summary>
/// A catalog that resolves a <see cref="BankCode"/> to a <see cref="BankInstitution"/>.
/// </summary>
/// <remarks>
/// The set of Mexican bank codes changes over time as institutions are added,
/// merged, or renamed. This abstraction keeps the (volatile) catalog data
/// separate from the (stable) validation algorithm, so the data source can be a
/// bundled snapshot today and a live Banxico sync later without touching callers.
/// </remarks>
public interface IBankCatalog
{
    /// <summary>
    /// Gets all institutions known to this catalog.
    /// </summary>
    IReadOnlyList<BankInstitution> Institutions { get; }

    /// <summary>
    /// Attempts to resolve a bank code to its institution.
    /// </summary>
    /// <param name="code">The three-digit bank code.</param>
    /// <param name="institution">The resolved institution when found; otherwise, null.</param>
    /// <returns>True when the code is present in the catalog; otherwise, false.</returns>
    bool TryResolve(BankCode code, out BankInstitution? institution);
}
