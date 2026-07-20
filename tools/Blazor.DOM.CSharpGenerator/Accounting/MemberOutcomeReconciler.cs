using Blazor.DOM.CSharpGenerator.IR;

namespace Blazor.DOM.CSharpGenerator.Accounting;

public static class MemberOutcomeReconciler
{
    public static IReadOnlyList<MemberOutcome> CompleteSuccess(
        SymbolModel symbol,
        IReadOnlyList<MemberOutcome> outcomes)
    {
        var sourceMembers = GetSourceMembers(symbol);
        var normalized = NormalizeKnownOutcomes(symbol, sourceMembers, outcomes);
        var missing = sourceMembers
            .Where(source => !normalized.ContainsKey(source.QualifiedKey))
            .ToList();

        foreach (var factoryMember in missing.Where(source => source.NestedTypeLiteral))
        {
            normalized[factoryMember.QualifiedKey] = CreateOutcome(
                factoryMember,
                MemberOutcomeStatus.Deferred,
                SourceAccountingShape.FactoryConstructorPhase,
                SourceAccountingShape.FactoryReason(factoryMember.Declaration));
        }

        var unaccounted = missing
            .Where(source => !source.NestedTypeLiteral)
            .ToList();
        if (unaccounted.Count > 0)
        {
            var first = unaccounted[0];
            normalized[first.QualifiedKey] = CreateOutcome(
                first,
                MemberOutcomeStatus.Failed,
                phase: null,
                reason: $"Emitter produced no outcome for '{first.Provenance}'.");
            throw new MemberOutcomeReconciliationException(
                $"Emitter produced no outcome for '{first.Provenance}'.",
                first.Provenance,
                normalized.Values.ToList());
        }

        return sourceMembers
            .Select(source => normalized[source.QualifiedKey])
            .ToList();
    }

    public static IReadOnlyList<MemberOutcome> CompleteFailure(
        SymbolModel symbol,
        IReadOnlyList<MemberOutcome> partialOutcomes,
        string failureReason,
        string failureProvenance)
    {
        var sourceMembers = GetSourceMembers(symbol);
        var normalized = NormalizeKnownOutcomes(symbol, sourceMembers, partialOutcomes);

        foreach (var source in sourceMembers)
        {
            if (normalized.ContainsKey(source.QualifiedKey))
                continue;

            normalized[source.QualifiedKey] = source.NestedTypeLiteral
                ? CreateOutcome(
                    source,
                    MemberOutcomeStatus.Deferred,
                    SourceAccountingShape.FactoryConstructorPhase,
                    SourceAccountingShape.FactoryReason(source.Declaration))
                : CreateOutcome(
                    source,
                    MemberOutcomeStatus.NotAttemptedAfterFailure,
                    phase: null,
                    reason:
                        $"Not attempted because emission failed at '{failureProvenance}': {failureReason}");
        }

        return sourceMembers
            .Select(source => normalized[source.QualifiedKey])
            .ToList();
    }

    private static Dictionary<string, MemberOutcome> NormalizeKnownOutcomes(
        SymbolModel symbol,
        IReadOnlyList<SourceMemberShape> sourceMembers,
        IReadOnlyList<MemberOutcome> outcomes)
    {
        var duplicateSource = sourceMembers
            .GroupBy(source => source.QualifiedKey, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateSource is not null)
        {
            throw new MemberOutcomeReconciliationException(
                $"Source symbol contains duplicate member identity " +
                $"'{duplicateSource.Key}'.",
                $"{symbol.Name}/member-accounting",
                []);
        }

        var sourceIndex = sourceMembers.ToDictionary(
            source => source.QualifiedKey,
            StringComparer.Ordinal);
        var normalized = new Dictionary<string, MemberOutcome>(StringComparer.Ordinal);

        foreach (var outcome in outcomes)
        {
            SourceMemberShape? source = null;
            if (outcome.QualifiedKey is not null)
                sourceIndex.TryGetValue(outcome.QualifiedKey, out source);
            else
            {
                var candidates = sourceMembers
                    .Where(item =>
                        item.Declaration.Ordinal == outcome.DeclarationOrdinal
                        && item.Member.Ordinal == outcome.Ordinal)
                    .ToList();
                if (candidates.Count == 1)
                    source = candidates[0];
            }

            if (source is null)
            {
                throw new MemberOutcomeReconciliationException(
                    $"Emitter produced an outcome for unknown member " +
                    $"'{outcome.QualifiedKey ?? $"{symbol.Name}/decl[{outcome.DeclarationOrdinal}]/member[{outcome.Ordinal}]"}'.",
                    $"{symbol.Name}/member-accounting",
                    normalized.Values.ToList());
            }

            if (normalized.ContainsKey(source.QualifiedKey))
            {
                throw new MemberOutcomeReconciliationException(
                    $"Emitter produced duplicate outcomes for '{source.Provenance}'.",
                    source.Provenance,
                    normalized.Values.ToList());
            }

            normalized.Add(source.QualifiedKey, outcome with
            {
                Name = source.Member.Name?.Text
                    ?? (string.IsNullOrEmpty(outcome.Name)
                        ? source.Member.Kind
                        : outcome.Name),
                Kind = source.Member.Kind,
                Provenance = source.Provenance,
                SourceLocation = source.SourceLocation,
                QualifiedKey = source.QualifiedKey,
            });
        }

        return normalized;
    }

    private static IReadOnlyList<SourceMemberShape> GetSourceMembers(SymbolModel symbol)
        => SourceAccountingShape.GetMembers(symbol);

    private static MemberOutcome CreateOutcome(
        SourceMemberShape source,
        MemberOutcomeStatus status,
        string? phase,
        string reason)
        => new(
            source.Member.Ordinal,
            source.Member.Name?.Text ?? source.Member.Kind,
            source.Member.Kind,
            status,
            phase,
            reason,
            source.Declaration.Ordinal,
            source.Provenance,
            source.SourceLocation,
            source.QualifiedKey);
}

public sealed class MemberOutcomeReconciliationException(
    string message,
    string provenance,
    IReadOnlyList<MemberOutcome> partialOutcomes)
    : Exception(message)
{
    public string Provenance { get; } = provenance;
    public IReadOnlyList<MemberOutcome> PartialOutcomes { get; } = partialOutcomes;
}
