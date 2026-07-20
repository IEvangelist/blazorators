// Interface emitter: projects TypeScript interfaces matched to WebIDL "interface" or
// "mixin" classification into C# partial interface contracts.
// - Preserves inheritance chain from TS heritage
// - Properties -> C# properties
// - Methods -> C# method signatures (bool|options params expand to three overloads)
// - Event-subscription generic overloads (addEventListener/removeEventListener<K extends keyof EventMap>):
//   deferred to the event-subscription phase with an honest DEFERRED comment.
// - Other generic methods: fail with provenance (require generic-emission phase approval).
// - Index signatures and call signatures: deferred with comment.
// FAIL-CLOSED: A symbol is only written when ALL in-scope members project successfully OR
// are legitimately deferred to a named phase. Any unhandled projection failure throws.

using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Projection;

namespace Blazor.DOM.CSharpGenerator.Emitters;

/// <summary>
/// Structured result from InterfaceEmitter.Emit. Contains the generated source plus
/// per-member outcomes for manifest accounting.
/// </summary>
public sealed record InterfaceEmitResult(
    string Source,
    IReadOnlyList<MemberOutcome> MemberOutcomes,
    IReadOnlyList<DeclarationOutcome>? DeclarationOutcomes = null,
    IReadOnlyList<OverloadOutcome>? OverloadOutcomes = null);

public sealed class InterfaceEmitException(
    string message,
    string provenance,
    IReadOnlyList<MemberOutcome> partialOutcomes,
    IReadOnlyList<DeclarationOutcome>? partialDeclarationOutcomes = null,
    IReadOnlyList<OverloadOutcome>? partialOverloadOutcomes = null)
    : TypeProjectionException(message, provenance)
{
    public IReadOnlyList<MemberOutcome> PartialOutcomes { get; } = partialOutcomes;
    public IReadOnlyList<DeclarationOutcome> PartialDeclarationOutcomes { get; } =
        partialDeclarationOutcomes ?? [];
    public IReadOnlyList<OverloadOutcome> PartialOverloadOutcomes { get; } =
        partialOverloadOutcomes ?? [];
}

