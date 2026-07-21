using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.IR;

namespace Blazor.DOM.CSharpGenerator.Projection;

public enum AccessorDirection
{
    Get,
    Set,
}

public sealed record AccessorSource(
    MemberModel Member,
    int DeclarationOrdinal,
    string Provenance)
{
    internal string Name => Member.Name?.Text ?? "";
}

public sealed record AccessorTypeIdentity(
    ClrTypeIdentity ClrType,
    bool IsNullable,
    bool IsOptional,
    bool IncludesUndefined,
    TransportModel? Transport)
{
    public static AccessorTypeIdentity Create(
        TypeProjection projection,
        bool optional,
        TypeNode? sourceType)
        => new(
            projection.Identity,
            projection.IsNullable || optional,
            optional,
            ContainsUndefined(sourceType),
            projection.Transport);

    public bool StructurallyEquals(AccessorTypeIdentity other)
        => IsNullable == other.IsNullable
            && IsOptional == other.IsOptional
            && IncludesUndefined == other.IncludesUndefined
            && ClrTypeEquals(ClrType, other.ClrType)
            && TransportEquals(Transport, other.Transport);

    private static bool ClrTypeEquals(ClrTypeIdentity left, ClrTypeIdentity right)
    {
        if (!string.Equals(left.CanonicalName, right.CanonicalName, StringComparison.Ordinal)
            || left.Kind != right.Kind
            || left.IsAwaitable != right.IsAwaitable
            || left.GenericArity != right.GenericArity
            || left.IsTypeParameter != right.IsTypeParameter)
        {
            return false;
        }

        var leftArguments = left.TypeArguments ?? [];
        var rightArguments = right.TypeArguments ?? [];
        return leftArguments.Count == rightArguments.Count
            && leftArguments
                .Zip(rightArguments)
                .All(pair => ClrTypeEquals(pair.First, pair.Second));
    }

    private static bool TransportEquals(TransportModel? left, TransportModel? right)
        => left is null
            ? right is null
            : right is not null
                && string.Equals(left.Kind, right.Kind, StringComparison.Ordinal)
                && left.Nullable == right.Nullable
                && string.Equals(left.SourceType, right.SourceType, StringComparison.Ordinal)
                && left.Streamable == right.Streamable
                && left.StructuredClone == right.StructuredClone
                && string.Equals(left.Reason, right.Reason, StringComparison.Ordinal);

    private static bool ContainsUndefined(TypeNode? type)
        => type switch
        {
            KeywordTypeNode keyword => keyword.Name is
                "UndefinedKeyword" or "undefined",
            LiteralTypeNode literal => literal.LiteralKind is
                "UndefinedKeyword" or "UndefinedLiteral"
                || string.Equals(literal.Text, "undefined", StringComparison.Ordinal),
            OptionalTypeNode => true,
            UnionTypeNode union => union.Types.Any(ContainsUndefined),
            _ => false,
        };
}

public sealed record AccessorEndpoint(
    AccessorDirection Direction,
    TypeProjection Projection,
    AccessorTypeIdentity Identity,
    TypeNode SourceType,
    AccessorSource CanonicalSource,
    IReadOnlyList<AccessorSource> Sources);

public sealed record ReconciledAccessor(
    string JavaScriptName,
    string CSharpName,
    bool IsStatic,
    AccessorEndpoint? Getter,
    AccessorEndpoint? Setter,
    IReadOnlyList<AccessorSource> Sources,
    string Documentation,
    bool Deprecated)
{
    public int DeclarationOrdinal => Sources[0].DeclarationOrdinal;
    public int MemberOrdinal => Sources[0].Member.Ordinal;
    public bool IsSymmetric => Getter is not null
        && Setter is not null
        && Getter.Identity.StructurallyEquals(Setter.Identity);
}

public sealed record AccessorReconciliationResult(
    IReadOnlyList<ReconciledAccessor> Accessors,
    IReadOnlyList<MemberOutcome> DeferredOutcomes,
    IReadOnlyList<string> DeferredOutputs);

