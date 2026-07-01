namespace Clabe.Core;

/// <summary>
/// Provides CLABE normalization: trimming whitespace and removing common
/// separators so that user-entered values can be validated consistently.
/// </summary>
public class ClabeNormalizer
{
    /// <summary>
    /// Normalizes a CLABE string by trimming surrounding whitespace and removing
    /// spaces and hyphens. A CLABE is numeric, so no case conversion is required.
    /// </summary>
    /// <param name="input">The CLABE string to normalize.</param>
    /// <returns>The normalized CLABE, or an empty string when the input is null or blank.</returns>
    public string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        return input
            .Trim()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty);
    }
}
