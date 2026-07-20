using Blazor.DOM.CSharpGenerator.IR;

namespace Blazor.DOM.CSharpGenerator.Accounting;

public sealed record StructuredEmitterOutcomes(
    IReadOnlyList<DeclarationOutcome> DeclarationOutcomes,
    IReadOnlyList<MemberOutcome> MemberOutcomes,
    IReadOnlyList<OverloadOutcome> OverloadOutcomes);

public static class EmitterOutcomeReconciler
{
    public static StructuredEmitterOutcomes CompleteSuccess(
        SymbolModel symbol,
        IReadOnlyList<MemberOutcome> memberOutcomes,
        IReadOnlySet<string> emittedDeclarationKinds,
        IReadOnlyList<OverloadOutcome>? overloadOutcomes = null)
    {
        var members = MemberOutcomeReconciler.CompleteSuccess(
            symbol,
            memberOutcomes);
        var overloads = CompleteOverloads(
            symbol,
            members,
            overloadOutcomes ?? [],
            emittedDeclarationKinds,
            succeeded: true,
            failureReason: null,
            failureProvenance: null);
        return new StructuredEmitterOutcomes(
            BuildDeclarations(
                symbol,
                members,
                overloads,
                emittedDeclarationKinds,
                succeeded: true,
                failureReason: null),
            members,
            overloads);
    }

    public static StructuredEmitterOutcomes CompleteFailure(
        SymbolModel symbol,
        IReadOnlyList<MemberOutcome> partialMemberOutcomes,
        IReadOnlySet<string> emittedDeclarationKinds,
        string failureReason,
        string failureProvenance,
        IReadOnlyList<OverloadOutcome>? partialOverloadOutcomes = null)
    {
        var members = MemberOutcomeReconciler.CompleteFailure(
            symbol,
            partialMemberOutcomes,
            failureReason,
            failureProvenance);
        var overloads = CompleteOverloads(
            symbol,
            members,
            partialOverloadOutcomes ?? [],
            emittedDeclarationKinds,
            succeeded: false,
            failureReason,
            failureProvenance);
        return new StructuredEmitterOutcomes(
            BuildDeclarations(
                symbol,
                members,
                overloads,
                emittedDeclarationKinds,
                succeeded: false,
                failureReason),
            members,
            overloads);
    }

    private static IReadOnlyList<DeclarationOutcome> BuildDeclarations(
        SymbolModel symbol,
        IReadOnlyList<MemberOutcome> memberOutcomes,
        IReadOnlyList<OverloadOutcome> overloadOutcomes,
        IReadOnlySet<string> emittedDeclarationKinds,
        bool succeeded,
        string? failureReason)
    {
        var membersByDeclaration = memberOutcomes
            .GroupBy(member => member.DeclarationOrdinal)
            .ToDictionary(group => group.Key, group => group.ToList());
        var overloadsByDeclaration = overloadOutcomes
            .GroupBy(overload => overload.DeclarationOrdinal)
            .ToDictionary(group => group.Key, group => group.ToList());

        return symbol.Declarations
            .OrderBy(declaration => declaration.Ordinal)
            .Select(declaration =>
            {
                if (SourceAccountingShape.IsFactoryDeclaration(declaration))
                {
                    return CreateDeclaration(
                        symbol,
                        declaration,
                        MemberOutcomeStatus.Deferred,
                        SourceAccountingShape.FactoryConstructorPhase,
                        SourceAccountingShape.FactoryReason(declaration));
                }

                if (!emittedDeclarationKinds.Contains(declaration.Kind))
                {
                    var isGlobalFunction = declaration.Kind == "globalFunction";
                    return CreateDeclaration(
                        symbol,
                        declaration,
                        MemberOutcomeStatus.Deferred,
                        isGlobalFunction
                            ? SourceAccountingShape.GlobalFunctionPhase
                            : "declaration-emission",
                        isGlobalFunction
                            ? SourceAccountingShape.GlobalFunctionReason
                            : $"Declaration kind '{declaration.Kind}' is not emitted by this emitter.");
                }

                membersByDeclaration.TryGetValue(declaration.Ordinal, out var members);
                overloadsByDeclaration.TryGetValue(declaration.Ordinal, out var overloads);
                var statuses = (members ?? [])
                    .Select(member => member.Status)
                    .Concat((overloads ?? []).Select(overload => overload.Status))
                    .ToList();
                var status = statuses.Count > 0
                    ? AggregateStatus(statuses)
                    : succeeded
                        ? MemberOutcomeStatus.Projected
                        : MemberOutcomeStatus.NotAttemptedAfterFailure;
                var phase = status == MemberOutcomeStatus.Deferred
                    ? (members ?? []).FirstOrDefault(member =>
                        member.Status == MemberOutcomeStatus.Deferred)?.Phase
                      ?? (overloads ?? []).FirstOrDefault(overload =>
                          overload.Status == MemberOutcomeStatus.Deferred)?.Phase
                    : null;
                var reason = succeeded
                    ? status == MemberOutcomeStatus.Projected
                        ? "emitted"
                        : "Declaration contains explicitly deferred members or overloads."
                    : $"Not fully emitted because generation failed: {failureReason}";
                return CreateDeclaration(
                    symbol,
                    declaration,
                    status,
                    phase,
                    reason);
            })
            .ToList();
    }

