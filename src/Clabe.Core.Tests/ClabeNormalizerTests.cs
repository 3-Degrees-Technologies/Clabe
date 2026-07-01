namespace Clabe.Core.Tests;

[TestFixture]
public class ClabeNormalizerTests
{
    [Test]
    public void Normalize_ShouldStripSeparatorsAndWhitespace()
    {
        var normalizer = new ClabeNormalizer();

        Assert.That(normalizer.Normalize("012 180 01234567890 9"), Is.EqualTo("012180012345678909"));
        Assert.That(normalizer.Normalize("012-180-01234567890-9"), Is.EqualTo("012180012345678909"));
        Assert.That(normalizer.Normalize("  012180012345678909  "), Is.EqualTo("012180012345678909"));
    }

    [Test]
    public void Normalize_ShouldReturnEmptyForBlankInput()
    {
        var normalizer = new ClabeNormalizer();

        Assert.That(normalizer.Normalize(null), Is.EqualTo(string.Empty));
        Assert.That(normalizer.Normalize(""), Is.EqualTo(string.Empty));
        Assert.That(normalizer.Normalize("   "), Is.EqualTo(string.Empty));
    }
}
