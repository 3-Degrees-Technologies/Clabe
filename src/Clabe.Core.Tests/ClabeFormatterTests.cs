namespace Clabe.Core.Tests;

[TestFixture]
public class ClabeFormatterTests
{
    [Test]
    public void FormatForDisplay_ShouldGroupByStructuralSegments()
    {
        var formatter = new ClabeFormatter();

        Assert.That(formatter.FormatForDisplay("012180012345678909"), Is.EqualTo("012 180 01234567890 9"));
        Assert.That(formatter.FormatForDisplay("012 180 01234567890 9"), Is.EqualTo("012 180 01234567890 9"));
        Assert.That(formatter.FormatForDisplay("012-180-01234567890-9"), Is.EqualTo("012 180 01234567890 9"));
    }

    [Test]
    public void FormatForDisplay_ShouldReturnNormalizedInputWhenNotEighteenDigits()
    {
        var formatter = new ClabeFormatter();

        Assert.That(formatter.FormatForDisplay("0121800123"), Is.EqualTo("0121800123"));
        Assert.That(formatter.FormatForDisplay(""), Is.EqualTo(""));
        Assert.That(formatter.FormatForDisplay(null), Is.EqualTo(""));
    }
}
