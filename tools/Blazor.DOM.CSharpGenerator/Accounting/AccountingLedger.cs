// Accounting ledger: every TypeScript symbol must be accounted for exactly.
// Outcomes: Projected | Excluded (with reason) | Deferred (named phase) | GenerationFailed.
// Ambiguous symbols fail unless an explicit override is provided.

using Blazor.DOM.CSharpGenerator.IR;

namespace Blazor.DOM.CSharpGenerator.Accounting;

public enum AccountingOutcome
{
    Projected,
    Excluded,
    Deferred,
    GenerationFailed,
}

public sealed record AccountingEntry(
    int Ordinal,
    string SymbolName,
    string DeclarationKind,
    string SemanticStatus,
    IReadOnlyList<string> Classifications,
    AccountingOutcome Outcome,
    string Reason,
    string? GeneratedFile = null,
    string? DeferredPhase = null
);

/// <summary>
/// Tracks the outcome for every TypeScript symbol. Produces an emitter manifest
/// that proves exact coverage and records intentional exclusions/deferrals.
/// </summary>
public sealed class AccountingLedger
{
    private readonly List<AccountingEntry> _entries = [];

    public IReadOnlyList<AccountingEntry> Entries => _entries;

    public void RecordProjected(SymbolModel symbol, string generatedFile)
        => Add(symbol, AccountingOutcome.Projected, "emitted", generatedFile: generatedFile);

    public void RecordExcluded(SymbolModel symbol, string reason)
        => Add(symbol, AccountingOutcome.Excluded, reason);

    public void RecordDeferred(SymbolModel symbol, string phase, string reason)
        => Add(symbol, AccountingOutcome.Deferred, reason, deferredPhase: phase);

    public void RecordFailed(SymbolModel symbol, string reason)
        => Add(symbol, AccountingOutcome.GenerationFailed, reason);

    private void Add(
        SymbolModel symbol,
        AccountingOutcome outcome,
        string reason,
        string? generatedFile = null,
        string? deferredPhase = null)
    {
        var kind = symbol.Declarations.Count > 0
            ? symbol.Declarations[0].Kind
            : "unknown";

        _entries.Add(new AccountingEntry(
            symbol.Ordinal,
            symbol.Name,
            kind,
            symbol.Semantic.Status,
            symbol.Semantic.Classifications,
            outcome,
            reason,
            generatedFile,
            deferredPhase));
    }

    /// <summary>
    /// Validates that every symbol has exactly one entry and no symbol is silently skipped.
    /// </summary>
    public AccountingValidationResult Validate(int expectedCount)
    {
        var missing = new List<int>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new List<string>();

        foreach (var entry in _entries)
        {
            if (!seen.Add(entry.SymbolName))
                duplicates.Add(entry.SymbolName);
        }

        var totals = new Dictionary<AccountingOutcome, int>();
        foreach (var e in _entries)
            totals[e.Outcome] = totals.TryGetValue(e.Outcome, out var v) ? v + 1 : 1;

        var isValid = _entries.Count == expectedCount && duplicates.Count == 0;
        return new AccountingValidationResult(
            isValid,
            _entries.Count,
            expectedCount,
            duplicates,
            totals);
    }

    public EmitterManifest BuildManifest(
        string generatorVersion,
        string dataDirectory,
        ManifestModel sourceManifest)
    {
        var projected = _entries.Where(e => e.Outcome == AccountingOutcome.Projected).ToList();
        var excluded = _entries.Where(e => e.Outcome == AccountingOutcome.Excluded).ToList();
        var deferred = _entries.Where(e => e.Outcome == AccountingOutcome.Deferred).ToList();
        var failed = _entries.Where(e => e.Outcome == AccountingOutcome.GenerationFailed).ToList();

        return new EmitterManifest(
            SchemaVersion: 1,
            GeneratorVersion: generatorVersion,
            SourceManifest: new ManifestReference(
                dataDirectory,
                sourceManifest.Files.TypescriptSymbols.Sha256,
                sourceManifest.Files.WebIdlSymbols.Sha256),
            Accounting: new AccountingSummary(
                TotalSymbols: _entries.Count,
                Projected: projected.Count,
                Excluded: excluded.Count,
                Deferred: deferred.Count,
                GenerationFailed: failed.Count,
                ProjectedSymbols: projected.Select(e => e.SymbolName).ToList(),
                ExcludedSymbols: excluded.Select(e =>
                    new ExcludedEntry(e.SymbolName, e.Reason)).ToList(),
                DeferredSymbols: deferred.Select(e =>
                    new DeferredEntry(e.SymbolName, e.DeferredPhase ?? "unknown", e.Reason)).ToList(),
                FailedSymbols: failed.Select(e =>
                    new FailedEntry(e.SymbolName, e.Reason)).ToList()));
    }
}

public sealed record AccountingValidationResult(
    bool IsValid,
    int ActualCount,
    int ExpectedCount,
    IReadOnlyList<string> Duplicates,
    IReadOnlyDictionary<AccountingOutcome, int> OutcomeTotals);

public sealed record EmitterManifest(
    int SchemaVersion,
    string GeneratorVersion,
    ManifestReference SourceManifest,
    AccountingSummary Accounting);

public sealed record ManifestReference(
    string DataDirectory,
    string TypescriptSymbolsSha256,
    string WebIdlSymbolsSha256);

public sealed record AccountingSummary(
    int TotalSymbols,
    int Projected,
    int Excluded,
    int Deferred,
    int GenerationFailed,
    IReadOnlyList<string> ProjectedSymbols,
    IReadOnlyList<ExcludedEntry> ExcludedSymbols,
    IReadOnlyList<DeferredEntry> DeferredSymbols,
    IReadOnlyList<FailedEntry> FailedSymbols);

public sealed record ExcludedEntry(string Symbol, string Reason);
public sealed record DeferredEntry(string Symbol, string Phase, string Reason);
public sealed record FailedEntry(string Symbol, string Reason);
