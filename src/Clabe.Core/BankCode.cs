namespace Clabe.Core;

/// <summary>
/// A Mexican bank (institution) code: the three leading digits of a CLABE that
/// identify the financial institution. Values are always exactly three digits.
/// </summary>
public readonly record struct BankCode
{
    /// <summary>
    /// Gets the three-digit code value (e.g. "012").
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Attempts to create a <see cref="BankCode"/> from a raw string.
    /// </summary>
    /// <param name="raw">Candidate value; must be exactly three ASCII digits.</param>
    /// <param name="bankCode">The created code when successful; otherwise the default value.</param>
    /// <returns>True when the value is a valid three-digit code; otherwise false.</returns>
    public static bool TryCreate(string? raw, out BankCode bankCode)
    {
        if (raw is { Length: 3 } && raw.All(char.IsAsciiDigit))
        {
            bankCode = new BankCode { Value = raw };
            return true;
        }

        bankCode = default;
        return false;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
