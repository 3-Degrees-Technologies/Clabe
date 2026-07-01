// update-bank-catalog.fsx
// -----------------------------------------------------------------------------
// Refreshes the embedded CLABE bank catalog from Banxico's authoritative,
// continuously-updated list of SPEI participant institutions.
//
//   Source : https://www.banxico.org.mx/cep-scl/listaInstituciones.do
//   Output : ../src/Clabe.Core/Data/banxico-institutions.json (by default)
//
// Usage:
//   dotnet fsi tools/update-bank-catalog.fsx                 # write default file
//   dotnet fsi tools/update-bank-catalog.fsx --dry-run       # print summary only
//   dotnet fsi tools/update-bank-catalog.fsx <output-path>   # write elsewhere
//
// Notes:
//   * Banxico lists a 5-digit SPEI participant code (e.g. 40012 = BBVA). The
//     CLABE bank code is its last three digits (012), which is what we key on.
//   * The script is FAIL-SAFE: if the response cannot be parsed into a plausible
//     catalog (too few entries, or missing well-known anchor banks), it aborts
//     WITHOUT touching the output file. This protects the committed data from a
//     Banxico markup change or an error page silently blanking the catalog.
// -----------------------------------------------------------------------------

open System
open System.IO
open System.Net
open System.Net.Http
open System.Text.Json
open System.Text.Encodings.Web
open System.Text.RegularExpressions

let sourceUrl = "https://www.banxico.org.mx/cep-scl/listaInstituciones.do"

// ---- Args --------------------------------------------------------------------
let args = fsi.CommandLineArgs |> Array.tail
let dryRun = args |> Array.contains "--dry-run"

let scriptDir = __SOURCE_DIRECTORY__
let defaultOutput =
    Path.GetFullPath(Path.Combine(scriptDir, "..", "src", "Clabe.Core", "Data", "banxico-institutions.json"))

let outputPath =
    args
    |> Array.filter (fun a -> not (a.StartsWith "--"))
    |> Array.tryHead
    |> Option.map Path.GetFullPath
    |> Option.defaultValue defaultOutput

// ---- Model -------------------------------------------------------------------
type Institution = { code: string; shortName: string; longName: string }

// ---- Fetch -------------------------------------------------------------------
let fetch (url: string) =
    use handler = new HttpClientHandler(AutomaticDecompression = DecompressionMethods.All)
    use client = new HttpClient(handler)
    client.Timeout <- TimeSpan.FromSeconds 30.0
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Clabe.Core-catalog-updater/1.0")
    client.GetStringAsync(url) |> Async.AwaitTask |> Async.RunSynchronously

// ---- Parse -------------------------------------------------------------------
// Rows look like: <tr><td>40012</td><td>BBVA MEXICO</td></tr>
let rowRegex =
    Regex(@"<tr>\s*<td>\s*(?<code>\d{5})\s*</td>\s*<td>\s*(?<name>.*?)\s*</td>\s*</tr>",
          RegexOptions.IgnoreCase ||| RegexOptions.Singleline)

let parse (html: string) : Institution list =
    rowRegex.Matches html
    |> Seq.map (fun m ->
        let code5 = m.Groups.["code"].Value
        let clabeCode = code5.Substring(2)                      // last 3 digits
        let name = WebUtility.HtmlDecode(m.Groups.["name"].Value).Trim()
        { code = clabeCode; shortName = name; longName = name })
    // Distinct 5-digit codes can share a 3-digit CLABE code; keep the first.
    |> Seq.distinctBy (fun i -> i.code)
    |> Seq.sortBy (fun i -> i.code)
    |> List.ofSeq

// ---- Validate (fail-safe) ----------------------------------------------------
let anchors = [ "002"; "012"; "072" ]   // Banamex, BBVA, Banorte — must always be present

let validate (institutions: Institution list) =
    let codes = institutions |> List.map (fun i -> i.code) |> Set.ofList
    let missing = anchors |> List.filter (fun a -> not (codes.Contains a))
    let missingList = String.Join(", ", missing)
    if institutions.Length < 30 then
        Error $"Only {institutions.Length} institutions parsed (expected 30+). Refusing to overwrite."
    elif not (List.isEmpty missing) then
        Error $"Missing expected anchor bank codes: {missingList}. Refusing to overwrite."
    else
        Ok institutions