    private static IReadOnlyList<OverloadOutcome> CompleteOverloads(
        SymbolModel symbol,
        IReadOnlyList<MemberOutcome> memberOutcomes,
        IReadOnlyList<OverloadOutcome> suppliedOutcomes,
        IReadOnlySet<string> emittedDeclarationKinds,
        bool succeeded,
        string? failureReason,
        string? failureProvenance)
    {
        var sourceOverloads = SourceAccountingShape.GetOverloads(symbol);
        var sourceIndex = sourceOverloads.ToDictionary(
            overload => overload.QualifiedKey,
            StringComparer.Ordinal);
        var normalized = new Dictionary<string, OverloadOutcome>(StringComparer.Ordinal);

        foreach (var supplied in suppliedOutcomes)
        {
            if (!sourceIndex.TryGetValue(supplied.QualifiedKey, out var source))
            {
                throw new InvalidOperationException(
                    $"Emitter produced an outcome for unknown overload '{supplied.QualifiedKey}'.");
            }
            if (!normalized.TryAdd(
                    source.QualifiedKey,
                    NormalizeOverload(source, supplied)))
            {
                throw new InvalidOperationException(
                    $"Emitter produced duplicate outcomes for overload '{source.QualifiedKey}'.");
            }
        }

        var memberIndex = memberOutcomes
            .Where(member => member.QualifiedKey is not null)
            .ToDictionary(member => member.QualifiedKey!, StringComparer.Ordinal);
        foreach (var source in sourceOverloads)
        {
            if (normalized.ContainsKey(source.QualifiedKey))
                continue;

            if (source.SourceMember is not null
                && memberIndex.TryGetValue(
                    source.SourceMember.QualifiedKey,
                    out var memberOutcome))
            {
                normalized[source.QualifiedKey] = CreateFromMember(
                    source,
                    memberOutcome);
                continue;
            }

            if (source.Declaration.Kind == "globalFunction"
                && !emittedDeclarationKinds.Contains(source.Declaration.Kind))
            {
                normalized[source.QualifiedKey] = CreateDefaultOverload(
                    source,
                    MemberOutcomeStatus.Deferred,
                    SourceAccountingShape.GlobalFunctionPhase,
                    SourceAccountingShape.GlobalFunctionReason);
                continue;
            }

            if (succeeded)
            {
                throw new InvalidOperationException(
                    $"Emitter produced no outcome for overload '{source.Provenance}'.");
            }

            normalized[source.QualifiedKey] = CreateDefaultOverload(
                source,
                MemberOutcomeStatus.NotAttemptedAfterFailure,
                null,
                $"Not attempted because emission failed at '{failureProvenance}': {failureReason}");
        }

        return sourceOverloads
            .Select(source => normalized[source.QualifiedKey])
            .ToList();
    }

    private static OverloadOutcome NormalizeOverload(
        SourceOverloadShape source,
        OverloadOutcome supplied)
        => supplied with
        {
            DeclarationOrdinal = source.Declaration.Ordinal,
            MemberOrdinal = source.MemberOrdinal,
            Name = source.Name,
            Kind = source.Kind,
            Provenance = source.Provenance,
            SourceLocation = source.SourceLocation,
            ParameterOutcomes = SourceAccountingShape.NormalizeParameterOutcomes(
                source,
                supplied.ParameterOutcomes),
        };

    private static OverloadOutcome CreateFromMember(
        SourceOverloadShape source,
        MemberOutcome member)
        => CreateDefaultOverload(
            source,
            member.Status,
            member.Phase,
            member.Reason ?? "");

    private static OverloadOutcome CreateDefaultOverload(
        SourceOverloadShape source,
        MemberOutcomeStatus status,
        string? phase,
        string reason)
        => new(
            source.QualifiedKey,
            source.Declaration.Ordinal,
            source.MemberOrdinal,
            source.Name,
            source.Kind,
            status,
            phase,
            reason,
            source.Provenance,
            source.SourceLocation,
            source.Parameters
                .OrderBy(parameter => parameter.Ordinal)
                .Select(parameter => new ParameterOutcome(
                    parameter.Ordinal,
                    parameter.Name,
                    status,
                    phase,
                    reason,
                    $"{source.Provenance}/parameter[{parameter.Ordinal}]/{parameter.Name}",
                    SourceAccountingShape.FormatLocation(parameter.Location)))
                .ToList());

    private static DeclarationOutcome CreateDeclaration(
        SymbolModel symbol,
        DeclarationModel declaration,
        MemberOutcomeStatus status,
        string? phase,
        string reason)
        => new(
            declaration.Ordinal,
            declaration.Kind,
            status,
            phase,
            reason,
            $"{symbol.Name}/decl[{declaration.Ordinal}]/{declaration.Kind}",
            SourceAccountingShape.FormatLocation(declaration.Location));

    private static MemberOutcomeStatus AggregateStatus(
        IReadOnlyList<MemberOutcomeStatus> statuses)
    {
        if (statuses.Contains(MemberOutcomeStatus.Failed))
            return MemberOutcomeStatus.Failed;
        if (statuses.Contains(MemberOutcomeStatus.NotAttemptedAfterFailure))
            return MemberOutcomeStatus.NotAttemptedAfterFailure;
        if (statuses.Contains(MemberOutcomeStatus.Deferred))
            return MemberOutcomeStatus.Deferred;
        return MemberOutcomeStatus.Projected;
    }
}
