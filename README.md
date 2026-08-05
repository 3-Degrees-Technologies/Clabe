# Clabe.Core - CLABE Validation Library

A validation, parsing, and bank-name resolution library for Mexican **CLABE**
(Clave Bancaria Estandarizada) account numbers, for .NET applications. It mirrors
the design of the sibling `Iban.Core` component.

## Why this component exists

A CLABE splits into a **stable algorithm** and **volatile data**:

- **Structure + control digit** — an 18-digit number (`3` bank code + `3` plaza
  code + `11` account + `1` control digit) validated with a weighted modulus-10
  check. This has not changed since 2004, so we implement it directly (no
  dependency to reinvent a 15-line algorithm).
- **Bank-code → bank-name catalog** — the set of Mexican banks changes over time.
  This is the part that goes stale, so it is kept as **refreshable data**, not
  hardcoded logic. The bundled snapshot is sourced from
  [Banxico's SPEI participant list](https://www.banxico.org.mx/cep-scl/listaInstituciones.do)
  and can be replaced/synced without any code change.

Existing options were unsuitable: the `ValidaCLABE` NuGet package is a single-dev
project last touched ~6 years ago with hardcoded, now-stale bank data, and
`clabe-validator` is TypeScript with the catalog embedded in source.

## Features

- ✅ **Structural validation** — length, digits, and control digit
- ✅ **Parsing** — bank code, plaza code, account number, control digit
- ✅ **Bank-name resolution** — display the institution like a SWIFT/BIC lookup
- ✅ **SWIFT/BIC resolution** — curated head-office BICs for major banks (`BankInstitution.SwiftBic`)
- ✅ **Refreshable catalog** — swap in fresh Banxico data via `IBankCatalog`
- ✅ **Structured errors** — `ClabeValidationError` codes, not bare booleans
- ✅ **Flexible input** — accepts spaces and hyphens

## Quick Start

```csharp
using Clabe.Core;

var service = new ClabeValidationService();

// Quick boolean check
bool ok = service.IsValid("012180012345678909");

// Detailed result + bank name for display
ValidationResult result = service.Validate("012 180 01234567890 9");
if (result.IsValid)
{
    Console.WriteLine(result.BankShortName); // "BBVA BANCOMER"
}
else
{
    Console.WriteLine($"{result.ErrorCode}: {result.ErrorMessage}");
}

// Resolve the bank for display (SWIFT-style), from a full or partial CLABE
BankInstitution? bank = service.IdentifyBank("072320098765432109"); // BANORTE

// The institution carries its head-office SWIFT/BIC when it has one.
// SPEI-only participants (fintechs, STP, …) have none — SwiftBic is null,
// which is meaningful: do not guess a BIC for them.
string? bic = bank?.SwiftBic; // "MENOMXMT"

// Parse into components
if (service.TryParse("012180012345678909", out var parsed))
{
    var p = parsed!.Value;
    // p.BankCode.Value == "012", p.PlazaCode == "180", p.AccountNumber, p.CheckDigit
}

// Stricter: require the bank code to be a known SPEI participant
ValidationResult strict = service.ValidateWithBankCheck("001180012345678900");
// strict.ErrorCode == ClabeValidationError.ERR_BANK_CODE_UNKNOWN
```

## Keeping the bank catalog current

The default `ClabeValidationService()` uses a bundled snapshot
(`BankCatalog.EmbeddedDefault`). To use fresher data, build a catalog from a list
you fetch/sync (e.g. from Banxico) and inject it:

```csharp
IBankCatalog catalog = new BankCatalog(freshInstitutions, snapshotInfo);
var service = new ClabeValidationService(catalog);
```

### Refreshing the embedded snapshot

`tools/update-bank-catalog.fsx` regenerates the bundled snapshot from Banxico's
live participant list. It maps each 5-digit SPEI code to its 3-digit CLABE code,
**merges** with the existing file (retaining historical codes such as `032`/IXE
that current-participant lists drop), and is fail-safe — it aborts without writing
if the response can't be parsed into a plausible catalog.

```bash
dotnet fsi tools/update-bank-catalog.fsx --dry-run   # preview added/renamed/retained
dotnet fsi tools/update-bank-catalog.fsx             # write the snapshot
```

Refreshing may update bank names to Banxico's current forms (e.g.
"BBVA BANCOMER" → "BBVA MEXICO"); tests assert names against a fixed catalog, not
the shipped snapshot, so a refresh won't break the build.

`swiftBic` values are curated by hand (Banxico's list carries no BIC data) and are
preserved as-is by the refresh script. When adding one, verify it against an
authoritative SWIFT directory first — a wrong BIC misroutes payments.

## Dependency Injection

```csharp
services.AddSingleton<IBankCatalog>(_ => BankCatalog.EmbeddedDefault);
services.AddScoped<IClabeValidationService, ClabeValidationService>();
```

## Requirements

- .NET 8.0 or later