// ---- Load existing -----------------------------------------------------------
// The committed catalog may contain HISTORICAL codes (merged/defunct banks) that
// Banxico's current participant list no longer includes but which still appear in
// real, structurally-valid CLABEs (e.g. 032/IXE). We MERGE rather than replace so
// those are never dropped.
let loadExisting () : Institution list =
    if File.Exists outputPath then
        try
            use doc = JsonDocument.Parse(File.ReadAllText outputPath)
            doc.RootElement.GetProperty("institutions").EnumerateArray()
            |> Seq.map (fun e ->
                { code = e.GetProperty("code").GetString()
                  shortName = e.GetProperty("shortName").GetString()
                  longName = e.GetProperty("longName").GetString() })
            |> List.ofSeq
        with _ -> []
    else []

// ---- Write -------------------------------------------------------------------
let write (institutions: Institution list) =
    let doc =
        {| ``$comment`` =
             "Snapshot of Mexican SPEI participant bank codes used to resolve CLABE bank names. Regenerate with tools/update-bank-catalog.fsx."
           authoritativeSource = sourceUrl
           seededFrom = "Banxico CEP listaInstituciones.do (live)"
           retrievedOn = DateTime.UtcNow.ToString("yyyy-MM-dd")
           institutions = institutions |> List.toArray |}
    let options = JsonSerializerOptions(WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
    let json = JsonSerializer.Serialize(doc, options)
    File.WriteAllText(outputPath, json + "\n")

// ---- Merge -------------------------------------------------------------------
// Banxico is authoritative for the codes it lists (fresh names); existing-only
// codes are retained as historical entries.
let merge (existing: Institution list) (live: Institution list) =
    let liveByCode = live |> List.map (fun i -> i.code, i) |> Map.ofList
    let existingByCode = existing |> List.map (fun i -> i.code, i) |> Map.ofList
    let allCodes = Set.union (Map.keys liveByCode |> Set.ofSeq) (Map.keys existingByCode |> Set.ofSeq)
    allCodes
    |> Seq.map (fun c ->
        match Map.tryFind c liveByCode with
        | Some live -> live
        | None -> existingByCode.[c])
    |> Seq.sortBy (fun i -> i.code)
    |> List.ofSeq

// ---- Run ---------------------------------------------------------------------
printfn "Fetching %s ..." sourceUrl
let live = fetch sourceUrl |> parse

match validate live with
| Error message ->
    eprintfn "ABORT: %s" message
    exit 1
| Ok live ->
    let existing = loadExisting ()
    let existingByCode = existing |> List.map (fun i -> i.code, i) |> Map.ofList
    let merged = merge existing live

    let liveCodes = live |> List.map (fun i -> i.code) |> Set.ofList
    let existingCodes = existing |> List.map (fun i -> i.code) |> Set.ofList
    let added = Set.difference liveCodes existingCodes |> Set.toList |> List.sort
    let renamed =
        live
        |> List.filter (fun l -> match Map.tryFind l.code existingByCode with Some e -> e.shortName <> l.shortName | None -> false)
        |> List.map (fun i -> i.code)
        |> List.sort
    let retainedHistorical = Set.difference existingCodes liveCodes |> Set.toList |> List.sort

    printfn "Banxico listed %d institutions; merged catalog has %d." live.Length merged.Length
    if not (List.isEmpty added) then printfn "  + new codes (%d): %s" added.Length (String.Join(", ", added))
    if not (List.isEmpty renamed) then printfn "  ~ renamed (%d): %s" renamed.Length (String.Join(", ", renamed))
    if not (List.isEmpty retainedHistorical) then
        printfn "  = retained historical, not in current Banxico list (%d): %s"
            retainedHistorical.Length (String.Join(", ", retainedHistorical))

    if dryRun then
        printfn "Dry run — %s not modified." outputPath
    else
        write merged
        printfn "Wrote %s (%d institutions)" outputPath merged.Length
