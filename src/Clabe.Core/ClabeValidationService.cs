using System.Globalization;
using System.Text;

namespace Clabe.Core;

/// <summary>
/// Implementation of <see cref="IClabeValidationService"/>. Validates the CLABE
/// structure and control digit, and resolves the bank name via an
/// <see cref="IBankCatalog"/>.
/// </summary>
public class ClabeValidationService : IClabeValidationService
{
    private const int ClabeLength = 18;
    private const int BankCodeLength = 3;
    private const int PlazaCodeLength = 3;
    private const int AccountNumberLength = 11;

    private static readonly ClabeNormalizer Normalizer = new();

    private readonly IBankCatalog _bankCatalog;

    /// <summary>
    /// Initializes a new instance using the bundled Banxico bank catalog snapshot.
    /// </summary>
    public ClabeValidationService()
        : this(BankCatalog.EmbeddedDefault)
    {
    }

    /// <summary>
    /// Initializes a new instance using the provided bank catalog. Supply a catalog
    /// backed by fresh data to keep bank-name resolution current.
    /// </summary>
    /// <param name="bankCatalog">The catalog used to resolve bank names.</param>
    public ClabeValidationService(IBankCatalog bankCatalog)
    {
        ArgumentNullException.ThrowIfNull(bankCatalog);
        _bankCatalog = bankCatalog;
    }

    /// <inheritdoc />
    public ValidationResult Validate(string? clabe)
    {
        var inputError = ValidateInput(clabe);
        if (inputError is not null)
        {
            return inputError.Value;
        }

        var normalized = Normalizer.Normalize(clabe);

        var formatError = ValidateFormat(normalized);
        if (formatError is not null)
        {
            return formatError.Value;
        }

        var bankCode = normalized[..BankCodeLength];
        var institution = ResolveInstitution(bankCode);
        return ValidationResult.Success(bankCode, institution);
    }

    /// <inheritdoc />
    public bool IsValid(string? clabe)
    {
        if (string.IsNullOrWhiteSpace(clabe))
        {
            return false;
        }

        var normalized = Normalizer.Normalize(clabe);
        return normalized.Length == ClabeLength
            && normalized.All(char.IsAsciiDigit)
            && ClabeCheckDigit.Matches(normalized);
    }

    /// <inheritdoc />
    public bool TryParse(string? clabe, out ParsedClabe? parsedClabe)
    {
        parsedClabe = null;

        if (!IsValid(clabe))
        {
            return false;
        }

        var normalized = Normalizer.Normalize(clabe);

        parsedClabe = new ParsedClabe
        {
            BankCode = new BankCode { Value = normalized[..BankCodeLength] },
            PlazaCode = normalized.Substring(BankCodeLength, PlazaCodeLength),
            AccountNumber = normalized.Substring(BankCodeLength + PlazaCodeLength, AccountNumberLength),
            CheckDigit = normalized[ClabeLength - 1],
            NormalizedClabe = normalized
        };
        return true;
    }

    /// <inheritdoc />
    public ValidationResult ValidateWithBankCheck(string? clabe)
    {
        var result = Validate(clabe);
        if (!result.IsValid)
        {
            return result;
        }

        if (result.BankShortName is null)
        {
            return ValidationResult.Failed(
                ClabeValidationError.ERR_BANK_CODE_UNKNOWN,
                $"Bank code '{result.BankCode}' is not recognized",
                result.BankCode);
        }

        return result;
    }

    /// <inheritdoc />
    public BankInstitution? IdentifyBank(string? clabe)
    {
        if (string.IsNullOrWhiteSpace(clabe))
        {
            return null;
        }

        var normalized = Normalizer.Normalize(clabe);
        return normalized.Length >= BankCodeLength
            ? ResolveInstitution(normalized[..BankCodeLength])
            : null;
    }

    /// <inheritdoc />
    public string? ResolveSwiftBic(string? clabe, SwiftBicResolutionHints? hints = null)
    {
        var institution = IdentifyBank(clabe);
        if (institution is null)
        {
            return null;
        }

        if (hints?.City is { } city && !string.IsNullOrWhiteSpace(city))
        {
            // A hint identifies a branch entry only when it matches EXACTLY ONE.
            // Directories list several departmental codes in the same city, so a
            // multi-match is ambiguity, not identification — fall through to the
            // unambiguous default rather than pick one arbitrarily.
            var cityMatches = institution.SwiftBics
                .Where(e => e.City is not null && CityEquals(e.City, city))
                .Take(2)
                .ToArray();
            if (cityMatches.Length == 1)
            {
                return cityMatches[0].Bic;
            }
        }

        return institution.SwiftBic;
    }

    /// <summary>
    /// Compares city names for hint matching: trimmed, case-insensitive, and
    /// accent-insensitive — Mexican city names arrive both accented and
    /// unaccented ("León"/"Leon", "Ciudad de México"/"Ciudad de Mexico").
    /// </summary>
    private static bool CityEquals(string left, string right) =>
        string.Equals(FoldCity(left), FoldCity(right), StringComparison.OrdinalIgnoreCase);

    private static string FoldCity(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Validates the raw input string, distinguishing null, empty, and whitespace-only inputs.
    /// </summary>
    private static ValidationResult? ValidateInput(string? clabe) => clabe switch
    {
        null => ValidationResult.Failed(ClabeValidationError.ERR_INPUT_NULL, "CLABE cannot be null"),
        { Length: 0 } => ValidationResult.Failed(ClabeValidationError.ERR_INPUT_EMPTY, "CLABE cannot be empty"),
        _ when string.IsNullOrWhiteSpace(clabe) =>
            ValidationResult.Failed(ClabeValidationError.ERR_INPUT_WHITESPACE, "CLABE cannot be whitespace only"),
        _ => null
    };

    /// <summary>
    /// Validates the structure (length, digits, control digit) of a normalized CLABE.
    /// </summary>
    /// <returns>A failed <see cref="ValidationResult"/> when invalid; otherwise null.</returns>
    private static ValidationResult? ValidateFormat(string normalized)
    {
        if (normalized.Length != ClabeLength)
        {
            return ValidationResult.Failed(
                ClabeValidationError.ERR_FORMAT_LENGTH,
                $"A CLABE must be {ClabeLength} digits long, but {normalized.Length} were provided");
        }

        if (!normalized.All(char.IsAsciiDigit))
        {
            return ValidationResult.Failed(
                ClabeValidationError.ERR_FORMAT_NON_NUMERIC,
                "A CLABE must contain only digits");
        }

        if (!ClabeCheckDigit.Matches(normalized))
        {
            return ValidationResult.Failed(
                ClabeValidationError.ERR_FORMAT_CHECKSUM,
                "Invalid CLABE check digit",
                normalized[..BankCodeLength]);
        }

        return null;
    }

    private BankInstitution? ResolveInstitution(string bankCode) =>
        BankCode.TryCreate(bankCode, out var code) && _bankCatalog.TryResolve(code, out var institution)
            ? institution
            : null;
}
