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

    [Test]
    public void EmbeddedDefault_ShouldResolveSwiftBicForMajorBanks()
    {
        var catalog = BankCatalog.EmbeddedDefault;

        // Externally verified head-office BICs (bank.codes / theswiftcodes.com,
        // 2026-08). Exact values matter: a wrong BIC misroutes a SWIFT payment.
        var expected = new Dictionary<string, string>
        {
            ["002"] = "BNMXMXMM", // Banamex
            ["012"] = "BCMRMXMM", // BBVA México (Bancomer)
            ["014"] = "BMSXMXMM", // Santander México
            ["021"] = "BIMEMXMM", // HSBC México
            ["030"] = "BJIOMXML", // Banco del Bajío
            ["036"] = "INBUMXMM", // Inbursa
            ["044"] = "MBCOMXMM", // Scotiabank México
            ["058"] = "RGIOMXMT", // Banregio
            ["072"] = "MENOMXMT", // Banorte
            ["127"] = "AZTKMXMM"  // Banco Azteca
        };

        foreach (var (code, bic) in expected)
        {
            Assert.That(TryResolve(catalog, code)?.SwiftBic, Is.EqualTo(bic), $"bank code {code}");
        }

        // SPEI-only participants (fintechs, transfer institutions) have no SWIFT
        // BIC — the catalog must say so rather than guess. 646 = STP.
        Assert.That(TryResolve(catalog, "646")?.SwiftBic, Is.Null);
    }

    [Test]
    public void EmbeddedDefault_SwiftBicsShouldBeWellFormedWhenPresent()
    {
        // ISO 9362: 8 or 11 alphanumeric characters, uppercase. Guards the
        // hand-curated data file against typos slipping into a release.
        var malformed = BankCatalog.EmbeddedDefault.Institutions
            .SelectMany(i => i.SwiftBics, (i, e) => (i.Code.Value, e.Bic))
            .Where(x => x.Bic.Length is not (8 or 11)
                || !x.Bic.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c)))
            .Select(x => $"{x.Value}={x.Bic}")
            .ToArray();

        Assert.That(malformed, Is.Empty);
    }

    [Test]
    public void SwiftBic_ShouldPickOnlyWhenUnambiguous()
    {
        // Single entry: identifiable.
        Assert.That(Institution("999", Entry("TESTMXMM")).SwiftBic, Is.EqualTo("TESTMXMM"));

        // Multiple entries but exactly one head office (City == null): identifiable.
        Assert.That(
            Institution("999", Entry("TESTMXMM"), Entry("TESTMXMMMTY", "Monterrey")).SwiftBic,
            Is.EqualTo("TESTMXMM"));

        // Multiple branch entries, no head office: ambiguous — must NOT guess.
        Assert.That(
            Institution("999", Entry("TESTMXMMMTY", "Monterrey"), Entry("TESTMXMMPUE", "Puebla")).SwiftBic,
            Is.Null);

        // Two head-office entries (bad data): ambiguous — must NOT guess.
        Assert.That(
            Institution("999", Entry("TESTMXMM"), Entry("OTHRMXMM")).SwiftBic,
            Is.Null);

        // No entries at all.
        Assert.That(Institution("999").SwiftBic, Is.Null);
    }

    [Test]
    public void ResolveSwiftBic_ShouldUseHintsToPickFromTheList()
    {
        var catalog = new BankCatalog(
            new[]
            {
                Institution("999", Entry("TESTMXMM"), Entry("TESTMXMMMTY", "Monterrey")),
                Institution("998", Entry("NOHQMXMMMTY", "Monterrey"), Entry("NOHQMXMMPUE", "Puebla"))
            },
            new BankCatalogSnapshot
            {
                AuthoritativeSource = "https://www.banxico.org.mx/cep-scl/listaInstituciones.do",
                SeededFrom = "unit-test",
                RetrievedOn = "2026-08-05"
            });
        var service = new ClabeValidationService(catalog);

        // City hint matches a branch entry (case-insensitive, trimmed).
        Assert.That(
            service.ResolveSwiftBic("999180012345678909", new SwiftBicResolutionHints { City = "  monterrey " }),
            Is.EqualTo("TESTMXMMMTY"));

        // No hints: fall back to the unambiguous pick (head office).
        Assert.That(service.ResolveSwiftBic("999180012345678909"), Is.EqualTo("TESTMXMM"));

        // Unmatched hint: fall back to the unambiguous pick.
        Assert.That(
            service.ResolveSwiftBic("999180012345678909", new SwiftBicResolutionHints { City = "Cancun" }),
            Is.EqualTo("TESTMXMM"));

        // Branch-only institution: hint picks; without a hint there is no
        // identifiable BIC and the resolver must return null, not guess.
        Assert.That(
            service.ResolveSwiftBic("998180012345678900", new SwiftBicResolutionHints { City = "Puebla" }),
            Is.EqualTo("NOHQMXMMPUE"));
        Assert.That(service.ResolveSwiftBic("998180012345678900"), Is.Null);

        // Unknown bank code resolves nothing regardless of hints.
        Assert.That(
            service.ResolveSwiftBic("500000000000000000", new SwiftBicResolutionHints { City = "Monterrey" }),
            Is.Null);
    }

    [Test]
    public void ResolveSwiftBic_ShouldNotGuessWhenSeveralEntriesShareTheHintedCity()
    {
        // Real directories list several departmental codes in the same city
        // (e.g. four Banorte codes all in Monterrey). A city hint that matches
        // more than one entry identifies nothing — the resolver must fall back
        // to the unambiguous default, never pick one arbitrarily.
        var catalog = new BankCatalog(
            new[]
            {
                Institution("997",
                    Entry("TESTMXMM"),
                    Entry("TESTMXMMDER", "Monterrey"),
                    Entry("TESTMXMMFEX", "Monterrey")),
                Institution("996",
                    Entry("NOHQMXMMDER", "Monterrey"),
                    Entry("NOHQMXMMFEX", "Monterrey"))
            },
            new BankCatalogSnapshot
            {
                AuthoritativeSource = "https://www.banxico.org.mx/cep-scl/listaInstituciones.do",
                SeededFrom = "unit-test",
                RetrievedOn = "2026-08-05"
            });
        var service = new ClabeValidationService(catalog);

        // Ambiguous city match, head office present: fall back to it.
        Assert.That(
            service.ResolveSwiftBic("997180012345678901", new SwiftBicResolutionHints { City = "Monterrey" }),
            Is.EqualTo("TESTMXMM"));

        // Ambiguous city match, no head office: nothing identifiable.
        Assert.That(
            service.ResolveSwiftBic("996180012345678902", new SwiftBicResolutionHints { City = "Monterrey" }),
            Is.Null);
    }

    [Test]
    public void ResolveSwiftBic_ShouldMatchCityHintsAccentInsensitively()
    {
        // Mexican city names arrive both accented and unaccented ("León"/"Leon",
        // "Ciudad de México"/"Ciudad de Mexico") — the match must not care.
        var catalog = new BankCatalog(
            new[] { Institution("995", Entry("TESTMXMM"), Entry("TESTMXMMLEO", "León")) },
            new BankCatalogSnapshot
            {
                AuthoritativeSource = "https://www.banxico.org.mx/cep-scl/listaInstituciones.do",
                SeededFrom = "unit-test",
                RetrievedOn = "2026-08-05"
            });
        var service = new ClabeValidationService(catalog);

        Assert.That(
            service.ResolveSwiftBic("995180012345678903", new SwiftBicResolutionHints { City = "Leon" }),
            Is.EqualTo("TESTMXMMLEO"));
        Assert.That(
            service.ResolveSwiftBic("995180012345678903", new SwiftBicResolutionHints { City = "LEÓN" }),
            Is.EqualTo("TESTMXMMLEO"));
    }

    [Test]
    public void EmbeddedDefault_ShouldResolveBranchQualifiedBicsByCity()
    {
        var service = new ClabeValidationService();

        // Banamex (002) carries geographic branch entries; a city hint picks them,
        // accent-insensitively, while the head office remains the default.
        Assert.That(
            service.ResolveSwiftBic("002010077777777771", new SwiftBicResolutionHints { City = "Monterrey" }),
            Is.EqualTo("BNMXMXMMMTY"));
        Assert.That(
            service.ResolveSwiftBic("002010077777777771", new SwiftBicResolutionHints { City = "León" }),
            Is.EqualTo("BNMXMXMMLEO"));
        Assert.That(service.ResolveSwiftBic("002010077777777771"), Is.EqualTo("BNMXMXMM"));

        // BBVA (012): Guadalajara branch entry; unmatched cities fall back.
        Assert.That(
            service.ResolveSwiftBic("012320029937286769", new SwiftBicResolutionHints { City = "Guadalajara" }),
            Is.EqualTo("BCMRMXMMGUA"));
        Assert.That(
            service.ResolveSwiftBic("012320029937286769", new SwiftBicResolutionHints { City = "Zapopan" }),
            Is.EqualTo("BCMRMXMM"));

        // Scotiabank (044): geographic branch entries confirmed by directory dump + xe.com.
        Assert.That(
            service.ResolveSwiftBic("044180012345678906", new SwiftBicResolutionHints { City = "Puebla" }),
            Is.EqualTo("MBCOMXMMPUE"));
    }

    [Test]
    public void EmbeddedDefault_ShouldCoverTheVerifiedBankSet()
    {
        var catalog = BankCatalog.EmbeddedDefault;

        // Batches verified 2026-08 against two independent SWIFT directories each.
        var expected = new Dictionary<string, string>
        {
            ["001"] = "BDEMMXMM", // Banxico
            ["006"] = "BNCEMXMM", // Bancomext
            ["019"] = "EJERMXMM", // Banjercito
            ["042"] = "MIFEMXMM", // Banca Mifel
            ["059"] = "INXXMXMM", // Invex
            ["060"] = "SNABMXM1", // Bansí
            ["062"] = "AFIRMXMT", // Afirme
            ["106"] = "BOFAMXMX", // Bank of America México
            ["108"] = "BOTKMXMX", // MUFG México
            ["110"] = "CHASMXMX", // JP Morgan México
            ["112"] = "MONXMXMM", // Banco Monex
            ["124"] = "CITIMXMM", // Citi México
            ["126"] = "CSFBMXMM", // Credit Suisse México
            ["128"] = "AUMCMXMM", // Autofin
            ["129"] = "BARCMXMM", // Barclays México
            ["132"] = "MIMMMXMX", // Multiva
            ["133"] = "ACIOMXMM", // Actinver
            ["135"] = "NFSAMXMM", // Nafin
            ["136"] = "INTEMXMM", // Intercam Banco
            ["137"] = "BNNMMXMM", // BanCoppel
            ["139"] = "UBSWMXMM", // UBS México
            ["143"] = "CIMXMXMM", // CIBanco
            ["145"] = "BBSEMXMX", // Banco Base
            ["147"] = "BKOLMXMM", // Bankaool
            ["152"] = "BIBPMXMM", // Bancrea
            ["155"] = "ICBKMXMM", // ICBC México
            ["156"] = "BSABMXMM", // Sabadell México
            ["157"] = "SHBKMXMM", // Shinhan México
            ["158"] = "MHBMMXMM", // Mizuho México
            ["159"] = "BKCHMXMX", // Bank of China México
            ["160"] = "BSMXMXMM"  // Banco S3 (CACEIS)
        };

        foreach (var (code, bic) in expected)
        {
            Assert.That(TryResolve(catalog, code)?.SwiftBic, Is.EqualTo(bic), $"bank code {code}");
        }

        // Institutions confirmed to have NO SWIFT membership (or no verifiable
        // MX BIC) must stay empty: fintechs/SPEI-only (646 STP, 722 Mercado
        // Pago), defunct banks (131 Famsa), not-connected banks (140
        // Consubanco, 148 PagaTodo), Hey Banco (167, wires via parent), and
        // American Express México (103).
        foreach (var code in new[] { "103", "131", "140", "148", "167", "646" })
        {
            var institution = TryResolve(catalog, code);
            if (institution is not null)
            {
                Assert.That(institution.SwiftBics, Is.Empty, $"bank code {code} must have no BIC");
            }
        }
    }

    private static BankInstitution Institution(string code, params SwiftBicEntry[] swiftBics) => new()
    {
        Code = new BankCode { Value = code },
        ShortName = "TEST BANK",
        LongName = "Test Bank",
        SwiftBics = swiftBics
    };

    private static SwiftBicEntry Entry(string bic, string? city = null) => new() { Bic = bic, City = city };

    [Test]
    public void IdentifyBank_ShouldCarrySwiftBicFromFullClabe()
    {
        var service = new ClabeValidationService();

        // Full 18-digit CLABEs resolve through to the institution's BIC.
        Assert.That(service.IdentifyBank("012320029937286769")?.SwiftBic, Is.EqualTo("BCMRMXMM"));
        Assert.That(service.IdentifyBank("002010077777777771")?.SwiftBic, Is.EqualTo("BNMXMXMM"));

        // Unknown bank code resolves to no institution at all.
        Assert.That(service.IdentifyBank("500000000000000000"), Is.Null);
    }

    private static BankInstitution? TryResolve(IBankCatalog catalog, string code) =>
        catalog.TryResolve(new BankCode { Value = code }, out var institution) ? institution : null;
}
