namespace Clabe.Core;

/// <summary>
/// Computes and verifies the CLABE control (check) digit.
/// </summary>
/// <remarks>
/// The control digit is the 18th digit of a CLABE. It is derived from the first
/// 17 digits using a weighted modulus-10 scheme with the repeating weights
/// 3, 7, 1: each digit is multiplied by its weight, the product is reduced
/// modulo 10, the reduced products are summed, and the control digit is
/// <c>(10 - (sum mod 10)) mod 10</c>.
/// </remarks>
public static class ClabeCheckDigit
{
    /// <summary>The full 18-digit length of a CLABE.</summary>
    public const int ClabeLength = 18;

    private const int PayloadLength = 17;

    private static readonly int[] Weights =
        { 3, 7, 1, 3, 7, 1, 3, 7, 1, 3, 7, 1, 3, 7, 1, 3, 7 };

    /// <summary>
    /// Computes the control digit for the first 17 digits of a CLABE.
    /// </summary>
    /// <param name="payload">Exactly 17 ASCII digits (bank + plaza + account).</param>
    /// <returns>The control digit (0-9).</returns>
    /// <exception cref="ArgumentException">Thrown when the payload is not 17 digits.</exception>
    public static int Compute(ReadOnlySpan<char> payload)
    {
        if (payload.Length != PayloadLength)
        {
            throw new ArgumentException(
                $"CLABE payload must be exactly {PayloadLength} digits.", nameof(payload));
        }

        var sum = 0;
        for (var i = 0; i < PayloadLength; i++)
        {
            var digit = payload[i];
            if (!char.IsAsciiDigit(digit))
            {
                throw new ArgumentException(
                    "CLABE payload must contain only digits.", nameof(payload));
            }

            sum += (digit - '0') * Weights[i] % 10;
        }

        return (10 - sum % 10) % 10;
    }

    /// <summary>
    /// Determines whether an 18-digit CLABE has a valid control digit.
    /// </summary>
    /// <param name="normalizedClabe">A normalized 18-digit CLABE string.</param>
    /// <returns>True when the 18th digit matches the computed control digit.</returns>
    public static bool Matches(string normalizedClabe)
    {
        if (normalizedClabe is not { Length: ClabeLength } || !normalizedClabe.All(char.IsAsciiDigit))
        {
            return false;
        }

        var expected = Compute(normalizedClabe.AsSpan(0, PayloadLength));
        return normalizedClabe[PayloadLength] - '0' == expected;
    }
}
