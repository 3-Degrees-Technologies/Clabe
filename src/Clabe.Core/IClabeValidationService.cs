namespace Clabe.Core;

/// <summary>
/// Service for validating and parsing Mexican CLABE (Clave Bancaria Estandarizada)
/// account numbers, and for resolving the associated bank name for display.
/// </summary>
public interface IClabeValidationService
{
    /// <summary>
    /// Validates the provided CLABE (length, digits, and control digit) and returns
    /// detailed results, including the bank name when the bank code is recognized.
    /// </summary>
    /// <param name="clabe">The CLABE string to validate. Can include spaces or hyphens.</param>
    /// <returns>A <see cref="ValidationResult"/> containing the validation status and any error.</returns>
    ValidationResult Validate(string? clabe);

    /// <summary>
    /// Validates whether the provided CLABE is structurally valid (length, digits, control digit).
    /// </summary>
    /// <param name="clabe">The CLABE string to validate. Can include spaces or hyphens.</param>
    /// <returns>True if the CLABE is valid; otherwise, false.</returns>
    bool IsValid(string? clabe);

    /// <summary>
    /// Attempts to parse the provided CLABE into its constituent parts.
    /// </summary>
    /// <param name="clabe">The CLABE string to parse. Can include spaces or hyphens.</param>
    /// <param name="parsedClabe">The parsed CLABE when successful; otherwise, null.</param>
    /// <returns>True if the CLABE was successfully parsed; otherwise, false.</returns>
    bool TryParse(string? clabe, out ParsedClabe? parsedClabe);

    /// <summary>
    /// Validates the provided CLABE and additionally requires the bank code to be
    /// present in the catalog. Structurally-valid CLABEs whose bank code is unknown
    /// fail with <see cref="ClabeValidationError.ERR_BANK_CODE_UNKNOWN"/>.
    /// </summary>
    /// <param name="clabe">The CLABE string to validate. Can include spaces or hyphens.</param>
    /// <returns>A <see cref="ValidationResult"/> containing the validation status and any error.</returns>
    ValidationResult ValidateWithBankCheck(string? clabe);

    /// <summary>
    /// Resolves the bank institution for a CLABE, for display purposes (as with a
    /// SWIFT/BIC code). Returns null when the CLABE is too short to contain a bank
    /// code or when the bank code is not in the catalog.
    /// </summary>
    /// <param name="clabe">The CLABE string. Can include spaces or hyphens.</param>
    /// <returns>The resolved <see cref="BankInstitution"/>, or null.</returns>
    BankInstitution? IdentifyBank(string? clabe);
}
