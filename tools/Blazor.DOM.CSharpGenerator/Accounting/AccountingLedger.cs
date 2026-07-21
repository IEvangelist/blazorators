// Accounting ledger: every TypeScript symbol must be accounted for exactly.
// Outcomes: Projected | ProjectedWithDeferredMembers | Excluded (with reason) | Deferred (named phase) | GenerationFailed.
// Ambiguous symbols fail unless an explicit override is provided.

using Blazor.DOM.CSharpGenerator.IR;
using System.Text.Json.Serialization;

namespace Blazor.DOM.CSharpGenerator.Accounting;

public enum AccountingOutcome
{
    Projected,
    ProjectedWithDeferredMembers,
    Excluded,
    Deferred,
    GenerationFailed,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MemberOutcomeStatus
{
    Projected,
    Deferred,
    Failed,
    NotAttemptedAfterFailure,
}

/// <summary>
/// Per-member outcome for structured member-level accounting.
/// </summary>
public sealed record MemberOutcome(
    int Ordinal,
    string Name,
    string Kind,
    MemberOutcomeStatus Status,
    string? Phase = null,
    string? Reason = null,
    int DeclarationOrdinal = 0,
    string? Provenance = null,
    string? SourceLocation = null,
    string? QualifiedKey = null);

public sealed record ParameterOutcome(
    int Ordinal,
    string Name,
    MemberOutcomeStatus Status,
    string? Phase,
    string Reason,
    string Provenance,
    string SourceLocation);

public sealed record OverloadOutcome(
    string QualifiedKey,
    int DeclarationOrdinal,
    int? MemberOrdinal,
    string Name,
    string Kind,
    MemberOutcomeStatus Status,
    string? Phase,
    string Reason,
    string Provenance,
    string SourceLocation,
    IReadOnlyList<ParameterOutcome> ParameterOutcomes);

public sealed record DeclarationOutcome(
    int Ordinal,
    string Kind,
    MemberOutcomeStatus Status,
    string? Phase,
    string Reason,
    string Provenance,
    string SourceLocation);

public sealed record AccountingEntry(
    int Ordinal,
    string SymbolName,
    string DeclarationKind,
    string SemanticStatus,
    IReadOnlyList<string> Classifications,
    AccountingOutcome Outcome,
    string Reason,
    string? GeneratedFile = null,
    string? DeferredPhase = null,
    IReadOnlyList<MemberOutcome>? MemberOutcomes = null,
    IReadOnlyList<DeclarationOutcome>? DeclarationOutcomes = null,
    IReadOnlyList<OverloadOutcome>? OverloadOutcomes = null,
    int ExpectedDeclarationCount = 0,
    int ExpectedMemberCount = 0,
    int ExpectedOverloadCount = 0,
    int ExpectedParameterCount = 0
);

/// <summary>
/// Tracks the outcome for every TypeScript symbol. Produces an emitter manifest
/// that proves exact coverage and records intentional exclusions/deferrals.
/// </summary>
public sealed class AccountingLedger
{
    private readonly List<AccountingEntry> _entries = [];

    public IReadOnlyList<AccountingEntry> Entries => _entries;

    public void RecordProjected(SymbolModel symbol, string generatedFile,
        IReadOnlyList<MemberOutcome>? memberOutcomes = null,
        IReadOnlyList<DeclarationOutcome>? declarationOutcomes = null,
        IReadOnlyList<OverloadOutcome>? overloadOutcomes = null)
    {
        Add(
            symbol,
            AccountingOutcome.Projected,
            "emitted",
            generatedFile: generatedFile,
            memberOutcomes: memberOutcomes,
            declarationOutcomes: declarationOutcomes,
            overloadOutcomes: overloadOutcomes);
    }

    public void RecordExcluded(
        SymbolModel symbol,
        string reason,
        IReadOnlyList<MemberOutcome>? memberOutcomes = null,
        IReadOnlyList<DeclarationOutcome>? declarationOutcomes = null,
        IReadOnlyList<OverloadOutcome>? overloadOutcomes = null)
        => Add(
            symbol,
            AccountingOutcome.Excluded,
            reason,
            memberOutcomes: memberOutcomes,
            declarationOutcomes: declarationOutcomes,
            overloadOutcomes: overloadOutcomes);

    public void RecordDeferred(
        SymbolModel symbol,
        string phase,
        string reason,
        IReadOnlyList<MemberOutcome>? memberOutcomes = null,
        IReadOnlyList<DeclarationOutcome>? declarationOutcomes = null,
        IReadOnlyList<OverloadOutcome>? overloadOutcomes = null)
        => Add(
            symbol,
            AccountingOutcome.Deferred,
            reason,
            deferredPhase: phase,
            memberOutcomes: memberOutcomes,
            declarationOutcomes: declarationOutcomes,
            overloadOutcomes: overloadOutcomes);

