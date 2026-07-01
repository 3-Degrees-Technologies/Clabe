namespace Clabe.Core;

/// <summary>
/// Provides CLABE formatting for human-readable display.
/// </summary>
public class ClabeFormatter
{
    private static readonly ClabeNormalizer Normalizer = new();

    /// <summary>
    /// Formats an 18-digit CLABE for display by grouping it into its structural
    /// segments: bank (3), plaza (3), account (11), and control digit (1),
    /// e.g. "012 180 01234567890 9".
    /// </summary>
    /// <param name="clabe">The CLABE to format (normalized or with separators).</param>
    /// <returns>The grouped CLABE, or the normalized input when it is not 18 digits.</returns>
    public string FormatForDisplay(string? clabe)
    {
        var normalized = Normalizer.Normalize(clabe);

        if (normalized.Length != 18)
        {
            return normalized;
        }

        return string.Join(
            ' ',
            normalized[..3],
            normalized.Substring(3, 3),
            normalized.Substring(6, 11),
            normalized[17..]);
    }
}