public sealed class AccessorReconciler(TypeResolver typeResolver)
{
    public AccessorReconciliationResult Reconcile(
        string symbolName,
        IReadOnlyList<DeclarationModel> declarations,
        GenericScope declarationScope,
        Func<string, TypeProjection, bool>? isAdditionallyMutable = null)
    {
        var deferredOutcomes = new List<MemberOutcome>();
        var deferredOutputs = new List<string>();
        var accessors = new List<ReconciledAccessor>();
        var csharpNames = new Dictionary<(bool IsStatic, string Name), ReconciledAccessor>();

        var groups = declarations
            .SelectMany(declaration => declaration.Members
                .Where(member =>
                    member.Name is not null
                    && member.Kind is "property" or "getter" or "setter")
                .Select(member => new AccessorSource(
                    member,
                    declaration.Ordinal,
                    SourceProvenance(symbolName, declaration.Ordinal, member))))
            .GroupBy(
                source => (source.Member.Static, source.Name),
                new AccessorGroupKeyComparer())
            .Select(group => group
                .OrderBy(source => source.DeclarationOrdinal)
                .ThenBy(source => source.Member.Ordinal)
                .ToList())
            .OrderBy(group => group[0].DeclarationOrdinal)
            .ThenBy(group => group[0].Member.Ordinal)
            .ThenBy(group => group[0].Name, StringComparer.Ordinal)
            .ToList();

        foreach (var sources in groups)
        {
            var reconciled = ReconcileGroup(
                symbolName,
                sources,
                declarationScope,
                isAdditionallyMutable,
                deferredOutcomes,
                deferredOutputs);
            if (reconciled is null)
                continue;

            var normalizedKey = (reconciled.IsStatic, reconciled.CSharpName);
            if (csharpNames.TryGetValue(normalizedKey, out var existing)
                && !string.Equals(
                    existing.JavaScriptName,
                    reconciled.JavaScriptName,
                    StringComparison.Ordinal))
            {
                throw new TypeProjectionException(
                    $"Accessor names '{existing.JavaScriptName}' and " +
                    $"'{reconciled.JavaScriptName}' on '{symbolName}' both normalize " +
                    $"to C# member '{reconciled.CSharpName}'. Sources: " +
                    $"{DescribeSources(existing.Sources)}; " +
                    $"{DescribeSources(reconciled.Sources)}.",
                    reconciled.Sources[0].Provenance);
            }

            csharpNames[normalizedKey] = reconciled;
            accessors.Add(reconciled);
        }

        return new AccessorReconciliationResult(
            accessors,
            deferredOutcomes,
            deferredOutputs);
    }