public sealed class InterfaceEmitter(
    TypeResolver typeResolver,
    string generatorVersion,
    string ns,
    DeclarationRoutingPlan? routingPlan = null)
{
    private static readonly IReadOnlySet<string> EmittedDeclarationKinds =
        new HashSet<string>(["interface"], StringComparer.Ordinal);

    private sealed record AccessorRef(MemberModel Member, int DeclarationOrdinal);
    private sealed record MethodRef(MemberModel Member, int DeclarationOrdinal);
    private sealed record MethodSig(
        string Rendered,
        string CanonicalKey,
        string ReturnType,
        int DeclarationOrdinal,
        int OptionalParamCount = 0);
    private sealed record MethodBuildResult(
        IReadOnlyList<MethodSig> Outputs,
        IReadOnlyList<MemberOutcome> Outcomes,
        string? DeferredOutput = null);

    /// <summary>
    /// Emits a C# partial interface for a symbol classified as WebIDL interface or mixin.
    /// Processes ALL merged interface declarations — not just the first one.
    /// Throws <see cref="TypeProjectionException"/> if any member fails to project.
    /// Returns a structured result with source and per-member outcomes.
    /// </summary>
    public InterfaceEmitResult Emit(SymbolModel symbol)
    {
        try
        {
            var result = EmitCore(symbol);
            var outcomes = EmitterOutcomeReconciler.CompleteSuccess(
                symbol,
                result.MemberOutcomes,
                EmittedDeclarationKinds);
            return result with
            {
                MemberOutcomes = outcomes.MemberOutcomes,
                DeclarationOutcomes = outcomes.DeclarationOutcomes,
                OverloadOutcomes = outcomes.OverloadOutcomes,
            };
        }
        catch (InterfaceEmitException ex)
        {
            throw CompleteFailure(symbol, ex.Message, ex.Provenance, ex.PartialOutcomes);
        }
        catch (MemberOutcomeReconciliationException ex)
        {
            throw CompleteFailure(symbol, ex.Message, ex.Provenance, ex.PartialOutcomes);
        }
        catch (TypeProjectionException ex)
        {
            throw CompleteFailure(symbol, ex.Message, ex.Provenance, []);
        }
        catch (Exception ex)
        {
            throw CompleteFailure(symbol, ex.Message, $"{symbol.Name}/emitter", []);
        }
    }

    private InterfaceEmitResult EmitCore(SymbolModel symbol)
    {
        var allDecls = symbol.Declarations
            .Where(d => d.Kind == "interface")
            .OrderBy(d => d.Ordinal)
            .ToList();

        if (allDecls.Count == 0)
            throw new InvalidOperationException(
                $"InterfaceEmitter: '{symbol.Name}' has no interface declaration.");

        var primaryDecl = allDecls[0];

        if (primaryDecl.EventMap.IsEventMap)
            throw new InvalidOperationException(
                $"InterfaceEmitter: '{symbol.Name}' is an event map and should be deferred, not emitted.");

        var csName = Naming.ToCSharpSimpleTypeName(symbol.Name);
        string baseClause;
        try
        {
            baseClause = BuildBaseClause(allDecls, typeResolver, symbol.Name);
        }
        catch (TypeProjectionException ex)
        {
            throw new InterfaceEmitException(ex.Message, ex.Provenance, []);
        }

        var propertyOutputs = new List<string>();
        var methodOutputs = new List<string>();
        var memberOutcomes = new List<MemberOutcome>();
        var memberFailures = new List<TypeProjectionException>();
        var emittedPropertyKeys = new HashSet<string>(StringComparer.Ordinal);
        var emittedDeferredMethodOutputs = new HashSet<string>(StringComparer.Ordinal);
        var emittedMethodKeys = new Dictionary<string, MethodSig>(StringComparer.Ordinal);
        var emittedMethodOutputIndices = new Dictionary<string, int>(StringComparer.Ordinal);

        var accessorGroups = allDecls
            .SelectMany(d => d.Members
                .Where(m => m.Kind is "property" or "getter" or "setter" && m.Name is not null)
                .Select(m => new AccessorRef(m, d.Ordinal)))
            .GroupBy(m => m.Member.Name!.Text, StringComparer.Ordinal)
            .Select(g => g.OrderBy(m => m.DeclarationOrdinal).ThenBy(m => m.Member.Ordinal).ToList())
            .OrderBy(g => g[0].DeclarationOrdinal)
            .ThenBy(g => g[0].Member.Ordinal)
            .ToList();

        foreach (var group in accessorGroups)
        {
            var canonical = group.FirstOrDefault(m => m.Member.Kind != "setter");
            if (canonical is null)
            {
                var setter = group[0];
                var setterName = setter.Member.Name?.Text ?? "";
                var failure = new TypeProjectionException(
                    $"Setter '{symbol.Name}.{setterName}' in decl[{setter.DeclarationOrdinal}] has no paired getter. " +
                    "Standalone setter accessors are not representable in C# interfaces without a getter.",
                    $"{symbol.Name}/{setterName}/setter");
                memberFailures.Add(failure);
                memberOutcomes.AddRange(group.Select(accessor => new MemberOutcome(
                    accessor.Member.Ordinal,
                    accessor.Member.Name?.Text ?? "",
                    accessor.Member.Kind,
                    MemberOutcomeStatus.Failed,
                    null,
                    failure.Message,
                    accessor.DeclarationOrdinal)));
                continue;
            }

            try
            {
                var (output, outcomes) = BuildProperty(
                    canonical.Member,
                    symbol.Name,
                    allDecls,
                    canonical.DeclarationOrdinal);

                memberOutcomes.AddRange(outcomes);

                var propKey = $"prop:{canonical.Member.Name!.Text}";
                if (output is not null && emittedPropertyKeys.Add(propKey))
                    propertyOutputs.Add(output);
            }
            catch (TypeProjectionException ex)
            {
                memberFailures.Add(ex);
                memberOutcomes.AddRange(group.Select(accessor => new MemberOutcome(
                    accessor.Member.Ordinal,
                    accessor.Member.Name?.Text ?? "",
                    accessor.Member.Kind,
                    MemberOutcomeStatus.Failed,
                    null,
                    ex.Message,
                    accessor.DeclarationOrdinal)));
            }
        }

        foreach (var decl in allDecls)
        {
            foreach (var methodRef in decl.Members
                .Where(m => m.Kind == "method" && m.Name is not null)
                .OrderBy(m => m.Ordinal)
                .Select(m => new MethodRef(m, decl.Ordinal)))
            {
                try
                {
                    var build = BuildMethod(
                        methodRef.Member,
                        symbol.Name,
                        methodRef.DeclarationOrdinal);
                    var outcomes = build.Outcomes.ToList();

                    if (build.DeferredOutput is not null
                        && emittedDeferredMethodOutputs.Add(build.DeferredOutput))
                    {
                        methodOutputs.Add(build.DeferredOutput);
                    }

                    var dedupedFromDecl = new HashSet<int>();
                    foreach (var sig in build.Outputs)
                    {
                        if (emittedMethodKeys.TryGetValue(sig.CanonicalKey, out var existing))
                        {
                            if (!string.Equals(existing.ReturnType, sig.ReturnType, StringComparison.Ordinal))
                            {
                                var methodName = methodRef.Member.Name?.Text ?? "";
                                throw new TypeProjectionException(
                                    $"Method '{symbol.Name}.{methodName}' in decl[{methodRef.DeclarationOrdinal}] collides with decl[{existing.DeclarationOrdinal}] " +
                                    $"for canonical signature '{sig.CanonicalKey}' but has incompatible return types '{existing.ReturnType}' and '{sig.ReturnType}'.",
                                    $"{symbol.Name}/decl[{methodRef.DeclarationOrdinal}]/{methodName}/method[{methodRef.Member.Ordinal}]/return");
                            }

                            if (sig.OptionalParamCount > existing.OptionalParamCount
                                && emittedMethodOutputIndices.TryGetValue(sig.CanonicalKey, out var outputIndex))
                            {
                                methodOutputs[outputIndex] = sig.Rendered;
                                emittedMethodKeys[sig.CanonicalKey] = sig;
                            }

                            dedupedFromDecl.Add(existing.DeclarationOrdinal);
                            continue;
                        }

                        emittedMethodKeys.Add(sig.CanonicalKey, sig);
                        emittedMethodOutputIndices[sig.CanonicalKey] = methodOutputs.Count;
                        methodOutputs.Add(sig.Rendered);
                    }

                    if (dedupedFromDecl.Count > 0)
                    {
                        var reason = $"Deduplicated from declaration ordinal {dedupedFromDecl.Min()}.";
                        outcomes = outcomes
                            .Select(o => o with { Reason = AppendReason(o.Reason, reason) })
                            .ToList();
                    }

                    memberOutcomes.AddRange(outcomes);
                }
                catch (TypeProjectionException ex)
                {
                    memberFailures.Add(ex);
                    memberOutcomes.Add(new MemberOutcome(
                        methodRef.Member.Ordinal,
                        methodRef.Member.Name?.Text ?? "",
                        methodRef.Member.Kind,
                        MemberOutcomeStatus.Failed,
                        null,
                        ex.Message,
                        methodRef.DeclarationOrdinal));
                }
            }

            foreach (var m in decl.Members.Where(m => m.Kind == "indexSignature"))
                memberOutcomes.Add(new MemberOutcome(
                    m.Ordinal,
                    m.Name?.Text ?? "indexSignature",
                    "indexSignature",
                    MemberOutcomeStatus.Deferred,
                    "index-accessor",
                    "Index signatures deferred to index-accessor phase.",
                    decl.Ordinal));

            foreach (var m in decl.Members.Where(m => m.Kind == "callSignature"))
                memberOutcomes.Add(new MemberOutcome(
                    m.Ordinal,
                    m.Name?.Text ?? "callSignature",
                    "callSignature",
                    MemberOutcomeStatus.Deferred,
                    "callable-interface",
                    "Call signatures deferred to callable-interface phase.",
                    decl.Ordinal));

            foreach (var m in decl.Members.Where(m => m.Kind == "constructSignature"))
                memberOutcomes.Add(new MemberOutcome(
                    m.Ordinal,
                    m.Name?.Text ?? "constructSignature",
                    "constructSignature",
                    MemberOutcomeStatus.Deferred,
                    "factory",
                    "Constructor signatures deferred to factory phase.",
                    decl.Ordinal));
        }

        if (memberFailures.Count > 0)
        {
            var firstFailure = memberFailures[0];
            throw new InterfaceEmitException(
                firstFailure.Message,
                firstFailure.Provenance,
                memberOutcomes);
        }

        var w = new CSharpWriter();
        w.AppendLine("#nullable enable");
        w.AppendLine(CSharpWriter.AutoGeneratedHeader("Blazor.DOM.CSharpGenerator", generatorVersion));
        w.AppendLine($"namespace {Naming.ToGeneratedNamespace(ns, symbol.Name)};");
        w.AppendLine();

        var docText = primaryDecl.Documentation?.Text ?? "";
        var deprecated = primaryDecl.Documentation?.Deprecated ?? false;
        w.XmlDoc(docText, deprecated);

        if (symbol.Semantic.SecureContext)
            w.AppendLine("// Requires secure context (HTTPS).");
        if (symbol.Semantic.Transferable)
            w.AppendLine("// Transferable (supports postMessage transfer).");

        var header = string.IsNullOrEmpty(baseClause)
            ? $"public partial interface I{csName}"
            : $"public partial interface I{csName} : {baseClause}";

        w.Block(header, () =>
        {
            for (var i = 0; i < propertyOutputs.Count; i++)
            {
                if (i > 0) w.AppendLine();
                w.AppendLine(propertyOutputs[i]);
            }

            for (var i = 0; i < methodOutputs.Count; i++)
            {
                if (propertyOutputs.Count > 0 || i > 0) w.AppendLine();
                w.AppendLine(methodOutputs[i]);
            }
        });

        return new InterfaceEmitResult(w.ToString(), memberOutcomes);
    }

    private static InterfaceEmitException CompleteFailure(
        SymbolModel symbol,
        string message,
        string provenance,
        IReadOnlyList<MemberOutcome> partialOutcomes)
    {
        var outcomes = EmitterOutcomeReconciler.CompleteFailure(
            symbol,
            partialOutcomes,
            EmittedDeclarationKinds,
            message,
            provenance);
        return new InterfaceEmitException(
            message,
            provenance,
            outcomes.MemberOutcomes,
            outcomes.DeclarationOutcomes,
            outcomes.OverloadOutcomes);
    }

    /// <summary>Emits and returns source only (for test compatibility).</summary>
    public string EmitSource(SymbolModel symbol) => Emit(symbol).Source;

    private (string? Output, IReadOnlyList<MemberOutcome> Outcomes) BuildProperty(
        MemberModel member,
        string symbolName,
        IReadOnlyList<DeclarationModel> allDecls,
        int declOrdinal)
    {
        var memberName = member.Name?.Text;
        if (memberName is null)
        {
            return (null, [
                new MemberOutcome(
                    member.Ordinal,
                    "",
                    member.Kind,
                    MemberOutcomeStatus.Deferred,
                    "unknown",
                    "Member has no name.",
                    declOrdinal)
            ]);
        }

        var related = allDecls
            .SelectMany(d => d.Members
                .Where(m => m.Name?.Text == memberName && m.Kind is "property" or "getter" or "setter")
                .Select(m => new AccessorRef(m, d.Ordinal)))
            .OrderBy(m => m.DeclarationOrdinal)
            .ThenBy(m => m.Member.Ordinal)
            .ToList();

        if (member.Kind == "setter")
            throw new TypeProjectionException(
                $"Setter '{symbolName}.{memberName}' in decl[{declOrdinal}] has no paired getter. " +
                "Standalone setter accessors are not representable in C# interfaces without a getter.",
                $"{symbolName}/{memberName}/setter");

        var (docText, deprecated) = MergeGlobalDocumentation(
            symbolName,
            memberName,
            member.Documentation,
            DeclarationRouteKind.GlobalVariable);
        var csName = Naming.ToCSharpMemberName(memberName);
        var outcomes = new List<MemberOutcome>();

        if (member.Kind == "property")
        {
            var propertyRefs = related.Where(r => r.Member.Kind == "property").ToList();
            var accessorRefs = related.Where(r => r.Member.Kind is "getter" or "setter").ToList();
            if (accessorRefs.Count > 0)
                throw new TypeProjectionException(
                    $"Property '{symbolName}.{memberName}' in decl[{declOrdinal}] is declaration-merged with accessor members. " +
                    "Mixed property/accessor shapes are not representable deterministically.",
                    $"{symbolName}/{memberName}/property");

            TypeProjection? canonicalProjection = null;
            string? deferredOutput = null;
            foreach (var propertyRef in propertyRefs)
            {
                TypeProjection projection;
                try
                {
                    projection = typeResolver.Project(
                        propertyRef.Member.Type,
                        $"{symbolName}/decl[{propertyRef.DeclarationOrdinal}]/{memberName}/property");
                }
                catch (TypeProjectionException ex) when (ex.Message.Contains("deferred to the events phase", StringComparison.Ordinal))
                {
                    outcomes.Add(new MemberOutcome(
                        propertyRef.Member.Ordinal,
                        memberName,
                        propertyRef.Member.Kind,
                        MemberOutcomeStatus.Deferred,
                        "event-subscription",
                        "Event handler property deferred to event-subscription phase.",
                        propertyRef.DeclarationOrdinal));
                    deferredOutput ??= $"// DEFERRED (events): {memberName} — {ex.Provenance}";
                    continue;
                }

                if (projection.CSharpType is "null" or "void")
                {
                    outcomes.Add(new MemberOutcome(
                        propertyRef.Member.Ordinal,
                        memberName,
                        propertyRef.Member.Kind,
                        MemberOutcomeStatus.Deferred,
                        "undefined-type",
                        $"Type resolves to '{projection.CSharpType}' (undefined/void in TypeScript).",
                        propertyRef.DeclarationOrdinal));
                    continue;
                }

                if (canonicalProjection is null)
                {
                    canonicalProjection = projection;
                    outcomes.Add(new MemberOutcome(
                        propertyRef.Member.Ordinal,
                        memberName,
                        propertyRef.Member.Kind,
                        MemberOutcomeStatus.Projected,
                        null,
                        null,
                        propertyRef.DeclarationOrdinal));
                    continue;
                }

                if (!string.Equals(
                        canonicalProjection.CanonicalType,
                        projection.CanonicalType,
                        StringComparison.Ordinal)
                    || propertyRef.Member.Readonly != propertyRefs[0].Member.Readonly)
                {
                    throw new TypeProjectionException(
                        $"Property '{symbolName}.{memberName}' has incompatible merged declarations between decl[{propertyRefs[0].DeclarationOrdinal}] and decl[{propertyRef.DeclarationOrdinal}].",
                        $"{symbolName}/{memberName}/property");
                }

                outcomes.Add(new MemberOutcome(
                    propertyRef.Member.Ordinal,
                    memberName,
                    propertyRef.Member.Kind,
                    MemberOutcomeStatus.Projected,
                    null,
                    $"Deduplicated from declaration ordinal {propertyRefs[0].DeclarationOrdinal}.",
                    propertyRef.DeclarationOrdinal));
            }

            if (canonicalProjection is null)
                return (deferredOutput, outcomes);

            var globallyMutable = IsGloballyMutable(
                symbolName,
                memberName,
                canonicalProjection);
            var csType = ApplyPropertyNullability(
                canonicalProjection,
                member.Optional || canonicalProjection.IsNullable);
            var w = new CSharpWriter();
            w.XmlDoc(docText, deprecated);
            w.AppendLine(member.Readonly && !globallyMutable
                ? $"{csType} {csName} {{ get; }}"
                : $"{csType} {csName} {{ get; set; }}");
            return (w.ToString().TrimEnd(), outcomes);
        }

        var getterRefs = related.Where(r => r.Member.Kind == "getter").ToList();
        var setterRefs = related.Where(r => r.Member.Kind == "setter").ToList();
        var propertyLikeRefs = related.Where(r => r.Member.Kind == "property").ToList();
        if (propertyLikeRefs.Count > 0)
            throw new TypeProjectionException(
                $"Getter '{symbolName}.{memberName}' in decl[{declOrdinal}] is declaration-merged with property members. " +
                "Mixed property/accessor shapes are not representable deterministically.",
                $"{symbolName}/{memberName}/getter");

        var canonicalGetter = getterRefs[0];
        TypeProjection? getterProjection = null;
        string? deferredGetterOutput = null;
        foreach (var getterRef in getterRefs)
        {
            TypeProjection projection;
            try
            {
                projection = typeResolver.Project(
                    getterRef.Member.ReturnType,
                    $"{symbolName}/decl[{getterRef.DeclarationOrdinal}]/{memberName}/getter");
            }
            catch (TypeProjectionException ex) when (ex.Message.Contains("deferred to the events phase", StringComparison.Ordinal))
            {
                outcomes.Add(new MemberOutcome(
                    getterRef.Member.Ordinal,
                    memberName,
                    getterRef.Member.Kind,
                    MemberOutcomeStatus.Deferred,
                    "event-subscription",
                    "Event handler property deferred to event-subscription phase.",
                    getterRef.DeclarationOrdinal));
                deferredGetterOutput ??= $"// DEFERRED (events): {memberName} — {ex.Provenance}";
                continue;
            }

            if (projection.CSharpType is "null" or "void")
            {
                outcomes.Add(new MemberOutcome(
                    getterRef.Member.Ordinal,
                    memberName,
                    getterRef.Member.Kind,
                    MemberOutcomeStatus.Deferred,
                    "undefined-type",
                    $"Type resolves to '{projection.CSharpType}' (undefined/void in TypeScript).",
                    getterRef.DeclarationOrdinal));
                continue;
            }

            if (getterProjection is null)
            {
                getterProjection = projection;
                outcomes.Add(new MemberOutcome(
                    getterRef.Member.Ordinal,
                    memberName,
                    getterRef.Member.Kind,
                    MemberOutcomeStatus.Projected,
                    null,
                    getterRef.DeclarationOrdinal == canonicalGetter.DeclarationOrdinal && getterRef.Member.Ordinal == canonicalGetter.Member.Ordinal
                        ? null
                        : $"Deduplicated from declaration ordinal {canonicalGetter.DeclarationOrdinal}.",
                    getterRef.DeclarationOrdinal));
                continue;
            }

            if (!string.Equals(
                    getterProjection.CanonicalType,
                    projection.CanonicalType,
                    StringComparison.Ordinal))
            {
                throw new TypeProjectionException(
                    $"Getter '{symbolName}.{memberName}' has incompatible merged declarations between decl[{canonicalGetter.DeclarationOrdinal}] and decl[{getterRef.DeclarationOrdinal}].",
                    $"{symbolName}/{memberName}/getter");
            }

            outcomes.Add(new MemberOutcome(
                getterRef.Member.Ordinal,
                memberName,
                getterRef.Member.Kind,
                MemberOutcomeStatus.Projected,
                null,
                $"Deduplicated from declaration ordinal {canonicalGetter.DeclarationOrdinal}.",
                getterRef.DeclarationOrdinal));
        }

        if (getterProjection is null)
            return (deferredGetterOutput, outcomes);

        var effectiveGetterProjection = getterProjection with
        {
            IsNullable = member.Optional || getterProjection.IsNullable
        };
        var getterType = effectiveGetterProjection.RenderedType;
        var normalizedGetterType = effectiveGetterProjection.CanonicalType;
        var hasSetter = setterRefs.Count > 0;
        var globallyMutableAccessor = IsGloballyMutable(
            symbolName,
            memberName,
            getterProjection);
        AccessorRef? canonicalSetter = setterRefs.Count > 0 ? setterRefs[0] : null;

        foreach (var setterRef in setterRefs)
        {
            if (setterRef.Member.Parameters.Count != 1)
            {
                throw new TypeProjectionException(
                    $"Setter '{symbolName}.{memberName}' in decl[{setterRef.DeclarationOrdinal}] must declare exactly one value parameter.",
                    $"{symbolName}/{memberName}/setter");
            }

            var setterParam = setterRef.Member.Parameters[0];
            var setterProjection = typeResolver.Project(
                setterParam.Type,
                $"{symbolName}/decl[{setterRef.DeclarationOrdinal}]/{memberName}/setter/{setterParam.Name}");
            var effectiveSetterProjection = setterProjection with
            {
                IsNullable = setterParam.Optional || setterProjection.IsNullable
            };
            var setterType = effectiveSetterProjection.RenderedType;
            if (!string.Equals(normalizedGetterType, effectiveSetterProjection.CanonicalType, StringComparison.Ordinal))
            {
                throw new TypeProjectionException(
                    $"Getter/setter pair '{symbolName}.{memberName}' is incompatible: getter projects to '{getterType}' " +
                    $"but setter in decl[{setterRef.DeclarationOrdinal}] projects to '{setterType}'.",
                    $"{symbolName}/{memberName}/setter");
            }

            outcomes.Add(new MemberOutcome(
                setterRef.Member.Ordinal,
                memberName,
                setterRef.Member.Kind,
                MemberOutcomeStatus.Projected,
                null,
                setterRef.DeclarationOrdinal == canonicalSetter!.DeclarationOrdinal && setterRef.Member.Ordinal == canonicalSetter.Member.Ordinal
                    ? $"Paired with getter member ordinal {canonicalGetter.Member.Ordinal}."
                    : $"Paired with getter member ordinal {canonicalGetter.Member.Ordinal}; deduplicated from declaration ordinal {canonicalSetter.DeclarationOrdinal}.",
                setterRef.DeclarationOrdinal));
        }

        var w2 = new CSharpWriter();
        w2.XmlDoc(docText, deprecated);
        w2.AppendLine(hasSetter || globallyMutableAccessor
            ? $"{getterType} {csName} {{ get; set; }}"
            : $"{getterType} {csName} {{ get; }}");
        return (w2.ToString().TrimEnd(), outcomes);
    }

    private MethodBuildResult BuildMethod(MemberModel method, string symbolName, int declOrdinal)
    {
        var memberName = method.Name?.Text;
        if (memberName is null)
            return new MethodBuildResult([], []);

        var provenance = $"{symbolName}/decl[{declOrdinal}]/{memberName}";

        if (method.TypeParameters.Count > 0)
        {
            var typeParamNames = string.Join(", ", method.TypeParameters.Select(tp => tp.Name));

            if (IsEventSubscriptionOverload(method))
            {
                return new MethodBuildResult(
                    [],
                    [
                        new MemberOutcome(
                            method.Ordinal,
                            memberName,
                            method.Kind,
                            MemberOutcomeStatus.Deferred,
                            "event-subscription",
                            "Event-map-keyed generic overload deferred to event-subscription phase.",
                            declOrdinal)
                    ],
                    $"// DEFERRED (event-subscription): {memberName}<{typeParamNames}> — deferred to typed event subscription phase.");
            }

            throw new TypeProjectionException(
                $"Generic method '{symbolName}.{memberName}<{typeParamNames}>' requires the generic-emission phase. " +
                "This is not an event-subscription overload (must be named addEventListener/removeEventListener " +
                "with all type parameters constrained by keyof <EventMapType>).",
                provenance);
        }

        var returnProj = typeResolver.Project(method.ReturnType, $"{provenance}/return");
        var (docText, deprecated) = MergeGlobalDocumentation(
            symbolName,
            memberName,
            method.Documentation,
            DeclarationRouteKind.GlobalFunction);
        var csBaseName = Naming.ToCSharpMemberName(memberName);
        var emittedName = returnProj.Identity.IsAwaitable
            ? $"{csBaseName}Async"
            : csBaseName;
        var csReturn = returnProj.RenderedType;
        var paramList = MergeGlobalParameterForms(
            symbolName,
            memberName,
            method.Parameters,
            returnProj);
        var optionalParamCount = 0;

        for (var pi = 0; pi < paramList.Count; pi++)
        {
            if (!TryGetBoolOptionsUnion(paramList[pi].Type, out var optionsTypeName))
                continue;

            var noOptionsOverload = BuildMethodSignature(
                emittedName,
                csReturn,
                returnProj.CanonicalType,
                paramList,
                pi,
                null,
                null,
                null,
                pi,
                docText,
                deprecated,
                provenance,
                declOrdinal);
            var boolOverload = BuildMethodSignature(
                emittedName,
                csReturn,
                returnProj.CanonicalType,
                paramList,
                pi,
                "bool",
                "capture",
                "bool",
                -1,
                null,
                false,
                provenance,
                declOrdinal);
            var optOverload = BuildMethodSignature(
                emittedName,
                csReturn,
                returnProj.CanonicalType,
                paramList,
                pi,
                optionsTypeName + "?",
                paramList[pi].Name,
                optionsTypeName,
                -1,
                null,
                false,
                provenance,
                declOrdinal);

            return new MethodBuildResult(
                [noOptionsOverload, boolOverload, optOverload],
                [
                    new MemberOutcome(
                        method.Ordinal,
                        memberName,
                        method.Kind,
                        MemberOutcomeStatus.Projected,
                        null,
                        $"Expanded bool|{optionsTypeName} optional param into three unambiguous overloads (no-arg, bool, options).",
                        declOrdinal)
                ]);
        }

        var parts = new List<string>();
        var canonicalParamTypes = new List<string>();
        foreach (var p in paramList)
        {
            var pProj = typeResolver.Project(p.Type, $"{provenance}/{p.Name}");
            var pType = FormatParameterType(pProj);
            var pName = Naming.ToCSharpParameterName(p.Name);

            if (p.Rest)
            {
                var restElementType = pType.EndsWith("[]", StringComparison.Ordinal) ? pType[..^2] : pType;
                parts.Add($"params {restElementType}[] {pName}");
                canonicalParamTypes.Add(pProj.CanonicalType);
            }
            else if (p.Optional)
            {
                var optionalProjection = pProj with
                {
                    IsNullable = pProj.IsNullable
                        || pProj.Identity.Kind == ClrTypeKind.Reference,
                };
                parts.Add(
                    $"{FormatParameterType(optionalProjection)} {pName} = default");
                canonicalParamTypes.Add(optionalProjection.CanonicalType);
                optionalParamCount++;
            }
            else
            {
                parts.Add($"{pType} {pName}");
                canonicalParamTypes.Add(pProj.CanonicalType);
            }
        }

        var w = new CSharpWriter();
        w.XmlDoc(docText, deprecated);
        w.AppendLine($"{csReturn} {emittedName}({string.Join(", ", parts)});");

        return new MethodBuildResult(
            [new MethodSig(
                w.ToString().TrimEnd(),
                CanonicalMethodKey(emittedName, canonicalParamTypes),
                returnProj.CanonicalType,
                declOrdinal,
                optionalParamCount)],
            [
                new MemberOutcome(
                    method.Ordinal,
                    memberName,
                    method.Kind,
                    MemberOutcomeStatus.Projected,
                    null,
                    null,
                    declOrdinal)
            ]);
    }

    private MethodSig BuildMethodSignature(
        string emittedName,
        string csReturn,
        string canonicalReturnType,
        IReadOnlyList<ParameterModel> paramList,
        int substituteIndex,
        string? substituteType,
        string? substituteName,
        string? substituteCanonicalType,
        int dropFromIndex,
        string? docText,
        bool deprecated,
        string provenance,
        int declOrdinal)
    {
        var parts = new List<string>();
        var canonicalParamTypes = new List<string>();
        var optionalParamCount = 0;
        for (var i = 0; i < paramList.Count; i++)
        {
            if (dropFromIndex >= 0 && i >= dropFromIndex)
                break;

            if (i == substituteIndex && substituteType is not null && substituteName is not null)
            {
                var substitutedName = Naming.ToCSharpParameterName(substituteName);
                parts.Add($"{substituteType} {substitutedName}");
                canonicalParamTypes.Add(substituteCanonicalType ?? substituteType);
                continue;
            }

            var p = paramList[i];
            var pProj = typeResolver.Project(p.Type, $"{provenance}/{p.Name}");
            var pType = FormatParameterType(pProj);
            var pName = Naming.ToCSharpParameterName(p.Name);

            if (p.Rest)
            {
                var restElementType = pType.EndsWith("[]", StringComparison.Ordinal) ? pType[..^2] : pType;
                parts.Add($"params {restElementType}[] {pName}");
                canonicalParamTypes.Add(pProj.CanonicalType);
            }
            else if (p.Optional)
            {
                var optionalProjection = pProj with
                {
                    IsNullable = pProj.IsNullable
                        || pProj.Identity.Kind == ClrTypeKind.Reference,
                };
                parts.Add(
                    $"{FormatParameterType(optionalProjection)} {pName} = default");
                canonicalParamTypes.Add(optionalProjection.CanonicalType);
                optionalParamCount++;
            }
            else
            {
                parts.Add($"{pType} {pName}");
                canonicalParamTypes.Add(pProj.CanonicalType);
            }
        }

        var w = new CSharpWriter();
        if (docText is not null)
            w.XmlDoc(docText, deprecated);
        w.AppendLine($"{csReturn} {emittedName}({string.Join(", ", parts)});");

        return new MethodSig(
            w.ToString().TrimEnd(),
            CanonicalMethodKey(emittedName, canonicalParamTypes),
            canonicalReturnType,
            declOrdinal,
            optionalParamCount);
    }

    private static string BuildBaseClause(
        IReadOnlyList<DeclarationModel> allDecls,
        TypeResolver typeResolver,
        string symbolName)
    {
        var seenBases = new HashSet<string>(StringComparer.Ordinal);
        var bases = new List<string>();

        foreach (var decl in allDecls)
        {
            foreach (var heritage in decl.Heritage)
            {
                if (heritage.Token != "extends")
                {
                    throw new TypeProjectionException(
                        $"Interface '{symbolName}' decl[{decl.Ordinal}] has unsupported heritage clause token '{heritage.Token}'. " +
                        "Only 'extends' is supported for interface heritage.",
                        $"{symbolName}/heritage/{heritage.Token}");
                }

                foreach (var typeNode in heritage.Types)
                {
                    if (typeNode is not HeritageReferenceTypeNode hrt)
                    {
                        throw new TypeProjectionException(
                            $"Interface '{symbolName}' decl[{decl.Ordinal}] has unsupported heritage node kind '{typeNode.Kind}'. " +
                            "Non-reference heritage (e.g. generic computed) requires the generic-heritage phase.",
                            $"{symbolName}/extends");
                    }

                    if (hrt.TypeArguments.Count > 0)
                    {
                        throw new TypeProjectionException(
                            $"Interface '{symbolName}' decl[{decl.Ordinal}] extends generic type '{hrt.Expression}<...>'. " +
                            "Generic heritage requires the generic-heritage phase.",
                            $"{symbolName}/extends/{hrt.Expression}");
                    }

                    var resolvedBaseName = hrt.ResolvedSymbol ?? hrt.Expression;
                    if (!typeResolver.IsKnownSymbol(resolvedBaseName))
                    {
                        throw new TypeProjectionException(
                            $"Interface '{symbolName}' decl[{decl.Ordinal}] extends unknown symbol '{resolvedBaseName}'. " +
                            "Add it to the symbol index or provide an emitter override.",
                            $"{symbolName}/extends/{resolvedBaseName}");
                    }

                    if (!typeResolver.IsInterfaceOrMixin(resolvedBaseName))
                    {
                        throw new TypeProjectionException(
                            $"Interface '{symbolName}' decl[{decl.Ordinal}] extends '{resolvedBaseName}' which has classification " +
                            $"'{typeResolver.GetClassification(resolvedBaseName)}', not interface/mixin. " +
                            "Only interface/mixin bases are valid in a C# interface base clause.",
                            $"{symbolName}/extends/{resolvedBaseName}");
                    }

                    var csBase = typeResolver.GetCSharpTypeReference(resolvedBaseName);
                    if (seenBases.Add(csBase))
                        bases.Add(csBase);
                }
            }
        }

        return string.Join(", ", bases);
    }

    private static string CanonicalMethodKey(
        string csName,
        IReadOnlyList<string> canonicalParamTypes)
        => $"{csName}({string.Join(",", canonicalParamTypes)})";

    private static string ApplyPropertyNullability(
        TypeProjection projection,
        bool nullable)
        => (projection with { IsNullable = projection.IsNullable || nullable }).RenderedType;

    private List<ParameterModel> MergeGlobalParameterForms(
        string ownerSymbol,
        string memberName,
        IReadOnlyList<ParameterModel> ownerParameters,
        TypeProjection ownerReturn)
    {
        var merged = ownerParameters
            .OrderBy(parameter => parameter.Ordinal)
            .ToList();
        if (routingPlan is null)
            return merged;

        foreach (var alias in routingPlan.GetGlobalAliases(ownerSymbol, memberName)
            .Where(route => route.Route == DeclarationRouteKind.GlobalFunction))
        {
            var declaration = alias.Declaration;
            var aliasParameters = declaration.Parameters
                .OrderBy(parameter => parameter.Ordinal)
                .ToList();
            if (declaration.TypeParameters.Count > 0
                || aliasParameters.Count != merged.Count)
            {
                continue;
            }

            try
            {
                var aliasReturn = typeResolver.Project(
                    declaration.ReturnType,
                    $"{alias.Symbol.Name}/decl[{declaration.Ordinal}]/globalFunction/return");
                if (!string.Equals(
                        ownerReturn.CanonicalType,
                        aliasReturn.CanonicalType,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var compatible = true;
                for (var index = 0; index < merged.Count; index++)
                {
                    if (merged[index].Type is null
                        || aliasParameters[index].Type is null)
                    {
                        compatible = false;
                        break;
                    }
                    var ownerProjection = typeResolver.Project(
                        merged[index].Type,
                        $"{ownerSymbol}/{memberName}/parameter[{index}]");
                    var aliasProjection = typeResolver.Project(
                        aliasParameters[index].Type,
                        $"{alias.Symbol.Name}/decl[{declaration.Ordinal}]/" +
                        $"globalFunction/parameter[{aliasParameters[index].Ordinal}]");
                    if (!string.Equals(
                            ownerProjection.CanonicalType,
                            aliasProjection.CanonicalType,
                            StringComparison.Ordinal)
                        || merged[index].Rest != aliasParameters[index].Rest)
                    {
                        compatible = false;
                        break;
                    }
                }

                if (!compatible)
                    continue;

                for (var index = 0; index < merged.Count; index++)
                {
                    merged[index] = merged[index] with
                    {
                        Optional = merged[index].Optional
                            || aliasParameters[index].Optional,
                    };
                }
            }
            catch (TypeProjectionException)
            {
                // The supplemental route records the precise failure.
            }
        }

        return merged;
    }

    private static string FormatParameterType(TypeProjection projection)
        => projection.RenderedType;

    private static string AppendReason(string? existing, string addition)
        => string.IsNullOrWhiteSpace(existing) ? addition : $"{existing} {addition}";

    private bool IsGloballyMutable(
        string ownerSymbol,
        string memberName,
        TypeProjection ownerProjection)
    {
        if (routingPlan is null
            || memberName.StartsWith("on", StringComparison.Ordinal))
        {
            return false;
        }

        var mutable = false;
        foreach (var alias in routingPlan.GetGlobalAliases(ownerSymbol, memberName)
            .Where(route => route.Route == DeclarationRouteKind.GlobalVariable))
        {
            var declaration = alias.Declaration;
            if (declaration.Type is null
                || declaration.Type is KeywordTypeNode
                {
                    Name: "VoidKeyword",
                })
            {
                continue;
            }

            var aliasProjection = typeResolver.Project(
                declaration.Type,
                $"{alias.Symbol.Name}/decl[{declaration.Ordinal}]/globalVariable");
            if (!string.Equals(
                    aliasProjection.CanonicalType,
                    ownerProjection.CanonicalType,
                    StringComparison.Ordinal))
            {
                throw new TypeProjectionException(
                    $"Global variable '{alias.Symbol.Name}' collides with " +
                    $"'{ownerSymbol}.{memberName}' using incompatible canonical " +
                    $"types '{aliasProjection.CanonicalType}' and " +
                    $"'{ownerProjection.CanonicalType}'.",
                    $"{alias.Symbol.Name}/decl[{declaration.Ordinal}]/globalVariable");
            }

            mutable |= declaration.VariableKind is "var" or "let";
        }

        return mutable;
    }

    private (string Text, bool Deprecated) MergeGlobalDocumentation(
        string ownerSymbol,
        string memberName,
        DocumentationModel documentation,
        DeclarationRouteKind routeKind)
    {
        if (routingPlan is null)
            return (documentation.Text, documentation.Deprecated);

        var aliases = routingPlan.GetGlobalAliases(ownerSymbol, memberName)
            .Where(route => route.Route == routeKind)
            .Where(route =>
                routeKind != DeclarationRouteKind.GlobalVariable
                || (!memberName.StartsWith("on", StringComparison.Ordinal)
                    && route.Declaration.Type is not KeywordTypeNode
                    {
                        Name: "VoidKeyword",
                    }))
            .ToList();
        var texts = new[] { documentation.Text }
            .Concat(aliases.Select(alias =>
                alias.Declaration.Documentation.Text))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return (
            string.Join("\n\n", texts),
            documentation.Deprecated
                || aliases.Any(alias =>
                    alias.Declaration.Documentation.Deprecated));
    }

    /// <summary>
    /// Returns true if the method is an event-subscription overload that may be deferred:
    /// must be named addEventListener or removeEventListener, and all type parameters
    /// must be constrained by keyof &lt;SomeEventMap&gt;.
    /// </summary>
    private static bool IsEventSubscriptionOverload(MemberModel method)
    {
        var name = method.Name?.Text;
        if (name is not ("addEventListener" or "removeEventListener"))
            return false;
        return method.TypeParameters.All(tp => IsKeyofEventMapConstraint(tp.Constraint));
    }

    private static bool IsKeyofEventMapConstraint(TypeNode? constraint)
        => constraint is OperatorTypeNode op &&
           (op.Operator is "keyof" or "KeyOfKeyword") &&
           op.OperandType is ReferenceTypeNode rf &&
           rf.Name.EndsWith("EventMap", StringComparison.Ordinal);

    /// <summary>
    /// Checks if a type node is <c>boolean | EventListenerOptions</c> or
    /// <c>boolean | AddEventListenerOptions</c> (order-independent, with or without null arms).
    /// If so, returns the options type name.
    /// </summary>
    private static bool TryGetBoolOptionsUnion(TypeNode? paramType, out string optionsTypeName)
    {
        optionsTypeName = "";

        if (paramType is ParenthesizedTypeNode paren)
            paramType = paren.InnerType;

        if (paramType is not UnionTypeNode union)
            return false;

        var nonNull = union.Types.Where(t =>
            !(t is KeywordTypeNode kw &&
              (kw.Name is "NullKeyword" or "UndefinedKeyword" ||
               kw.CheckerType is "null" or "undefined")) &&
            !(t is LiteralTypeNode lit &&
              lit.LiteralKind is "NullLiteral" or "NullKeyword" or "UndefinedKeyword"))
            .ToList();

        if (nonNull.Count != 2) return false;

        var boolArm = nonNull.FirstOrDefault(t =>
            t is KeywordTypeNode bkw &&
            (bkw.Name is "BooleanKeyword" or "boolean" || bkw.CheckerType is "boolean"));
        var optionsArm = nonNull.FirstOrDefault(t =>
            t is ReferenceTypeNode rf &&
            rf.Name is "EventListenerOptions" or "AddEventListenerOptions");

        if (boolArm is null || optionsArm is not ReferenceTypeNode optRef)
            return false;

        optionsTypeName = optRef.Name;
        return true;
    }
}