    public void RecordFailed(
        SymbolModel symbol,
        string reason,
        IReadOnlyList<MemberOutcome>? partialOutcomes = null,
        IReadOnlyList<DeclarationOutcome>? partialDeclarationOutcomes = null,
        IReadOnlyList<OverloadOutcome>? partialOverloadOutcomes = null)
        => Add(
            symbol,
            AccountingOutcome.GenerationFailed,
            reason,
            memberOutcomes: partialOutcomes,
            declarationOutcomes: partialDeclarationOutcomes,
            overloadOutcomes: partialOverloadOutcomes);

    private void Add(
        SymbolModel symbol,
        AccountingOutcome outcome,
        string reason,
        string? generatedFile = null,
        string? deferredPhase = null,
        IReadOnlyList<MemberOutcome>? memberOutcomes = null,
        IReadOnlyList<DeclarationOutcome>? declarationOutcomes = null,
        IReadOnlyList<OverloadOutcome>? overloadOutcomes = null)
    {
        var kind = symbol.Declarations.Count > 0
            ? symbol.Declarations[0].Kind
            : "unknown";
        var preparedMembers = PrepareMemberOutcomes(
            symbol,
            outcome,
            reason,
            deferredPhase,
            memberOutcomes);
        var preparedDeclarations = PrepareDeclarationOutcomes(
            symbol,
            outcome,
            reason,
            deferredPhase,
            preparedMembers,
            declarationOutcomes);
        var preparedOverloads = PrepareOverloadOutcomes(
            symbol,
            outcome,
            reason,
            deferredPhase,
            preparedMembers,
            overloadOutcomes);
        var sourceMembers = SourceAccountingShape.GetMembers(symbol);
        var sourceOverloads = SourceAccountingShape.GetOverloads(
            symbol,
            sourceMembers);
        var recordedOutcome = outcome == AccountingOutcome.Projected
            && (preparedMembers.Any(member =>
                    member.Status == MemberOutcomeStatus.Deferred)
                || preparedDeclarations.Any(declaration =>
                    declaration.Status == MemberOutcomeStatus.Deferred)
                || preparedOverloads.Any(overload =>
                    overload.Status == MemberOutcomeStatus.Deferred))
                ? AccountingOutcome.ProjectedWithDeferredMembers
                : outcome;

        _entries.Add(new AccountingEntry(
            symbol.Ordinal,
            symbol.Name,
            kind,
            symbol.Semantic.Status,
            symbol.Semantic.Classifications,
            recordedOutcome,
            reason,
            generatedFile,
            deferredPhase,
            preparedMembers,
            preparedDeclarations,
            preparedOverloads,
            symbol.Declarations.Count,
            sourceMembers.Count,
            sourceOverloads.Count,
            sourceOverloads.Sum(overload => overload.Parameters.Count)));
    }

    private static IReadOnlyList<MemberOutcome> PrepareMemberOutcomes(
        SymbolModel symbol,
        AccountingOutcome outcome,
        string reason,
        string? deferredPhase,
        IReadOnlyList<MemberOutcome>? suppliedOutcomes)
    {
        var sourceMembers = SourceAccountingShape.GetMembers(symbol);

        var duplicateSource = sourceMembers
            .GroupBy(source => source.QualifiedKey, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateSource is not null)
        {
            throw new InvalidOperationException(
                $"Source symbol '{symbol.Name}' contains duplicate member identity " +
                $"'{duplicateSource.Key}'.");
        }

        var sourceIndex = sourceMembers.ToDictionary(
            source => source.QualifiedKey,
            StringComparer.Ordinal);
        var normalized = new Dictionary<string, MemberOutcome>(StringComparer.Ordinal);
        foreach (var supplied in suppliedOutcomes ?? [])
        {
            SourceMemberShape? source = null;
            if (supplied.QualifiedKey is not null)
                sourceIndex.TryGetValue(supplied.QualifiedKey, out source);
            else
            {
                var candidates = sourceMembers
                    .Where(item =>
                        item.Declaration.Ordinal == supplied.DeclarationOrdinal
                        && item.Member.Ordinal == supplied.Ordinal)
                    .ToList();
                if (candidates.Count == 1)
                    source = candidates[0];
            }

            if (source is null)
            {
                throw new InvalidOperationException(
                    $"Member outcome for '{supplied.QualifiedKey ?? $"{symbol.Name}/decl[{supplied.DeclarationOrdinal}]/member[{supplied.Ordinal}]"}' " +
                    "does not identify a source member.");
            }

            if (!normalized.TryAdd(
                source.QualifiedKey,
                supplied with
                {
                    Name = source.Member.Name?.Text
                        ?? (string.IsNullOrEmpty(supplied.Name)
                            ? source.Member.Kind
                            : supplied.Name),
                    Kind = source.Member.Kind,
                    Provenance = source.Provenance,
                    SourceLocation = source.SourceLocation,
                    QualifiedKey = source.QualifiedKey,
                }))
            {
                throw new InvalidOperationException(
                    $"Duplicate member outcome for '{source.QualifiedKey}'.");
            }
        }

        var defaultStatus = outcome switch
        {
            AccountingOutcome.Projected or AccountingOutcome.ProjectedWithDeferredMembers
                => MemberOutcomeStatus.Projected,
            AccountingOutcome.Deferred or AccountingOutcome.Excluded
                => MemberOutcomeStatus.Deferred,
            AccountingOutcome.GenerationFailed
                => MemberOutcomeStatus.NotAttemptedAfterFailure,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };

        foreach (var source in sourceMembers)
        {
            if (normalized.ContainsKey(source.QualifiedKey))
                continue;

            if (source.NestedTypeLiteral)
            {
                normalized[source.QualifiedKey] = new MemberOutcome(
                    source.Member.Ordinal,
                    source.Member.Name?.Text ?? source.Member.Kind,
                    source.Member.Kind,
                    MemberOutcomeStatus.Deferred,
                    SourceAccountingShape.FactoryConstructorPhase,
                    SourceAccountingShape.FactoryReason(source.Declaration),
                    source.Declaration.Ordinal,
                    source.Provenance,
                    source.SourceLocation,
                    source.QualifiedKey);
                continue;
            }

            var memberReason = defaultStatus == MemberOutcomeStatus.NotAttemptedAfterFailure
                ? $"Not attempted after symbol generation failed: {reason}"
                : reason;
            normalized[source.QualifiedKey] = new MemberOutcome(
                source.Member.Ordinal,
                source.Member.Name?.Text ?? source.Member.Kind,
                source.Member.Kind,
                defaultStatus,
                defaultStatus == MemberOutcomeStatus.Deferred
                    ? deferredPhase ?? "excluded"
                    : null,
                memberReason,
                source.Declaration.Ordinal,
                source.Provenance,
                source.SourceLocation,
                source.QualifiedKey);
        }

        return sourceMembers
            .Select(source => normalized[source.QualifiedKey])
            .ToList();
    }

