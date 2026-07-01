namespace Clabe.Core;

/// <summary>
/// Represents the result of a CLABE validation operation.
/// </summary>
public readonly record struct ValidationResult
{
    /// <summary>
    /// Gets whether the CLABE is valid.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Gets the error code if validation failed; otherwise, null.
    /// Provides a structured, consistent error key for programmatic handling.
    /// </summary>
    public ClabeValidationError? ErrorCode { get; init; }

    /// <summary>
    /// Gets the error message if validation failed; otherwise, null.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the three-digit bank code when the CLABE was long enough to parse it; otherwise, null.
    /// </summary>
    public string? BankCode { get; init; }

    /// <summary>
    /// Gets the short, display-friendly bank name when the bank code was recognized; otherwise, null.
    /// </summary>
    public string? BankShortName { get; init; }

    /// <summary>
    /// Gets the long, official bank name when the bank code was recognized; otherwise, null.
    /// </summary>
    public string? BankLongName { get; init; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="bankCode">The three-digit bank code.</param>
    /// <param name="institution">The resolved institution, when the bank code was recognized.</param>
    public static ValidationResult Success(string? bankCode = null, BankInstitution? institution = null) => new()
    {
        IsValid = true,
        BankCode = bankCode,
        BankShortName = institution?.ShortName,
        BankLongName = institution?.LongName
    };

    /// <summary>
    /// Creates a failed validation result with an error code and message.
    /// </summary>
    /// <param name="errorCode">The structured error code.</param>
    /// <param name="errorMessage">The human-readable error message.</param>
    /// <param name="bankCode">The three-digit bank code if parseable.</param>
    /// <param name="institution">The resolved institution, when the bank code was recognized.</param>
    public static ValidationResult Failed(
        ClabeValidationError errorCode,
        string errorMessage,
        string? bankCode = null,
        BankInstitution? institution = null) => new()
    {
        IsValid = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
        BankCode = bankCode,
        BankShortName = institution?.ShortName,
        BankLongName = institution?.LongName
    };
}
