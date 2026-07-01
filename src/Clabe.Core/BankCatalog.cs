using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clabe.Core;

/// <summary>
/// An in-memory <see cref="IBankCatalog"/> built from a fixed list of institutions.
/// </summary>
/// <remarks>
/// The default instance (<see cref="EmbeddedDefault"/>) is loaded from a bundled
/// Banxico-sourced snapshot. To keep bank names current, construct a catalog from
/// a freshly fetched institution list (e.g. a Banxico sync) rather than editing
/// code — the validation algorithm never changes, only this data does.
/// </remarks>
public sealed class BankCatalog : IBankCatalog
{
    private const string EmbeddedResourceName = "Clabe.Core.Data.banxico-institutions.json";

    private static readonly Lazy<BankCatalog> LazyEmbedded = new(LoadEmbeddedSnapshot);

    private readonly IReadOnlyDictionary<string, BankInstitution> _byCode;

    /// <summary>
    /// Initializes a new catalog from the provided institutions.
    /// </summary>
    /// <param name="institutions">The institutions to index.</param>
    /// <param name="snapshot">Provenance metadata describing where the data came from.</param>
    public BankCatalog(IReadOnlyList<BankInstitution> institutions, BankCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(institutions);

        // Deduplicate defensively: distinct 5-digit SPEI participant codes can reduce to
        // the same 3-digit CLABE bank code, so a refreshed data source may contain
        // duplicates. Keep the first occurrence rather than throwing on construction.
        var deduped = institutions
            .GroupBy(i => i.Code.Value, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        Institutions = deduped;
        Snapshot = snapshot;
        _byCode = deduped.ToDictionary(i => i.Code.Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the shared, lazily-loaded catalog built from the bundled Banxico snapshot.
    /// </summary>
    public static BankCatalog EmbeddedDefault => LazyEmbedded.Value;

    /// <inheritdoc />
    public IReadOnlyList<BankInstitution> Institutions { get; }

    /// <summary>
    /// Gets provenance metadata describing the source and retrieval date of the data.
    /// </summary>
    public BankCatalogSnapshot Snapshot { get; }

    /// <inheritdoc />
    public bool TryResolve(BankCode code, out BankInstitution? institution)
    {
        if (_byCode.TryGetValue(code.Value, out var found))
        {
            institution = found;
            return true;
        }

        institution = null;
        return false;
    }

    private static BankCatalog LoadEmbeddedSnapshot()
    {
        var assembly = typeof(BankCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded bank catalog resource '{EmbeddedResourceName}' was not found.");

        var document = JsonSerializer.Deserialize(stream, BankCatalogJsonContext.Default.BankCatalogDocument)
            ?? throw new InvalidOperationException("Embedded bank catalog could not be deserialized.");

        var institutions = document.Institutions
            .Where(i => BankCode.TryCreate(i.Code, out _))
            .Select(i => new BankInstitution
            {
                Code = new BankCode { Value = i.Code },
                ShortName = i.ShortName,
                LongName = i.LongName
            })
            .ToArray();

        var snapshot = new BankCatalogSnapshot
        {
            AuthoritativeSource = document.AuthoritativeSource,
            SeededFrom = document.SeededFrom,
            RetrievedOn = document.RetrievedOn
        };

        return new BankCatalog(institutions, snapshot);
    }
}

/// <summary>
/// Provenance metadata for a <see cref="BankCatalog"/>: where the data came from
/// and when it was retrieved.
/// </summary>
public sealed record BankCatalogSnapshot
{
    /// <summary>Gets the authoritative source URL for the bank codes (Banxico).</summary>
    public required string AuthoritativeSource { get; init; }

    /// <summary>Gets the source the bundled snapshot was seeded from.</summary>
    public required string SeededFrom { get; init; }

    /// <summary>Gets the date (ISO-8601) the snapshot was retrieved.</summary>
    public required string RetrievedOn { get; init; }
}

/// <summary>
/// DTO mirroring the embedded catalog JSON document.
/// </summary>
internal sealed record BankCatalogDocument
{
    [JsonPropertyName("authoritativeSource")]
    public string AuthoritativeSource { get; init; } = string.Empty;

    [JsonPropertyName("seededFrom")]
    public string SeededFrom { get; init; } = string.Empty;

    [JsonPropertyName("retrievedOn")]
    public string RetrievedOn { get; init; } = string.Empty;

    [JsonPropertyName("institutions")]
    public IReadOnlyList<BankCatalogEntry> Institutions { get; init; } = Array.Empty<BankCatalogEntry>();
}

/// <summary>
/// DTO mirroring a single institution entry in the embedded catalog JSON.
/// </summary>
internal sealed record BankCatalogEntry
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("shortName")]
    public string ShortName { get; init; } = string.Empty;

    [JsonPropertyName("longName")]
    public string LongName { get; init; } = string.Empty;
}

/// <summary>
/// Source-generated JSON context for trim/AOT-safe deserialization of the catalog.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BankCatalogDocument))]
internal sealed partial class BankCatalogJsonContext : JsonSerializerContext
{
}
