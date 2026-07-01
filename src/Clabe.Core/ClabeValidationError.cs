namespace Clabe.Core;

/// <summary>
/// Standardized validation error codes for CLABE validation.
/// Error codes are organized by category:
/// - 1000-1999: Format validation errors
/// - 2000-2999: Bank (institution) validation errors
/// - 3000-3999: Input validation errors
/// </summary>
public enum ClabeValidationError
{
    // Format Validation Errors (1000-1999)

    /// <summary>
    /// Incorrect length - a CLABE must be exactly 18 digits.
    /// User-facing message: "A CLABE must be 18 digits long"
    /// Example: A 17- or 19-digit value.
    /// </summary>
    ERR_FORMAT_LENGTH = 1001,

    /// <summary>
    /// Non-numeric content - a CLABE must contain only digits.
    /// User-facing message: "A CLABE must contain only digits"
    /// Example: "012ABC012345678909"
    /// </summary>
    ERR_FORMAT_NON_NUMERIC = 1002,

    /// <summary>
    /// Control digit mismatch - the 18th digit does not match the computed check digit.
    /// User-facing message: "Invalid CLABE check digit"
    /// Example: A transcription error in any of the first 17 digits.
    /// </summary>
    ERR_FORMAT_CHECKSUM = 1003,

    // Bank Validation Errors (2000-2999)

    /// <summary>
    /// Unknown bank code - the three-digit institution code is not present in the
    /// bank catalog. The CLABE may still be structurally valid; the institution
    /// simply cannot be identified for display.
    /// User-facing message: "Unrecognized bank code"
    /// Example: A newly registered SPEI participant not yet in the local snapshot.
    /// </summary>
    ERR_BANK_CODE_UNKNOWN = 2001,

    // Input Validation Errors (3000-3999)

    /// <summary>
    /// Null input - the provided CLABE value is null.
    /// User-facing message: "CLABE cannot be null"
    /// </summary>
    ERR_INPUT_NULL = 3001,

    /// <summary>
    /// Empty string - the provided CLABE is an empty string.
    /// User-facing message: "CLABE cannot be empty"
    /// </summary>
    ERR_INPUT_EMPTY = 3002,

    /// <summary>
    /// Whitespace-only - the provided CLABE contains only whitespace characters.
    /// User-facing message: "CLABE cannot be whitespace only"
    /// </summary>
    ERR_INPUT_WHITESPACE = 3003
}
