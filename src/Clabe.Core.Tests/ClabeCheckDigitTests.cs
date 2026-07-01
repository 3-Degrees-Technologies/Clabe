namespace Clabe.Core.Tests;

[TestFixture]
public class ClabeCheckDigitTests
{
    [Test]
    public void Compute_ShouldReturnControlDigitForKnownPayloads()
    {
        // Control digits verified against independently-generated valid CLABEs.
        // Payload is the first 17 digits; the expected value is the 18th digit.
        Assert.That(ClabeCheckDigit.Compute("03218000011835971"), Is.EqualTo(9));
        Assert.That(ClabeCheckDigit.Compute("01218001234567890"), Is.EqualTo(9));
        Assert.That(ClabeCheckDigit.Compute("07232009876543210"), Is.EqualTo(9));
        Assert.That(ClabeCheckDigit.Compute("01418000000000012"), Is.EqualTo(3));
        Assert.That(ClabeCheckDigit.Compute("00201001010101010"), Is.EqualTo(0));
    }

    [Test]
    public void Matches_ShouldDistinguishValidFromInvalidControlDigits()
    {
        // Valid full CLABEs (control digit correct)
        Assert.That(ClabeCheckDigit.Matches("032180000118359719"), Is.True);
        Assert.That(ClabeCheckDigit.Matches("012180012345678909"), Is.True);
        Assert.That(ClabeCheckDigit.Matches("014180000000000123"), Is.True);

        // Same CLABEs with a wrong control digit
        Assert.That(ClabeCheckDigit.Matches("032180000118359710"), Is.False);
        Assert.That(ClabeCheckDigit.Matches("012180012345678900"), Is.False);
        Assert.That(ClabeCheckDigit.Matches("014180000000000120"), Is.False);

        // Wrong length / non-numeric never match
        Assert.That(ClabeCheckDigit.Matches("01218001234567890"), Is.False);
        Assert.That(ClabeCheckDigit.Matches("0121800123456789099"), Is.False);
        Assert.That(ClabeCheckDigit.Matches("01218001234567890X"), Is.False);
    }

    [Test]
    public void Compute_ShouldRejectPayloadsThatAreNotSeventeenDigits()
    {
        Action tooShort = () => ClabeCheckDigit.Compute("123");
        Action sixteenDigits = () => ClabeCheckDigit.Compute("0121800123456789");
        Action nonDigit = () => ClabeCheckDigit.Compute("0121800123456789X");

        Assert.Throws<ArgumentException>(tooShort);
        Assert.Throws<ArgumentException>(sixteenDigits);
        Assert.Throws<ArgumentException>(nonDigit);
    }
}
