namespace Clabe.Core.Tests;

[TestFixture]
public class BankCatalogTests
{
    [Test]
    public void EmbeddedDefault_ShouldResolveWellKnownBankCodes()
    {
        var catalog = BankCatalog.EmbeddedDefault;

        // Assert presence and a non-empty name rather than exact strings: bank names
        // are volatile data that the refresh script may legitimately change (e.g.
        // "BBVA BANCOMER" -> "BBVA MEXICO"). These anchor codes always exist.
        foreach (var code in new[] { "002", "012", "072", "646" })
        {
            var institution = TryResolve(catalog, code);
            Assert.That(institution, Is.Not.Null, $"anchor bank {code} should resolve");
            Assert.That(institution!.ShortName, Is.Not.Empty);
        }

        // A code not present in the snapshot returns nothing.
        Assert.That(TryResolve(catalog, "500"), Is.Null);
    }

    [Test]
    public void EmbeddedDefault_ShouldExposeProvenanceAndAContentfulCatalog()
    {
        var catalog = BankCatalog.EmbeddedDefault;

        Assert.That(catalog.Institutions.Count, Is.GreaterThan(50));
        Assert.That(catalog.Snapshot.AuthoritativeSource, Does.Contain("banxico.org.mx"));
        Assert.That(catalog.Snapshot.RetrievedOn, Is.Not.Empty);

        // Every entry is a well-formed three-digit code with names.
        Assert.That(catalog.Institutions.All(i => i.Code.Value.Length == 3), Is.True);
        Assert.That(catalog.Institutions.All(i => !string.IsNullOrWhiteSpace(i.ShortName)), Is.True);
    }

    [Test]
    public void Custom_ShouldSupportRefreshedDataWithoutCodeChanges()
    {
        // Demonstrates the refresh path: a catalog built from freshly-supplied data.
        var institutions = new[]
        {
            new BankInstitution
            {
                Code = new BankCode { Value = "999" },
                ShortName = "NEW BANK",
                LongName = "Newly Registered Bank"
            }
        };
        var catalog = new BankCatalog(
            institutions,
            new BankCatalogSnapshot
            {
                AuthoritativeSource = "https://www.banxico.org.mx/cep-scl/listaInstituciones.do",
                SeededFrom = "unit-test",
                RetrievedOn = "2026-07-01"
            });

        var service = new ClabeValidationService(catalog);

        Assert.That(service.IdentifyBank("999")?.ShortName, Is.EqualTo("NEW BANK"));
        // The default catalog does not know 999, proving the injected data is used.
        Assert.That(new ClabeValidationService().IdentifyBank("999"), Is.Null);
    }

    private static BankInstitution? TryResolve(IBankCatalog catalog, string code) =>
        catalog.TryResolve(new BankCode { Value = code }, out var institution) ? institution : null;
}
