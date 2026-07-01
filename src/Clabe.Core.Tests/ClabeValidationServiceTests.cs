namespace Clabe.Core.Tests;

[TestFixture]
public class ClabeValidationServiceTests
{
    // Independently-generated valid CLABEs (correct control digit) for known banks.
    private const string ValidIxe = "032180000118359719";       // bank 032 IXE (Wikipedia example)
    private const string ValidBbva = "012180012345678909";      // bank 012 BBVA
    private const string ValidBanorte = "072320098765432109";   // bank 072 BANORTE
    private const string ValidSantander = "014180000000000123"; // bank 014 SANTANDER

    // A fixed catalog so bank-name assertions are deterministic and independent of
    // the shipped (refreshable) snapshot. Structural/algorithm tests use the default.
    private static IClabeValidationService FixedCatalogService() =>
        new ClabeValidationService(new BankCatalog(
            new[]
            {
                Institution("012", "BBVA BANCOMER", "BBVA Bancomer"),
                Institution("072", "BANORTE", "Banco Mercantil del Norte"),
                Institution("014", "SANTANDER", "Banco Santander")
            },
            new BankCatalogSnapshot
            {
                AuthoritativeSource = "https://www.banxico.org.mx/cep-scl/listaInstituciones.do",
                SeededFrom = "unit-test",
                RetrievedOn = "2026-07-01"
            }));

    private static BankInstitution Institution(string code, string shortName, string longName) =>
        new() { Code = new BankCode { Value = code }, ShortName = shortName, LongName = longName };

    [Test]
    public void IsValid_ShouldDistinguishValidFromInvalidClabes()
    {
        var service = new ClabeValidationService();

        // Valid CLABEs
        Assert.That(service.IsValid(ValidIxe), Is.True);
        Assert.That(service.IsValid(ValidBbva), Is.True);
        Assert.That(service.IsValid(ValidBanorte), Is.True);

        // Bad control digit
        Assert.That(service.IsValid("012180012345678900"), Is.False);
        Assert.That(service.IsValid("032180000118359710"), Is.False);

        // Wrong length
        Assert.That(service.IsValid("01218001234567890"), Is.False);
        Assert.That(service.IsValid("0121800123456789099"), Is.False);

        // Non-numeric, empty, whitespace
        Assert.That(service.IsValid("01218001234567890X"), Is.False);
        Assert.That(service.IsValid(""), Is.False);
        Assert.That(service.IsValid("   "), Is.False);
        Assert.That(service.IsValid(null), Is.False);
    }

    [Test]
    public void IsValid_ShouldNormalizeSeparatorsAndWhitespace()
    {
        var service = new ClabeValidationService();

        Assert.That(service.IsValid("012 180 01234567890 9"), Is.True);
        Assert.That(service.IsValid("012-180-01234567890-9"), Is.True);
        Assert.That(service.IsValid("  012180012345678909  "), Is.True);

        // Normalization must not turn an invalid CLABE into a valid one
        Assert.That(service.IsValid("012 180 01234567890 0"), Is.False);
    }

    [Test]
    public void Validate_ShouldReportSpecificErrorCodesPerFailureMode()
    {
        var service = new ClabeValidationService();

        Assert.That(service.Validate(null).ErrorCode, Is.EqualTo(ClabeValidationError.ERR_INPUT_NULL));
        Assert.That(service.Validate("").ErrorCode, Is.EqualTo(ClabeValidationError.ERR_INPUT_EMPTY));
        Assert.That(service.Validate("   ").ErrorCode, Is.EqualTo(ClabeValidationError.ERR_INPUT_WHITESPACE));
        Assert.That(service.Validate("01218001234567890").ErrorCode, Is.EqualTo(ClabeValidationError.ERR_FORMAT_LENGTH));
        Assert.That(service.Validate("01218001234567890X").ErrorCode, Is.EqualTo(ClabeValidationError.ERR_FORMAT_NON_NUMERIC));
        Assert.That(service.Validate("012180012345678900").ErrorCode, Is.EqualTo(ClabeValidationError.ERR_FORMAT_CHECKSUM));

        var valid = service.Validate(ValidBbva);
        Assert.That(valid.IsValid, Is.True);
        Assert.That(valid.ErrorCode, Is.Null);
    }