    private ReconciledAccessor? ReconcileGroup(
        string symbolName,
        IReadOnlyList<AccessorSource> sources,
        GenericScope declarationScope,
        Func<string, TypeProjection, bool>? isAdditionallyMutable,
        List<MemberOutcome> deferredOutcomes,
        List<string> deferredOutputs)
    {
        var getterCandidates = new List<AccessorCandidate>();
        var setterCandidates = new List<AccessorCandidate>();
        var activeSources = new HashSet<string>(StringComparer.Ordinal);

        foreach (var source in sources)
        {
            switch (source.Member.Kind)
            {
                case "property":
                {
                    var candidate = TryProject(
                        symbolName,
                        source,
                        source.Member.Type,
                        source.Member.Optional,
                        declarationScope,
                        deferredOutcomes,
                        deferredOutputs);
                    if (candidate is null)
                        break;

                    getterCandidates.Add(candidate);
                    activeSources.Add(source.Provenance);
                    if (!source.Member.Readonly
                        || isAdditionallyMutable?.Invoke(source.Name, candidate.Projection) == true)
                    {
                        setterCandidates.Add(candidate);
                    }
                    break;
                }

                case "getter":
                {
                    var candidate = TryProject(
                        symbolName,
                        source,
                        source.Member.ReturnType,
                        source.Member.Optional,
                        declarationScope,
                        deferredOutcomes,
                        deferredOutputs);
                    if (candidate is not null)
                    {
                        getterCandidates.Add(candidate);
                        activeSources.Add(source.Provenance);
                    }
                    break;
                }

                case "setter":
                {
                    if (source.Member.Parameters.Count != 1
                        || source.Member.Parameters[0].Rest)
                    {
                        throw new TypeProjectionException(
                            $"Setter '{symbolName}.{source.Name}' at " +
                            $"'{source.Provenance}' must declare exactly one value " +
                            "parameter (and it cannot be rest).",
                            source.Provenance);
                    }

                    var parameter = source.Member.Parameters[0];
                    var candidate = TryProject(
                        symbolName,
                        source,
                        parameter.Type,
                        parameter.Optional,
                        declarationScope,
                        deferredOutcomes,
                        deferredOutputs,
                        $"/parameter[{parameter.Ordinal}]/{parameter.Name}");
                    if (candidate is not null)
                    {
                        setterCandidates.Add(candidate);
                        activeSources.Add(source.Provenance);
                    }
                    break;
                }
            }
        }

        var active = sources
            .Where(source => activeSources.Contains(source.Provenance))
            .ToList();
        if (active.Count == 0)
            return null;

        var getter = ReconcileDirection(
            symbolName,
            sources[0].Name,
            AccessorDirection.Get,
            getterCandidates);
        var setter = ReconcileDirection(
            symbolName,
            sources[0].Name,
            AccessorDirection.Set,
            setterCandidates);
        if (getter is null && setter is null)
            return null;

        var documentation = active
            .Select(source => source.Member.Documentation.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return new ReconciledAccessor(
            sources[0].Name,
            Naming.ToCSharpMemberName(sources[0].Name),
            sources[0].Member.Static,
            getter,
            setter,
            active,
            string.Join("\n\n", documentation),
            active.Any(source => source.Member.Documentation.Deprecated));
    }

    private AccessorCandidate? TryProject(
        string symbolName,
        AccessorSource source,
        TypeNode? type,
        bool optional,
        GenericScope declarationScope,
        List<MemberOutcome> deferredOutcomes,
        List<string> deferredOutputs,
        string provenanceSuffix = "")
    {
        var projectedType = NormalizeThisType(
            symbolName,
            type,
            declarationScope);
        TypeProjection projection;
        try
        {
            projection = typeResolver.Project(
                projectedType,
                source.Provenance + provenanceSuffix,
                declarationScope);
        }
        catch (GenericDeferralException exception)
        {
            AddDeferred(
                source,
                exception.Phase,
                exception.Message,
                $"// DEFERRED ({exception.Phase}): {source.Name} — " +
                exception.Message,
                deferredOutcomes,
                deferredOutputs);
            return null;
        }
        catch (TypeProjectionException exception) when (
            exception.Message.Contains(
                "deferred to the events phase",
                StringComparison.Ordinal))
        {
            AddDeferred(
                source,
                "event-subscription",
                "Event handler property deferred to event-subscription phase.",
                $"// DEFERRED (events): {source.Name} — {exception.Provenance}",
                deferredOutcomes,
                deferredOutputs);
            return null;
        }

        if (projection.Identity.Kind is ClrTypeKind.Null or ClrTypeKind.Void)
        {
            AddDeferred(
                source,
                "undefined-type",
                $"Type resolves to '{projection.CSharpType}' (undefined/void in TypeScript).",
                $"// DEFERRED (undefined-type): {source.Name} — {source.Provenance}",
                deferredOutcomes,
                deferredOutputs);
            return null;
        }

        var effectiveProjection = projection with
        {
            IsNullable = optional || projection.IsNullable
        };
        return new AccessorCandidate(
            source,
            effectiveProjection,
            AccessorTypeIdentity.Create(
                effectiveProjection,
                optional,
                type),
            type!);
    }

    private static TypeNode? NormalizeThisType(
        string symbolName,
        TypeNode? type,
        GenericScope declarationScope)
    {
        if (type is not ReferenceTypeNode { Name: "this" }
            && !string.Equals(
                type?.CheckerType,
                "this",
                StringComparison.Ordinal))
        {
            return type;
        }

        var original = type!;
        return new ReferenceTypeNode(
            symbolName,
            symbolName,
            declarationScope.Parameters
                .Select(parameter => (TypeNode)new ReferenceTypeNode(
                    parameter.SourceName,
                    parameter.SourceName,
                    []))
                .ToList())
        {
            CheckerType = original.CheckerType,
            SyntaxKind = original.SyntaxKind,
            Transport = original.Transport,
        };
    }

    private static AccessorEndpoint? ReconcileDirection(
        string symbolName,
        string memberName,
        AccessorDirection direction,
        IReadOnlyList<AccessorCandidate> candidates)
    {
        if (candidates.Count == 0)
            return null;

        var canonical = candidates[0];
        var incompatible = candidates
            .Skip(1)
            .Where(candidate =>
                !canonical.Identity.StructurallyEquals(candidate.Identity))
            .ToList();
        if (incompatible.Count > 0)
        {
            var all = new[] { canonical }.Concat(incompatible).ToList();
            throw new TypeProjectionException(
                $"{direction} accessor '{symbolName}.{memberName}' has incompatible " +
                $"merged source types. Sources: " +
                $"{string.Join("; ", all.Select(candidate =>
                    $"{candidate.Source.Provenance} => " +
                    $"'{candidate.Projection.RenderedType}'"))}.",
                incompatible[0].Source.Provenance);
        }

        return new AccessorEndpoint(
            direction,
            canonical.Projection,
            canonical.Identity,
            canonical.SourceType,
            canonical.Source,
            candidates.Select(candidate => candidate.Source).ToList());
    }

    private static void AddDeferred(
        AccessorSource source,
        string phase,
        string reason,
        string output,
        List<MemberOutcome> outcomes,
        List<string> outputs)
    {
        outcomes.Add(new MemberOutcome(
            source.Member.Ordinal,
            source.Name,
            source.Member.Kind,
            MemberOutcomeStatus.Deferred,
            phase,
            reason,
            source.DeclarationOrdinal,
            Provenance: source.Provenance));
        if (!outputs.Contains(output, StringComparer.Ordinal))
            outputs.Add(output);
    }

    private static string SourceProvenance(
        string symbolName,
        int declarationOrdinal,
        MemberModel member)
        => $"{symbolName}/decl[{declarationOrdinal}]/member[{member.Ordinal}]/" +
            $"{member.Kind}/{member.Name?.Text ?? "(unnamed)"}";

    private static string DescribeSources(IReadOnlyList<AccessorSource> sources)
        => string.Join(", ", sources.Select(source => source.Provenance));

    private sealed record AccessorCandidate(
        AccessorSource Source,
        TypeProjection Projection,
        AccessorTypeIdentity Identity,
        TypeNode SourceType);

    private sealed class AccessorGroupKeyComparer
        : IEqualityComparer<(bool Static, string Name)>
    {
        public bool Equals(
            (bool Static, string Name) x,
            (bool Static, string Name) y)
            => x.Static == y.Static
                && string.Equals(x.Name, y.Name, StringComparison.Ordinal);

        public int GetHashCode((bool Static, string Name) value)
            => HashCode.Combine(
                value.Static,
                StringComparer.Ordinal.GetHashCode(value.Name));
    }
}