    private static IReadOnlyList<DeclarationOutcome> PrepareDeclarationOutcomes(
        SymbolModel symbol,
        AccountingOutcome outcome,
        string reason,
        string? deferredPhase,
        IReadOnlyList<MemberOutcome> memberOutcomes,
        IReadOnlyList<DeclarationOutcome>? suppliedOutcomes)
    {
        var memberIndex = memberOutcomes
            .GroupBy(member => member.DeclarationOrdinal)
            .ToDictionary(group => group.Key, group => group.ToList());
        var sourceIndex = symbol.Declarations.ToDictionary(
            declaration => declaration.Ordinal);
        var suppliedIndex = new Dictionary<int, DeclarationOutcome>();
        foreach (var supplied in suppliedOutcomes ?? [])
        {
            if (!sourceIndex.TryGetValue(supplied.Ordinal, out var declaration))
            {
                throw new InvalidOperationException(
                    $"Declaration outcome for '{symbol.Name}/decl[{supplied.Ordinal}]' " +
                    "does not identify a source declaration.");
            }
            if (!suppliedIndex.TryAdd(
                    supplied.Ordinal,
                    supplied with
                    {
                        Kind = declaration.Kind,
                        Provenance =
                            $"{symbol.Name}/decl[{declaration.Ordinal}]/{declaration.Kind}",
                        SourceLocation =
                            SourceAccountingShape.FormatLocation(declaration.Location),
                    }))
            {
                throw new InvalidOperationException(
                    $"Duplicate declaration outcome for '{symbol.Name}/decl[{supplied.Ordinal}]'.");
            }
        }

        var defaultStatus = outcome switch
        {
            AccountingOutcome.Projected or AccountingOutcome.ProjectedWithDeferredMembers
                => MemberOutcomeStatus.Projected,
            AccountingOutcome.Deferred or AccountingOutcome.Excluded
                => MemberOutcomeStatus.Deferred,
            AccountingOutcome.GenerationFailed
                => MemberOutcomeStatus.NotAttemptedAfterFailure,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };

        return symbol.Declarations
            .OrderBy(declaration => declaration.Ordinal)
            .Select(declaration =>
            {
                if (suppliedIndex.TryGetValue(declaration.Ordinal, out var supplied))
                    return supplied;

                if (declaration.Kind == "globalFunction")
                {
                    return new DeclarationOutcome(
                        declaration.Ordinal,
                        declaration.Kind,
                        MemberOutcomeStatus.Deferred,
                        SourceAccountingShape.GlobalFunctionPhase,
                        SourceAccountingShape.GlobalFunctionReason,
                        $"{symbol.Name}/decl[{declaration.Ordinal}]/{declaration.Kind}",
                        SourceAccountingShape.FormatLocation(declaration.Location));
                }

                if (SourceAccountingShape.IsFactoryDeclaration(declaration))
                {
                    return new DeclarationOutcome(
                        declaration.Ordinal,
                        declaration.Kind,
                        MemberOutcomeStatus.Deferred,
                        SourceAccountingShape.FactoryConstructorPhase,
                        SourceAccountingShape.FactoryReason(declaration),
                        $"{symbol.Name}/decl[{declaration.Ordinal}]/{declaration.Kind}",
                        SourceAccountingShape.FormatLocation(declaration.Location));
                }

                memberIndex.TryGetValue(declaration.Ordinal, out var members);
                members ??= [];
                var status = members.Any(member => member.Status == MemberOutcomeStatus.Failed)
                    ? MemberOutcomeStatus.Failed
                    : members.Any(member =>
                        member.Status == MemberOutcomeStatus.NotAttemptedAfterFailure)
                        ? MemberOutcomeStatus.NotAttemptedAfterFailure
                        : members.Any(member => member.Status == MemberOutcomeStatus.Deferred)
                            ? MemberOutcomeStatus.Deferred
                            : defaultStatus;
                return new DeclarationOutcome(
                    declaration.Ordinal,
                    declaration.Kind,
                    status,
                    status == MemberOutcomeStatus.Deferred
                        ? members.FirstOrDefault(member =>
                            member.Status == MemberOutcomeStatus.Deferred)?.Phase
                          ?? deferredPhase
                          ?? "excluded"
                        : null,
                    reason,
                    $"{symbol.Name}/decl[{declaration.Ordinal}]/{declaration.Kind}",
                    SourceAccountingShape.FormatLocation(declaration.Location));
            })
            .ToList();
    }