    [Test]
    public void Validate_ShouldResolveBankNameForDisplay()
    {
        var service = FixedCatalogService();

        var bbva = service.Validate(ValidBbva);
        Assert.That(bbva.BankCode, Is.EqualTo("012"));
        Assert.That(bbva.BankShortName, Is.EqualTo("BBVA BANCOMER"));
        Assert.That(bbva.BankLongName, Is.EqualTo("BBVA Bancomer"));

        var banorte = service.Validate(ValidBanorte);
        Assert.That(banorte.BankCode, Is.EqualTo("072"));
        Assert.That(banorte.BankShortName, Is.EqualTo("BANORTE"));

        var santander = service.Validate(ValidSantander);
        Assert.That(santander.BankShortName, Is.EqualTo("SANTANDER"));
    }

    [Test]
    public void Validate_ShouldSurfaceBankCodeEvenWhenChecksumFails()
    {
        var service = new ClabeValidationService();

        // Checksum failure still exposes the parsed bank code for diagnostics.
        var result = service.Validate("012180012345678900");
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.BankCode, Is.EqualTo("012"));
    }

    [Test]
    public void ValidateWithBankCheck_ShouldFailForUnknownButStructurallyValidBankCode()
    {
        var service = new ClabeValidationService();

        // Bank code 500 is not a SPEI participant; build a structurally valid CLABE for it.
        var unknownBankClabe = BuildValidClabe("500", "180", "01234567890");

        // Structural validation passes...
        Assert.That(service.Validate(unknownBankClabe).IsValid, Is.True);

        // ...but the stricter bank check fails with a specific code.
        var strict = service.ValidateWithBankCheck(unknownBankClabe);
        Assert.That(strict.IsValid, Is.False);
        Assert.That(strict.ErrorCode, Is.EqualTo(ClabeValidationError.ERR_BANK_CODE_UNKNOWN));
        Assert.That(strict.BankCode, Is.EqualTo("500"));

        // A known bank passes the strict check.
        Assert.That(service.ValidateWithBankCheck(ValidBbva).IsValid, Is.True);
    }

    [Test]
    public void TryParse_ShouldSplitClabeIntoComponents()
    {
        var service = new ClabeValidationService();

        Assert.That(service.TryParse(ValidBbva, out var parsed), Is.True);
        Assert.That(parsed, Is.Not.Null);
        Assert.That(parsed!.Value.BankCode.Value, Is.EqualTo("012"));
        Assert.That(parsed.Value.PlazaCode, Is.EqualTo("180"));
        Assert.That(parsed.Value.AccountNumber, Is.EqualTo("01234567890"));
        Assert.That(parsed.Value.CheckDigit, Is.EqualTo('9'));
        Assert.That(parsed.Value.NormalizedClabe, Is.EqualTo(ValidBbva));

        // Invalid CLABEs do not parse
        Assert.That(service.TryParse("012180012345678900", out var bad), Is.False);
        Assert.That(bad, Is.Null);
        Assert.That(service.TryParse(null, out _), Is.False);
    }

    [Test]
    public void IdentifyBank_ShouldResolveInstitutionForDisplayLikeSwift()
    {
        var service = FixedCatalogService();

        var bank = service.IdentifyBank(ValidBanorte);
        Assert.That(bank, Is.Not.Null);
        Assert.That(bank!.Code.Value, Is.EqualTo("072"));
        Assert.That(bank.ShortName, Is.EqualTo("BANORTE"));

        // Resolves from just the leading digits, even without a full valid CLABE.
        Assert.That(service.IdentifyBank("012")!.ShortName, Is.EqualTo("BBVA BANCOMER"));

        // Unknown bank code / too short => null
        Assert.That(service.IdentifyBank("500180012345678900"), Is.Null);
        Assert.That(service.IdentifyBank("01"), Is.Null);
        Assert.That(service.IdentifyBank(null), Is.Null);
    }

    private static string BuildValidClabe(string bankCode, string plaza, string account)
    {
        var payload = bankCode + plaza + account;
        return payload + ClabeCheckDigit.Compute(payload);
    }
}