    private static IReadOnlyList<OverloadOutcome> PrepareOverloadOutcomes(
        SymbolModel symbol,
        AccountingOutcome outcome,
        string reason,
        string? deferredPhase,
        IReadOnlyList<MemberOutcome> memberOutcomes,
        IReadOnlyList<OverloadOutcome>? suppliedOutcomes)
    {
        var sourceOverloads = SourceAccountingShape.GetOverloads(symbol);
        var sourceIndex = sourceOverloads.ToDictionary(
            overload => overload.QualifiedKey,
            StringComparer.Ordinal);
        var normalized = new Dictionary<string, OverloadOutcome>(StringComparer.Ordinal);
        foreach (var supplied in suppliedOutcomes ?? [])
        {
            if (!sourceIndex.TryGetValue(supplied.QualifiedKey, out var source))
            {
                throw new InvalidOperationException(
                    $"Overload outcome '{supplied.QualifiedKey}' does not identify a source overload.");
            }
            if (!normalized.TryAdd(
                    source.QualifiedKey,
                    supplied with
                    {
                        DeclarationOrdinal = source.Declaration.Ordinal,
                        MemberOrdinal = source.MemberOrdinal,
                        Name = source.Name,
                        Kind = source.Kind,
                        Provenance = source.Provenance,
                        SourceLocation = source.SourceLocation,
                        ParameterOutcomes =
                            SourceAccountingShape.NormalizeParameterOutcomes(
                                source,
                                supplied.ParameterOutcomes),
                    }))
            {
                throw new InvalidOperationException(
                    $"Duplicate overload outcome for '{source.QualifiedKey}'.");
            }
        }

        var memberIndex = memberOutcomes
            .Where(member => member.QualifiedKey is not null)
            .ToDictionary(member => member.QualifiedKey!, StringComparer.Ordinal);
        var defaultStatus = outcome switch
        {
            AccountingOutcome.Projected or AccountingOutcome.ProjectedWithDeferredMembers
                => MemberOutcomeStatus.Projected,
            AccountingOutcome.Deferred or AccountingOutcome.Excluded
                => MemberOutcomeStatus.Deferred,
            AccountingOutcome.GenerationFailed
                => MemberOutcomeStatus.NotAttemptedAfterFailure,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };

        foreach (var source in sourceOverloads)
        {
            if (normalized.ContainsKey(source.QualifiedKey))
                continue;

            if (source.Declaration.Kind == "globalFunction")
            {
                normalized[source.QualifiedKey] = new OverloadOutcome(
                    source.QualifiedKey,
                    source.Declaration.Ordinal,
                    source.MemberOrdinal,
                    source.Name,
                    source.Kind,
                    MemberOutcomeStatus.Deferred,
                    SourceAccountingShape.GlobalFunctionPhase,
                    SourceAccountingShape.GlobalFunctionReason,
                    source.Provenance,
                    source.SourceLocation,
                    source.Parameters
                        .OrderBy(parameter => parameter.Ordinal)
                        .Select(parameter => new ParameterOutcome(
                            parameter.Ordinal,
                            parameter.Name,
                            MemberOutcomeStatus.Deferred,
                            SourceAccountingShape.GlobalFunctionPhase,
                            SourceAccountingShape.GlobalFunctionReason,
                            $"{source.Provenance}/parameter[{parameter.Ordinal}]/{parameter.Name}",
                            SourceAccountingShape.FormatLocation(parameter.Location)))
                        .ToList());
                continue;
            }

            var status = defaultStatus;
            var phase = status == MemberOutcomeStatus.Deferred
                ? deferredPhase ?? "excluded"
                : null;
            var overloadReason = reason;
            if (source.SourceMember is not null
                && memberIndex.TryGetValue(
                    source.SourceMember.QualifiedKey,
                    out var member))
            {
                status = member.Status;
                phase = member.Phase;
                overloadReason = member.Reason ?? reason;
            }

            normalized[source.QualifiedKey] = new OverloadOutcome(
                source.QualifiedKey,
                source.Declaration.Ordinal,
                source.MemberOrdinal,
                source.Name,
                source.Kind,
                status,
                phase,
                overloadReason,
                source.Provenance,
                source.SourceLocation,
                source.Parameters
                    .OrderBy(parameter => parameter.Ordinal)
                    .Select(parameter => new ParameterOutcome(
                        parameter.Ordinal,
                        parameter.Name,
                        status,
                        phase,
                        overloadReason,
                        $"{source.Provenance}/parameter[{parameter.Ordinal}]/{parameter.Name}",
                        SourceAccountingShape.FormatLocation(parameter.Location)))
                    .ToList());
        }

        return sourceOverloads
            .Select(source => normalized[source.QualifiedKey])
            .ToList();
    }

    /// <summary>
    /// Validates that every symbol has exactly one entry and no symbol is silently skipped.
    /// </summary>
    public AccountingValidationResult Validate(int expectedCount)
    {
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

        var expectedDeclarationCount = _entries.Sum(e => e.ExpectedDeclarationCount);
        var actualDeclarationCount = _entries.Sum(e => e.DeclarationOutcomes?.Count ?? 0);
        var duplicateDeclarations = _entries
            .SelectMany(e => (e.DeclarationOutcomes ?? [])
                .GroupBy(declaration => declaration.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => $"{e.SymbolName}/decl[{group.Key}]"))
            .OrderBy(identity => identity, StringComparer.Ordinal)
            .ToList();
        var declarationReconciliationValid =
            actualDeclarationCount == expectedDeclarationCount
            && duplicateDeclarations.Count == 0;

        var expectedMemberCount = _entries.Sum(e => e.ExpectedMemberCount);
        var actualMemberCount = _entries.Sum(e => e.MemberOutcomes?.Count ?? 0);
        var duplicateMembers = _entries
            .SelectMany(e => (e.MemberOutcomes ?? [])
                .GroupBy(
                    m => m.QualifiedKey
                        ?? $"{e.SymbolName}/decl[{m.DeclarationOrdinal}]/member[{m.Ordinal}]",
                    StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        var memberReconciliationValid =
            actualMemberCount == expectedMemberCount
            && duplicateMembers.Count == 0;

        var expectedOverloadCount = _entries.Sum(e => e.ExpectedOverloadCount);
        var actualOverloadCount = _entries.Sum(
            e => e.OverloadOutcomes?.Count ?? 0);
        var duplicateOverloads = _entries
            .SelectMany(e => (e.OverloadOutcomes ?? [])
                .GroupBy(overload => overload.QualifiedKey, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key))
            .OrderBy(identity => identity, StringComparer.Ordinal)
            .ToList();
        var overloadReconciliationValid =
            actualOverloadCount == expectedOverloadCount
            && duplicateOverloads.Count == 0;
        var expectedParameterCount = _entries.Sum(e => e.ExpectedParameterCount);
        var actualParameterCount = _entries.Sum(e =>
            e.OverloadOutcomes?.Sum(overload => overload.ParameterOutcomes.Count) ?? 0);
        var duplicateParameters = _entries
            .SelectMany(e => (e.OverloadOutcomes ?? [])
                .SelectMany(overload => overload.ParameterOutcomes
                    .GroupBy(parameter => parameter.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group =>
                        $"{overload.QualifiedKey}/parameter[{group.Key}]")))
            .OrderBy(identity => identity, StringComparer.Ordinal)
            .ToList();
        var parameterReconciliationValid =
            actualParameterCount == expectedParameterCount
            && duplicateParameters.Count == 0;
        var diagnostics = new List<string>();
        if (!declarationReconciliationValid)
        {
            diagnostics.Add(
                $"Declaration reconciliation failed: {actualDeclarationCount} / " +
                $"{expectedDeclarationCount}; duplicates: " +
                $"{Summarize(duplicateDeclarations)}.");
        }
        if (!memberReconciliationValid)
        {
            diagnostics.Add(
                $"Member reconciliation failed: {actualMemberCount} / " +
                $"{expectedMemberCount}; duplicates: {Summarize(duplicateMembers)}.");
        }
        if (!overloadReconciliationValid)
        {
            diagnostics.Add(
                $"Overload reconciliation failed: {actualOverloadCount} / " +
                $"{expectedOverloadCount}; duplicates: {Summarize(duplicateOverloads)}.");
        }
        if (!parameterReconciliationValid)
        {
            diagnostics.Add(
                $"Parameter reconciliation failed: {actualParameterCount} / " +
                $"{expectedParameterCount}; duplicates: {Summarize(duplicateParameters)}.");
        }

        var isValid = _entries.Count == expectedCount
            && duplicates.Count == 0
            && declarationReconciliationValid
            && memberReconciliationValid
            && overloadReconciliationValid
            && parameterReconciliationValid;
        return new AccountingValidationResult(
            IsValid: isValid,
            ActualCount: _entries.Count,
            ExpectedCount: expectedCount,
            Duplicates: duplicates,
            OutcomeTotals: totals,
            MemberReconciliationValid: memberReconciliationValid,
            ActualMemberCount: actualMemberCount,
            ExpectedMemberCount: expectedMemberCount,
            DuplicateMembers: duplicateMembers,
            DeclarationReconciliationValid: declarationReconciliationValid,
            ActualDeclarationCount: actualDeclarationCount,
            ExpectedDeclarationCount: expectedDeclarationCount,
            DuplicateDeclarations: duplicateDeclarations,
            OverloadReconciliationValid: overloadReconciliationValid,
            ActualOverloadCount: actualOverloadCount,
            ExpectedOverloadCount: expectedOverloadCount,
            ParameterReconciliationValid: parameterReconciliationValid,
            ActualParameterCount: actualParameterCount,
            ExpectedParameterCount: expectedParameterCount,
            DuplicateParameters: duplicateParameters,
            Diagnostics: diagnostics);
    }

    public EmitterManifest BuildManifest(
        string generatorVersion,
        ManifestModel sourceManifest)
    {
        var projected = _entries.Where(e =>
            e.Outcome is AccountingOutcome.Projected or AccountingOutcome.ProjectedWithDeferredMembers).ToList();
        var projectedClean = _entries.Where(e => e.Outcome == AccountingOutcome.Projected).ToList();
        var projectedWithDeferred = _entries.Where(e => e.Outcome == AccountingOutcome.ProjectedWithDeferredMembers).ToList();
        var excluded = _entries.Where(e => e.Outcome == AccountingOutcome.Excluded).ToList();
        var deferred = _entries.Where(e => e.Outcome == AccountingOutcome.Deferred).ToList();
        var failed = _entries.Where(e => e.Outcome == AccountingOutcome.GenerationFailed).ToList();
        var allDeclarationOutcomes = _entries
            .SelectMany(e => (e.DeclarationOutcomes ?? [])
                .Select(declaration => (Entry: e, Declaration: declaration)))
            .ToList();
        var allMemberOutcomes = _entries
            .SelectMany(e => (e.MemberOutcomes ?? []).Select(m => (Entry: e, Member: m)))
            .ToList();
        var allOverloadOutcomes = _entries
            .SelectMany(e => (e.OverloadOutcomes ?? [])
                .Select(overload => (Entry: e, Overload: overload)))
            .ToList();

        // Build per-symbol deferred-member details
        var deferredMemberEntries = projectedWithDeferred
            .SelectMany(e => (e.MemberOutcomes ?? [])
                .Where(m => m.Status == MemberOutcomeStatus.Deferred)
                .Select(m => new DeferredMemberEntry(
                    e.SymbolName,
                    m.Name,
                    m.Kind,
                    m.Phase ?? "unknown",
                    m.Reason ?? "",
                    m.DeclarationOrdinal,
                    m.Ordinal,
                    MemberProvenance(e.SymbolName, m),
                    m.SourceLocation ?? "unknown")))
            .ToList();

        var failedMemberEntries = failed
            .SelectMany(e => (e.MemberOutcomes ?? [])
                .Where(m => m.Status == MemberOutcomeStatus.Failed)
                .Select(m => new FailedMemberEntry(
                    e.SymbolName,
                    m.Name,
                    m.Kind,
                    m.Reason ?? "",
                    m.DeclarationOrdinal,
                    m.Ordinal,
                    MemberProvenance(e.SymbolName, m),
                    m.SourceLocation ?? "unknown")))
            .ToList();

        var failedSymbolMemberEntries = failed
            .SelectMany(e => (e.MemberOutcomes ?? [])
                .OrderBy(m => m.DeclarationOrdinal)
                .ThenBy(m => m.Ordinal)
                .Select(m => new FailedSymbolMemberOutcomeEntry(
                    e.SymbolName,
                    m.Name,
                    m.Kind,
                    m.Status.ToString(),
                    m.Phase,
                    m.Reason ?? "",
                    m.DeclarationOrdinal,
                    m.Ordinal,
                    MemberProvenance(e.SymbolName, m),
                    m.SourceLocation ?? "unknown")))
            .ToList();

        var sourceDeclarationEntries = allDeclarationOutcomes
            .Select(item => new SourceDeclarationEntry(
                item.Entry.SymbolName,
                item.Declaration.Ordinal,
                item.Declaration.Kind,
                item.Declaration.Status.ToString(),
                item.Declaration.Phase,
                item.Declaration.Reason,
                item.Declaration.Provenance,
                item.Declaration.SourceLocation))
            .ToList();
        var sourceMemberEntries = allMemberOutcomes
            .Select(item => new SourceMemberEntry(
                item.Entry.SymbolName,
                item.Member.DeclarationOrdinal,
                item.Member.Ordinal,
                item.Member.Name,
                item.Member.Kind,
                item.Member.Status.ToString(),
                item.Member.Phase,
                item.Member.Reason ?? "",
                MemberProvenance(item.Entry.SymbolName, item.Member),
                item.Member.SourceLocation ?? "unknown",
                item.Member.QualifiedKey
                    ?? $"{item.Entry.SymbolName}/decl[{item.Member.DeclarationOrdinal}]/member[{item.Member.Ordinal}]"))
            .ToList();
        var sourceOverloadEntries = allOverloadOutcomes
            .Select(item => new SourceOverloadEntry(
                item.Entry.SymbolName,
                item.Overload.DeclarationOrdinal,
                item.Overload.MemberOrdinal,
                item.Overload.Name,
                item.Overload.Kind,
                item.Overload.Status.ToString(),
                item.Overload.Phase,
                item.Overload.Reason,
                item.Overload.QualifiedKey,
                item.Overload.Provenance,
                item.Overload.SourceLocation,
                item.Overload.ParameterOutcomes))
            .ToList();
        var validation = Validate(_entries.Count);
        var diagnostics = validation.Diagnostics
            .Select(message => new ManifestDiagnostic(
                "ACCOUNTING_RECONCILIATION",
                "error",
                message))
            .Concat(failed.Select(entry => new ManifestDiagnostic(
                "GENERATION_FAILED",
                "error",
                $"{entry.SymbolName}: {entry.Reason}")))
            .ToList();

        return new EmitterManifest(
            SchemaVersion: 1,
            GeneratorVersion: generatorVersion,
            SourceManifest: new ManifestReference(
                sourceManifest.Files.TypescriptSymbols.Sha256,
                sourceManifest.Files.WebIdlSymbols.Sha256),
            Accounting: new AccountingSummary(
                TotalSymbols: _entries.Count,
                Projected: projected.Count,
                ProjectedClean: projectedClean.Count,
                ProjectedWithDeferredMembers: projectedWithDeferred.Count,
                Excluded: excluded.Count,
                Deferred: deferred.Count,
                GenerationFailed: failed.Count,
                ProjectedSymbols: projected.Select(e => e.SymbolName).ToList(),
                ExcludedSymbols: excluded.Select(e =>
                    new ExcludedEntry(e.SymbolName, e.Reason)).ToList(),
                DeferredSymbols: deferred.Select(e =>
                    new DeferredEntry(e.SymbolName, e.DeferredPhase ?? "unknown", e.Reason)).ToList(),
                FailedSymbols: failed.Select(e =>
                    new FailedEntry(e.SymbolName, e.Reason)).ToList(),
                DeferredMemberEntries: deferredMemberEntries,
                FailedMemberEntries: failedMemberEntries,
                TotalMembers: allMemberOutcomes.Count,
                ProjectedMembers: allMemberOutcomes.Count(x =>
                    x.Member.Status == MemberOutcomeStatus.Projected),
                DeferredMembers: allMemberOutcomes.Count(x =>
                    x.Member.Status == MemberOutcomeStatus.Deferred),
                FailedMembers: allMemberOutcomes.Count(x =>
                    x.Member.Status is
                        MemberOutcomeStatus.Failed or
                        MemberOutcomeStatus.NotAttemptedAfterFailure),
                ExpectedMembers: validation.ExpectedMemberCount,
                MemberReconciliationValid: validation.MemberReconciliationValid,
                FailedSymbolMemberOutcomes: failedSymbolMemberEntries,
                NotAttemptedAfterFailureMembers: allMemberOutcomes.Count(x =>
                    x.Member.Status == MemberOutcomeStatus.NotAttemptedAfterFailure),
                SourceDeclarations: validation.ExpectedDeclarationCount,
                AccountedSourceDeclarations: validation.ActualDeclarationCount,
                SourceMembers: validation.ExpectedMemberCount,
                AccountedSourceMembers: validation.ActualMemberCount,
                SourceOverloads: validation.ExpectedOverloadCount,
                AccountedSourceOverloads: validation.ActualOverloadCount,
                SourceParameters: validation.ExpectedParameterCount,
                AccountedSourceParameters: validation.ActualParameterCount,
                DeclarationReconciliationValid: validation.DeclarationReconciliationValid,
                OverloadReconciliationValid: validation.OverloadReconciliationValid,
                ParameterReconciliationValid: validation.ParameterReconciliationValid,
                SourceDeclarationEntries: sourceDeclarationEntries,
                SourceMemberEntries: sourceMemberEntries,
                SourceOverloadEntries: sourceOverloadEntries),
            Diagnostics: diagnostics);
    }

    private static string MemberProvenance(string symbolName, MemberOutcome member)
        => member.Provenance
            ?? $"{symbolName}/decl[{member.DeclarationOrdinal}]/member[{member.Ordinal}]/{member.Kind}/{member.Name}";

    private static string Summarize(IReadOnlyList<string> identities)
        => identities.Count == 0
            ? "none"
            : string.Join(", ", identities.Take(5))
              + (identities.Count > 5
                  ? $", ... and {identities.Count - 5} more"
                  : "");

}

public sealed record AccountingValidationResult(
    bool IsValid,
    int ActualCount,
    int ExpectedCount,
    IReadOnlyList<string> Duplicates,
    IReadOnlyDictionary<AccountingOutcome, int> OutcomeTotals,
    bool MemberReconciliationValid,
    int ActualMemberCount,
    int ExpectedMemberCount,
    IReadOnlyList<string> DuplicateMembers,
    bool DeclarationReconciliationValid,
    int ActualDeclarationCount,
    int ExpectedDeclarationCount,
    IReadOnlyList<string> DuplicateDeclarations,
    bool OverloadReconciliationValid,
    int ActualOverloadCount,
    int ExpectedOverloadCount,
    bool ParameterReconciliationValid,
    int ActualParameterCount,
    int ExpectedParameterCount,
    IReadOnlyList<string> DuplicateParameters,
    IReadOnlyList<string> Diagnostics);

public sealed record EmitterManifest(
    int SchemaVersion,
    string GeneratorVersion,
    ManifestReference SourceManifest,
    AccountingSummary Accounting,
    IReadOnlyList<ManifestDiagnostic> Diagnostics,
    IReadOnlyList<SynthesizedTypeManifestEntry>? SynthesizedTypes = null);

public sealed record SynthesizedTypeManifestEntry(
    string Name,
    string Kind,
    string Provenance,
    string Fingerprint,
    string RelativePath);

public sealed record ManifestDiagnostic(
    string Code,
    string Severity,
    string Message);

/// <summary>
/// Stable identity reference for the input IR. Contains only content hashes —
/// no checkout paths, no absolute directories.
/// </summary>
public sealed record ManifestReference(
    string TypescriptSymbolsSha256,
    string WebIdlSymbolsSha256);

public sealed record AccountingSummary(
    int TotalSymbols,
    int Projected,
    int ProjectedClean,
    int ProjectedWithDeferredMembers,
    int Excluded,
    int Deferred,
    int GenerationFailed,
    IReadOnlyList<string> ProjectedSymbols,
    IReadOnlyList<ExcludedEntry> ExcludedSymbols,
    IReadOnlyList<DeferredEntry> DeferredSymbols,
    IReadOnlyList<FailedEntry> FailedSymbols,
    IReadOnlyList<DeferredMemberEntry> DeferredMemberEntries,
    IReadOnlyList<FailedMemberEntry> FailedMemberEntries,
    int TotalMembers = 0,
    int ProjectedMembers = 0,
    int DeferredMembers = 0,
    int FailedMembers = 0,
    int ExpectedMembers = 0,
    bool MemberReconciliationValid = true,
    IReadOnlyList<FailedSymbolMemberOutcomeEntry>? FailedSymbolMemberOutcomes = null,
    int NotAttemptedAfterFailureMembers = 0,
    int SourceDeclarations = 0,
    int AccountedSourceDeclarations = 0,
    int SourceMembers = 0,
    int AccountedSourceMembers = 0,
    int SourceOverloads = 0,
    int AccountedSourceOverloads = 0,
    int SourceParameters = 0,
    int AccountedSourceParameters = 0,
    bool DeclarationReconciliationValid = true,
    bool OverloadReconciliationValid = true,
    bool ParameterReconciliationValid = true,
    IReadOnlyList<SourceDeclarationEntry>? SourceDeclarationEntries = null,
    IReadOnlyList<SourceMemberEntry>? SourceMemberEntries = null,
    IReadOnlyList<SourceOverloadEntry>? SourceOverloadEntries = null);

public sealed record ExcludedEntry(string Symbol, string Reason);
public sealed record DeferredEntry(string Symbol, string Phase, string Reason);
public sealed record FailedEntry(string Symbol, string Reason);

/// <summary>
/// Per-member deferral entry for symbols projected with some members deferred to a later phase.
/// </summary>
public sealed record DeferredMemberEntry(
    string SymbolName,
    string MemberName,
    string MemberKind,
    string Phase,
    string Reason,
    int DeclarationOrdinal = 0,
    int MemberOrdinal = 0,
    string Provenance = "",
    string SourceLocation = "");

public sealed record FailedMemberEntry(
    string SymbolName,
    string MemberName,
    string MemberKind,
    string Reason,
    int DeclarationOrdinal = 0,
    int MemberOrdinal = 0,
    string Provenance = "",
    string SourceLocation = "");

public sealed record FailedSymbolMemberOutcomeEntry(
    string SymbolName,
    string MemberName,
    string MemberKind,
    string Status,
    string? Phase,
    string Reason,
    int DeclarationOrdinal,
    int MemberOrdinal,
    string Provenance,
    string SourceLocation);

public sealed record SourceDeclarationEntry(
    string SymbolName,
    int DeclarationOrdinal,
    string Kind,
    string Status,
    string? Phase,
    string Reason,
    string Provenance,
    string SourceLocation);

public sealed record SourceOverloadEntry(
    string SymbolName,
    int DeclarationOrdinal,
    int? MemberOrdinal,
    string Name,
    string Kind,
    string Status,
    string? Phase,
    string Reason,
    string QualifiedKey,
    string Provenance,
    string SourceLocation,
    IReadOnlyList<ParameterOutcome> ParameterOutcomes);

public sealed record SourceMemberEntry(
    string SymbolName,
    int DeclarationOrdinal,
    int MemberOrdinal,
    string MemberName,
    string MemberKind,
    string Status,
    string? Phase,
    string Reason,
    string Provenance,
    string SourceLocation,
    string QualifiedKey = "");
