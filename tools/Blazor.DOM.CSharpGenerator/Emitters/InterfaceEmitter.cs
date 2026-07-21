// Interface emitter: projects TypeScript interfaces matched to WebIDL "interface" or
// "mixin" classification into C# partial interface contracts.
// - Preserves inheritance chain from TS heritage
// - Properties -> C# properties
// - Methods -> C# method signatures (bool|options params expand to three overloads)
// - Event-subscription generic overloads (addEventListener/removeEventListener<K extends keyof EventMap>):
//   deferred to the event-subscription phase with an honest DEFERRED comment.
// - Other generic methods: preserve lexical parameters, constraints, and CLR arity.
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
    DeclarationRoutingPlan? routingPlan = null,
    EventTargetAssociationResolver? eventAssociations = null)
{
    private static readonly IReadOnlySet<string> EmittedDeclarationKinds =
        new HashSet<string>(["interface"], StringComparer.Ordinal);

    private sealed record MethodRef(MemberModel Member, int DeclarationOrdinal);
    private sealed record MethodSig(
        string Rendered,
        string CanonicalKey,
        string ReturnType,
        string CanonicalConstraints,
        int DeclarationOrdinal,
        int OptionalParamCount = 0,
        bool HasRestParameter = false,
        bool IsDefaultExpansion = false,
        string JavaScriptName = "",
        string SourceIdentity = "",
        string ReturnTransport = "",
        TypeProjection? ReturnProjection = null,
        IReadOnlyList<TypeNode?>? ReturnSources = null,
        GenericScope? Scope = null);
    private sealed record MethodBuildResult(
        IReadOnlyList<MethodSig> Outputs,
        IReadOnlyList<MemberOutcome> Outcomes,
        string? DeferredOutput = null);
    private sealed record AccessorBuildResult(
        string? Property,
        string? GetterMethod,
        string? SetterMethod,
        string? PropertyMemberName,
        string? GetterMemberName,
        string? SetterMemberName);
    private sealed record AccessorEmissionPlan(
        ReconciledAccessor Accessor,
        bool GetterAsMethod);
    private sealed record InheritedContract(
        IReadOnlyList<ReconciledAccessor> Accessors,
        IReadOnlyDictionary<string, string> MemberNames);
    private readonly EventTargetAssociationResolver _eventAssociations =
        eventAssociations ?? new EventTargetAssociationResolver(typeResolver);

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
        catch (GenericDeferralException)
        {
            throw;
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
        var eventAssociation = _eventAssociations.Resolve(symbol);

        var csName = Naming.ToCSharpSimpleTypeName(symbol.Name);
        var generic = typeResolver.CreateGenericDeclaration(
            symbol,
            symbol.Name);
        string baseClause;
        try
        {
            baseClause = BuildBaseClause(
                allDecls,
                typeResolver,
                symbol.Name,
                generic.Scope);
        }
        catch (GenericDeferralException)
        {
            throw;
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
        var emittedIndexSignatures = new HashSet<string>(StringComparer.Ordinal);
        var literalDispatchMethods = FindLiteralDispatchMethods(allDecls);

        var reconciledAccessors = new AccessorReconciler(typeResolver).Reconcile(
            symbol.Name,
            allDecls,
            generic.Scope,
            (memberName, projection) =>
                IsGloballyMutable(symbol.Name, memberName, projection));
        memberOutcomes.AddRange(reconciledAccessors.DeferredOutcomes);
        propertyOutputs.AddRange(reconciledAccessors.DeferredOutputs);

        var inherited = CollectInheritedContract(
            allDecls,
            generic.Scope,
            new HashSet<string>(StringComparer.Ordinal) { symbol.Name });
        var reservedAccessorNames = inherited.MemberNames.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        foreach (var accessor in reconciledAccessors.Accessors)
        {
            var plan = ReconcileInheritedAccessor(
                symbol.Name,
                accessor,
                inherited.Accessors);
            var build = BuildAccessor(
                plan.Accessor,
                symbol.Name,
                plan.GetterAsMethod);
            ReserveAccessorName(
                reservedAccessorNames,
                build.PropertyMemberName,
                accessor.Sources[0].Provenance,
                symbol.Name);
            ReserveAccessorName(
                reservedAccessorNames,
                build.GetterMemberName,
                accessor.Sources[0].Provenance,
                symbol.Name);
            ReserveAccessorName(
                reservedAccessorNames,
                build.SetterMemberName,
                accessor.Sources[0].Provenance,
                symbol.Name);

            if (build.Property is not null
                && emittedPropertyKeys.Add(
                    $"prop:{accessor.IsStatic}:{accessor.JavaScriptName}"))
            {
                propertyOutputs.Add(build.Property);
            }
            if (build.GetterMethod is not null)
                methodOutputs.Add(build.GetterMethod);
            if (build.SetterMethod is not null)
                methodOutputs.Add(build.SetterMethod);

            foreach (var source in accessor.Sources)
            {
                var read = accessor.Getter?.Sources.Contains(source) == true;
                var write = accessor.Setter?.Sources.Contains(source) == true;
                var directions = (read, write) switch
                {
                    (true, true) => "get/set",
                    (true, false) => "get",
                    (false, true) => "set",
                    _ => "none",
                };
                memberOutcomes.Add(new MemberOutcome(
                    source.Member.Ordinal,
                    source.Name,
                    source.Member.Kind,
                    MemberOutcomeStatus.Projected,
                    null,
                    $"Reconciled into canonical '{accessor.JavaScriptName}' " +
                    $"{directions} accessor from " +
                    $"{accessor.Getter?.Sources.Count ?? 0} getter source(s) and " +
                    $"{accessor.Setter?.Sources.Count ?? 0} setter source(s).",
                    source.DeclarationOrdinal,
                    Provenance: source.Provenance));
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
                        methodRef.DeclarationOrdinal,
                        generic.Scope,
                        literalDispatchMethods.Contains(
                            (methodRef.DeclarationOrdinal, methodRef.Member.Ordinal)));
                    foreach (var signature in build.Outputs)
                    {
                        var emittedName = MethodName(signature.CanonicalKey);
                        if (reservedAccessorNames.TryGetValue(
                                emittedName,
                                out var accessorProvenance))
                        {
                            throw new TypeProjectionException(
                                $"Method '{symbol.Name}.{methodRef.Member.Name?.Text}' " +
                                $"normalizes to C# member '{emittedName}', which collides " +
                                $"with accessor source '{accessorProvenance}'.",
                                $"{symbol.Name}/decl[{methodRef.DeclarationOrdinal}]/" +
                                $"member[{methodRef.Member.Ordinal}]/method/" +
                                $"{methodRef.Member.Name?.Text}");
                        }
                    }
                    var outcomes = build.Outcomes.ToList();
                    var stagedOutputs = methodOutputs.ToList();
                    var stagedMethodKeys = new Dictionary<string, MethodSig>(
                        emittedMethodKeys,
                        StringComparer.Ordinal);
                    var stagedOutputIndices = new Dictionary<string, int>(
                        emittedMethodOutputIndices,
                        StringComparer.Ordinal);
                    var dedupedFromDecl = new HashSet<int>();
                    foreach (var sig in build.Outputs)
                    {
                        if (stagedMethodKeys.TryGetValue(sig.CanonicalKey, out var existing))
                        {
                            if (!string.Equals(
                                    existing.JavaScriptName,
                                    sig.JavaScriptName,
                                    StringComparison.Ordinal))
                            {
                                throw new TypeProjectionException(
                                    $"JavaScript operations '{existing.JavaScriptName}' and " +
                                    $"'{sig.JavaScriptName}' on '{symbol.Name}' normalize to " +
                                    $"the same CLR signature '{sig.CanonicalKey}'.",
                                    $"{symbol.Name}/decl[{methodRef.DeclarationOrdinal}]/" +
                                    $"{sig.JavaScriptName}/normalization");
                            }
                            if (!string.Equals(existing.ReturnType, sig.ReturnType, StringComparison.Ordinal))
                            {
                                if (!string.Equals(
                                        existing.CanonicalConstraints,
                                        sig.CanonicalConstraints,
                                        StringComparison.Ordinal))
                                {
                                    throw new TypeProjectionException(
                                        $"Method '{symbol.Name}.{sig.JavaScriptName}' cannot " +
                                        "reconcile return-only overloads with incompatible " +
                                        "generic constraints.",
                                        $"{symbol.Name}/decl[{methodRef.DeclarationOrdinal}]/" +
                                        $"{sig.JavaScriptName}/constraints");
                                }
                                var returnSources = (existing.ReturnSources ?? [])
                                    .Concat(sig.ReturnSources ?? [])
                                    .ToList();
                                TypeProjection unionReturn;
                                try
                                {
                                    unionReturn = typeResolver.ProjectOverloadReturnUnion(
                                        returnSources,
                                        $"{symbol.Name}/decl[{methodRef.DeclarationOrdinal}]/" +
                                        $"{sig.JavaScriptName}/clr-collision",
                                        sig.Scope);
                                }
                                catch (TypeProjectionException exception)
                                {
                                    throw new TypeProjectionException(
                                        $"Method '{symbol.Name}.{sig.JavaScriptName}' in " +
                                        $"decl[{methodRef.DeclarationOrdinal}] collides with " +
                                        $"decl[{existing.DeclarationOrdinal}] for CLR signature " +
                                        $"'{sig.CanonicalKey}' and its return-only overloads " +
                                        $"cannot form a typed union: {exception.Message}",
                                        exception.Provenance);
                                }
                                var reconciled = ReplaceReturnType(
                                    existing,
                                    unionReturn,
                                    returnSources);
                                if (stagedOutputIndices.TryGetValue(
                                        sig.CanonicalKey,
                                        out var unionOutputIndex))
                                {
                                    stagedOutputs[unionOutputIndex] =
                                        reconciled.Rendered;
                                }
                                stagedMethodKeys[sig.CanonicalKey] = reconciled;
                                dedupedFromDecl.Add(existing.DeclarationOrdinal);
                                continue;
                            }
                            if (!string.Equals(
                                    existing.ReturnTransport,
                                    sig.ReturnTransport,
                                    StringComparison.Ordinal))
                            {
                                throw new TypeProjectionException(
                                    $"Method '{symbol.Name}.{sig.JavaScriptName}' has CLR-" +
                                    $"identical return type '{sig.ReturnType}' but incompatible " +
                                    $"return transports '{existing.ReturnTransport}' and " +
                                    $"'{sig.ReturnTransport}'.",
                                    $"{symbol.Name}/decl[{methodRef.DeclarationOrdinal}]/" +
                                    $"{sig.JavaScriptName}/return/transport");
                            }
                            if (!string.Equals(
                                    existing.CanonicalConstraints,
                                    sig.CanonicalConstraints,
                                    StringComparison.Ordinal))
                            {
                                var methodName = methodRef.Member.Name?.Text ?? "";
                                if (existing.IsDefaultExpansion || sig.IsDefaultExpansion)
                                {
                                    throw new GenericDeferralException(
                                        $"Default-expanded generic method " +
                                        $"'{symbol.Name}.{methodName}' collides with " +
                                        "incompatible generic constraints.",
                                        $"{symbol.Name}/decl[{methodRef.DeclarationOrdinal}]/" +
                                        $"{methodName}/generic-default",
                                        "generic-method-defaults");
                                }
                                throw new TypeProjectionException(
                                    $"Method '{symbol.Name}.{methodName}' in decl[{methodRef.DeclarationOrdinal}] collides with decl[{existing.DeclarationOrdinal}] " +
                                    $"for canonical signature '{sig.CanonicalKey}' but has incompatible generic constraints.",
                                    $"{symbol.Name}/decl[{methodRef.DeclarationOrdinal}]/{methodName}/constraints");
                            }

                            if ((sig.HasRestParameter && !existing.HasRestParameter
                                    || sig.HasRestParameter == existing.HasRestParameter
                                    && sig.OptionalParamCount > existing.OptionalParamCount)
                                && stagedOutputIndices.TryGetValue(sig.CanonicalKey, out var outputIndex))
                            {
                                stagedOutputs[outputIndex] = sig.Rendered;
                                stagedMethodKeys[sig.CanonicalKey] = sig;
                            }

                            dedupedFromDecl.Add(existing.DeclarationOrdinal);
                            continue;
                        }

                        stagedMethodKeys.Add(sig.CanonicalKey, sig);
                        stagedOutputIndices[sig.CanonicalKey] = stagedOutputs.Count;
                        stagedOutputs.Add(sig.Rendered);
                    }

                    if (dedupedFromDecl.Count > 0)
                    {
                        var reason = $"Deduplicated from declaration ordinal {dedupedFromDecl.Min()}.";
                        outcomes = outcomes
                            .Select(o => o with { Reason = AppendReason(o.Reason, reason) })
                            .ToList();
                    }

                    methodOutputs.Clear();
                    methodOutputs.AddRange(stagedOutputs);
                    emittedMethodKeys.Clear();
                    foreach (var pair in stagedMethodKeys)
                        emittedMethodKeys.Add(pair.Key, pair.Value);
                    emittedMethodOutputIndices.Clear();
                    foreach (var pair in stagedOutputIndices)
                        emittedMethodOutputIndices.Add(pair.Key, pair.Value);
                    if (build.DeferredOutput is not null
                        && emittedDeferredMethodOutputs.Add(build.DeferredOutput))
                    {
                        methodOutputs.Add(build.DeferredOutput);
                    }
                    memberOutcomes.AddRange(outcomes);
                }
                catch (GenericDeferralException exception)
                {
                    memberOutcomes.Add(new MemberOutcome(
                        methodRef.Member.Ordinal,
                        methodRef.Member.Name?.Text ?? "",
                        methodRef.Member.Kind,
                        MemberOutcomeStatus.Deferred,
                        exception.Phase,
                        exception.Message,
                        methodRef.DeclarationOrdinal));
                    methodOutputs.Add(
                        $"// DEFERRED ({exception.Phase}): " +
                        $"{methodRef.Member.Name?.Text} — {exception.Message}");
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
            {
                try
                {
                    var indexAccessor = BuildIndexAccessor(
                        m,
                        symbol.Name,
                        decl.Ordinal,
                        generic.Scope);
                    if (emittedIndexSignatures.Add(indexAccessor.CanonicalKey))
                        propertyOutputs.Add(indexAccessor.Rendered);
                    memberOutcomes.Add(new MemberOutcome(
                        m.Ordinal,
                        m.Name?.Text ?? "indexSignature",
                        "indexSignature",
                        MemberOutcomeStatus.Projected,
                        null,
                        "Emitted explicit typed indexed get/set operations.",
                        decl.Ordinal));
                }
                catch (GenericDeferralException)
                {
                    throw;
                }
                catch (TypeProjectionException exception)
                {
                    memberFailures.Add(exception);
                    memberOutcomes.Add(new MemberOutcome(
                        m.Ordinal,
                        m.Name?.Text ?? "indexSignature",
                        "indexSignature",
                        MemberOutcomeStatus.Failed,
                        null,
                        exception.Message,
                        decl.Ordinal));
                }
            }

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
        foreach (var defaultNote in generic.DefaultNotes)
            w.AppendLine($"// TypeScript generic default: {defaultNote}.");

        var typeName = $"I{csName}{generic.TypeParameterList}";
        var contractBases = new List<string>();
        if (!string.IsNullOrEmpty(baseClause))
            contractBases.Add(baseClause);
        contractBases.Add(eventAssociation is null
            ? "global::Microsoft.JSInterop.IDomProxy"
            : "global::Microsoft.JSInterop.IDomEventTargetProxy");
        if (eventAssociation is not null)
        {
            w.AppendLine(
                "[global::Microsoft.JSInterop.DomEventTarget(" +
                $"\"{EscapeCSharp(symbol.Name)}\", " +
                $"{string.Join(", ", eventAssociation.EventMaps.Select(map =>
                    $"\"{EscapeCSharp(map)}\""))})]");
        }
        var header =
            $"public partial interface {typeName} : " +
            $"{string.Join(", ", contractBases)}{generic.ConstraintSuffix}";

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

    private InheritedContract CollectInheritedContract(
        IReadOnlyList<DeclarationModel> declarations,
        GenericScope derivedScope,
        HashSet<string> visited)
    {
        var accessors = new List<ReconciledAccessor>();
        var memberNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var heritage in declarations
            .SelectMany(declaration => declaration.Heritage)
            .Where(clause => clause.Token == "extends")
            .SelectMany(clause => clause.Types)
            .OfType<HeritageReferenceTypeNode>())
        {
            var baseName = heritage.ResolvedSymbol ?? heritage.Expression;
            if (!visited.Add(baseName)
                || !typeResolver.TryGetSymbol(baseName, out var baseSymbol)
                || !typeResolver.IsInterfaceOrMixin(baseName))
            {
                continue;
            }

            var baseDeclarations = baseSymbol.Declarations
                .Where(declaration => declaration.Kind == "interface")
                .OrderBy(declaration => declaration.Ordinal)
                .ToList();
            var baseGeneric = typeResolver.CreateGenericDeclaration(
                baseSymbol,
                $"{baseName}/inherited-accessors");
            var baseScope = baseGeneric.Scope;
            if (heritage.TypeArguments.Count > 0)
            {
                var substitutions = heritage.TypeArguments
                    .Select((argument, index) => typeResolver.Project(
                        argument,
                        $"{baseName}/inherited-accessors/typeArgument[{index}]",
                        derivedScope))
                    .ToList();
                baseScope = baseScope.WithSubstitutions(substitutions);
            }

            AccessorReconciliationResult reconciled;
            try
            {
                reconciled = new AccessorReconciler(typeResolver).Reconcile(
                    baseName,
                    baseDeclarations,
                    baseScope,
                    (memberName, projection) =>
                        IsGloballyMutable(baseName, memberName, projection));
            }
            catch (TypeProjectionException)
            {
                continue;
            }
            foreach (var accessor in reconciled.Accessors)
            {
                accessors.Add(accessor);
                var hasExplicitAccessor = accessor.Sources.Any(source =>
                    source.Member.Kind is "getter" or "setter");
                if (hasExplicitAccessor && accessor.Getter is not null)
                {
                    AddInheritedMemberName(
                        memberNames,
                        accessor.CSharpName,
                        accessor.Sources[0].Provenance);
                }
                if (hasExplicitAccessor
                    && accessor.Setter is not null
                    && !accessor.IsSymmetric)
                {
                    AddInheritedMemberName(
                        memberNames,
                        $"Set{accessor.CSharpName}",
                        accessor.Sources[0].Provenance);
                }
            }

            var ancestors = CollectInheritedContract(
                baseDeclarations,
                baseScope,
                visited);
            accessors.AddRange(ancestors.Accessors);
            foreach (var pair in ancestors.MemberNames)
                AddInheritedMemberName(memberNames, pair.Key, pair.Value);
        }

        return new InheritedContract(accessors, memberNames);
    }

    private static AccessorEmissionPlan ReconcileInheritedAccessor(
        string symbolName,
        ReconciledAccessor accessor,
        IReadOnlyList<ReconciledAccessor> inheritedAccessors)
    {
        if (accessor.Sources.All(source => source.Member.Kind == "property"))
            return new AccessorEmissionPlan(accessor, GetterAsMethod: false);

        var inherited = inheritedAccessors.FirstOrDefault(candidate =>
            candidate.IsStatic == accessor.IsStatic
            && string.Equals(
                candidate.CSharpName,
                accessor.CSharpName,
                StringComparison.Ordinal));
        if (inherited is null)
            return new AccessorEmissionPlan(accessor, GetterAsMethod: false);

        if (!string.Equals(
                inherited.JavaScriptName,
                accessor.JavaScriptName,
                StringComparison.Ordinal))
        {
            throw new TypeProjectionException(
                $"Accessor '{symbolName}.{accessor.JavaScriptName}' normalizes to " +
                $"inherited C# member '{accessor.CSharpName}' from JavaScript property " +
                $"'{inherited.JavaScriptName}'. Sources: " +
                $"{inherited.Sources[0].Provenance}; " +
                $"{accessor.Sources[0].Provenance}.",
                accessor.Sources[0].Provenance);
        }

        var getterAsMethod = accessor.Getter is not null
            && inherited.Getter is not null
            && !accessor.Getter.Identity.StructurallyEquals(
                inherited.Getter.Identity);
        var getter = inherited.Getter is not null
            && !getterAsMethod
            ? null
            : accessor.Getter;
        var setter = inherited.Setter is not null
            && accessor.Setter is not null
            && accessor.Setter.Identity.StructurallyEquals(
                inherited.Setter.Identity)
                ? null
                : accessor.Setter;
        return new AccessorEmissionPlan(accessor with
        {
            Getter = getter,
            Setter = setter,
        }, getterAsMethod);
    }

    private static void AddInheritedMemberName(
        Dictionary<string, string> names,
        string memberName,
        string provenance)
    {
        names.TryAdd(memberName, provenance);
    }

    private AccessorBuildResult BuildAccessor(
        ReconciledAccessor accessor,
        string symbolName,
        bool getterAsMethod)
    {
        var globalDocumentation = MergeGlobalDocumentation(
            symbolName,
            accessor.JavaScriptName,
            new DocumentationModel(
                accessor.Documentation,
                [],
                accessor.Deprecated),
            DeclarationRouteKind.GlobalVariable);
        var getter = accessor.Getter;
        var setter = accessor.Setter;
        var symmetric = accessor.IsSymmetric;
        string? property = null;
        string? getterMethod = null;
        string? setterMethod = null;
        string? propertyMemberName = null;
        string? getterMemberName = null;
        string? setterMemberName = null;
        var modifier = accessor.IsStatic ? "static abstract " : "";

        if (getter is not null && !getterAsMethod)
        {
            var writer = new CSharpWriter();
            writer.XmlDoc(
                globalDocumentation.Text,
                globalDocumentation.Deprecated);
            writer.AppendLine(RenderAccessorMetadata(
                accessor.JavaScriptName,
                getter));
            if (symmetric)
            {
                writer.AppendLine(RenderAccessorMetadata(
                    accessor.JavaScriptName,
                    setter!));
            }
            writer.AppendLine(
                $"{modifier}{getter.Projection.RenderedType} " +
                $"{accessor.CSharpName} " +
                (symmetric ? "{ get; set; }" : "{ get; }"));
            property = writer.ToString().TrimEnd();
            propertyMemberName = accessor.CSharpName;
        }

        if (getter is not null && getterAsMethod)
        {
            var writer = new CSharpWriter();
            writer.AppendLine(
                "/// <summary>Gets the JavaScript property " +
                $"<c>{EscapeXml(accessor.JavaScriptName)}</c> using its exact " +
                "TypeScript getter type.</summary>");
            writer.AppendLine(RenderAccessorMetadata(
                accessor.JavaScriptName,
                getter));
            getterMemberName = $"Get{accessor.CSharpName}";
            writer.AppendLine(
                $"{modifier}{getter.Projection.RenderedType} " +
                $"{getterMemberName}();");
            getterMethod = writer.ToString().TrimEnd();
        }

        if (setter is not null && (!symmetric || getterAsMethod))
        {
            var writer = new CSharpWriter();
            writer.AppendLine(
                "/// <summary>Sets the JavaScript property " +
                $"<c>{EscapeXml(accessor.JavaScriptName)}</c> using its exact " +
                "TypeScript setter type.</summary>");
            writer.AppendLine(
                "/// <param name=\"value\">The value assigned to the " +
                "JavaScript property.</param>");
            writer.AppendLine(RenderAccessorMetadata(
                accessor.JavaScriptName,
                setter));
            setterMemberName = $"Set{accessor.CSharpName}";
            writer.AppendLine(
                $"{modifier}void {setterMemberName}" +
                $"({setter.Projection.RenderedType} value);");
            setterMethod = writer.ToString().TrimEnd();
        }

        return new AccessorBuildResult(
            property,
            getterMethod,
            setterMethod,
            propertyMemberName,
            getterMemberName,
            setterMemberName);
    }

    private static string RenderAccessorMetadata(
        string javaScriptName,
        AccessorEndpoint endpoint)
    {
        var transport = endpoint.Projection.Transport;
        var transportKind = transport?.Kind switch
        {
            "json-value" => "JsonValue",
            "js-reference" => "JsReference",
            "js-stream" => "JsStream",
            "binary" => "Binary",
            "transferable" => "Transferable",
            "runtime-inferred" => "Inferred",
            _ => "Unsupported",
        };
        var operation = endpoint.Direction == AccessorDirection.Get
            ? "Get"
            : "Set";
        var sourceType = transport?.SourceType
            ?? endpoint.SourceType.CheckerType
            ?? endpoint.SourceType.SyntaxKind
            ?? endpoint.Projection.CSharpType;
        var namedArguments = new List<string>
        {
            $"Nullable = {(endpoint.Identity.IsNullable || transport?.Nullable == true).ToString().ToLowerInvariant()}",
            $"Streamable = {(transport?.Streamable == true).ToString().ToLowerInvariant()}",
            $"StructuredClone = {(transport?.StructuredClone == true).ToString().ToLowerInvariant()}",
        };
        var unsupportedReason = transport?.Reason
            ?? (transport is null
                ? "Source IR does not provide reviewed transport metadata."
                : null);
        if (unsupportedReason is not null)
        {
            namedArguments.Add(
                $"UnsupportedReason = \"{EscapeCSharp(unsupportedReason)}\"");
        }

        return
            $"[global::Microsoft.JSInterop.DomAccessor(" +
            $"\"{EscapeCSharp(javaScriptName)}\", " +
            $"global::Microsoft.JSInterop.DomAccessorOperation.{operation}, " +
            $"global::Microsoft.JSInterop.DomTransportKind.{transportKind}, " +
            $"\"{EscapeCSharp(sourceType)}\", " +
            $"{string.Join(", ", namedArguments)})]";
    }

    private static void ReserveAccessorName(
        Dictionary<string, string> reservations,
        string? memberName,
        string provenance,
        string symbolName)
    {
        if (memberName is null)
            return;
        if (reservations.TryGetValue(memberName, out var existing))
        {
            throw new TypeProjectionException(
                $"Accessor lowering on '{symbolName}' produces duplicate C# member " +
                $"name '{memberName}' from '{existing}' and '{provenance}'.",
                provenance);
        }
        reservations.Add(memberName, provenance);
    }

    private static string MethodName(string canonicalKey)
    {
        var generic = canonicalKey.IndexOf('`');
        var parameters = canonicalKey.IndexOf('(');
        var end = generic >= 0 && (parameters < 0 || generic < parameters)
            ? generic
            : parameters;
        return end < 0 ? canonicalKey : canonicalKey[..end];
    }

    private static string EscapeCSharp(string value)
        => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string EscapeXml(string value)
        => value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private MethodBuildResult BuildMethod(
        MemberModel method,
        string symbolName,
        int declOrdinal,
        GenericScope declarationScope,
        bool preserveLiteralParameters = false)
    {
        var memberName = method.Name?.Text;
        if (memberName is null)
            return new MethodBuildResult([], []);

        var provenance = $"{symbolName}/decl[{declOrdinal}]/{memberName}";

        if (method.TypeParameters.Count > 0 && IsEventSubscriptionOverload(method))
        {
            return new MethodBuildResult(
                [],
                [
                    new MemberOutcome(
                        method.Ordinal,
                        memberName,
                        method.Kind,
                        MemberOutcomeStatus.Projected,
                        null,
                        "Mapped to the target's DomEventDescriptor<TEvent> subscription contract.",
                        declOrdinal)
                ]);
        }

        GenericDeclaration methodGeneric;
        try
        {
            methodGeneric = typeResolver.CreateGenericDeclaration(
                method.TypeParameters,
                provenance,
                declarationScope,
                canonicalPrefix: "!!");
        }
        catch (GenericDeferralException exception)
        {
            return new MethodBuildResult(
                [],
                [
                    new MemberOutcome(
                        method.Ordinal,
                        memberName,
                        method.Kind,
                        MemberOutcomeStatus.Deferred,
                        exception.Phase,
                        exception.Message,
                        declOrdinal)
                ],
                $"// DEFERRED ({exception.Phase}): {memberName} — {exception.Message}");
        }

        IReadOnlyList<GenericDeclaration> defaultExpansions;
        try
        {
            defaultExpansions = typeResolver.CreateDefaultExpandedDeclarations(
                method.TypeParameters,
                provenance,
                declarationScope,
                canonicalPrefix: "!!");
        }
        catch (GenericDeferralException exception)
        {
            return new MethodBuildResult(
                [],
                [
                    new MemberOutcome(
                        method.Ordinal,
                        memberName,
                        method.Kind,
                        MemberOutcomeStatus.Deferred,
                        exception.Phase,
                        exception.Message,
                        declOrdinal)
                ],
                $"// DEFERRED ({exception.Phase}): {memberName} — {exception.Message}");
        }

        var outputs = new List<MethodSig>();
        outputs.AddRange(BuildMethodVariant(
            method,
            symbolName,
            declOrdinal,
            provenance,
            methodGeneric,
            isDefaultExpansion: false,
            preserveLiteralParameters));
        foreach (var expansion in defaultExpansions)
        {
            try
            {
                outputs.AddRange(BuildMethodVariant(
                    method,
                    symbolName,
                    declOrdinal,
                    provenance,
                    expansion,
                    isDefaultExpansion: true,
                    preserveLiteralParameters));
            }
            catch (GenericDeferralException)
            {
                throw;
            }
            catch (TypeProjectionException exception)
            {
                throw new GenericDeferralException(
                    $"Default-expanded generic method '{symbolName}.{memberName}' " +
                    $"cannot be emitted: {exception.Message}",
                    exception.Provenance,
                    "generic-method-defaults");
            }
        }
        return new MethodBuildResult(
            outputs,
            [
                new MemberOutcome(
                    method.Ordinal,
                    memberName,
                    method.Kind,
                    MemberOutcomeStatus.Projected,
                    null,
                    defaultExpansions.Count == 0
                        ? null
                        : $"Emitted {defaultExpansions.Count} default-expanded overload(s).",
                    declOrdinal)
            ]);
    }

    private IReadOnlyList<MethodSig> BuildMethodVariant(
        MemberModel method,
        string symbolName,
        int declOrdinal,
        string provenance,
        GenericDeclaration methodGeneric,
        bool isDefaultExpansion,
        bool preserveLiteralParameters)
    {
        var memberName = method.Name?.Text
            ?? throw new InvalidOperationException("Method name is required.");
        var returnProj = typeResolver.Project(
            method.ReturnType,
            isDefaultExpansion
                ? $"{provenance}/return/defaultExpansion"
                : $"{provenance}/return",
            methodGeneric.Scope);
        ValidateMethodReturn(returnProj, provenance, isDefaultExpansion);
        var (docText, deprecated) = MergeGlobalDocumentation(
            symbolName,
            memberName,
            method.Documentation,
            DeclarationRouteKind.GlobalFunction);
        var csBaseName = Naming.ToCSharpMemberName(memberName);
        var emittedName = returnProj.Identity.IsAwaitable
            ? $"{csBaseName}Async"
            : csBaseName;
        var keyDomain = method.TypeParameters
            .Select(parameter => parameter.Constraint)
            .OfType<OperatorTypeNode>()
            .FirstOrDefault(operation =>
                operation.Operator is "keyof" or "KeyOfKeyword")
            ?.OperandType as ReferenceTypeNode;
        if (keyDomain is not null)
        {
            emittedName += "For" +
                Naming.ToCSharpSimpleTypeName(
                    keyDomain.ResolvedSymbol ?? keyDomain.Name);
        }
        var csReturn = returnProj.RenderedType;
        var paramList = MergeGlobalParameterForms(
            symbolName,
            memberName,
            method.Parameters,
            returnProj,
            methodGeneric.Scope);
        var optionalParamCount = 0;

        for (var pi = 0; pi < paramList.Count; pi++)
        {
            if (!TryGetBoolOptionsUnion(paramList[pi].Type, out var optionsTypeName))
                continue;

            var noOptionsOverload = BuildMethodSignature(
                emittedName,
                returnProj,
                method.ReturnType,
                memberName,
                paramList,
                pi,
                null,
                null,
                null,
                pi,
                docText,
                deprecated,
                provenance,
                declOrdinal,
                methodGeneric,
                isDefaultExpansion,
                preserveLiteralParameters);
            var boolOverload = BuildMethodSignature(
                emittedName,
                returnProj,
                method.ReturnType,
                memberName,
                paramList,
                pi,
                "bool",
                "capture",
                "bool",
                -1,
                null,
                false,
                provenance,
                declOrdinal,
                methodGeneric,
                isDefaultExpansion,
                preserveLiteralParameters);
            var optOverload = BuildMethodSignature(
                emittedName,
                returnProj,
                method.ReturnType,
                memberName,
                paramList,
                pi,
                optionsTypeName + "?",
                paramList[pi].Name,
                optionsTypeName,
                -1,
                null,
                false,
                provenance,
                declOrdinal,
                methodGeneric,
                isDefaultExpansion,
                preserveLiteralParameters);

            return [noOptionsOverload, boolOverload, optOverload];
        }

        var parts = new List<string>();
        var canonicalParamTypes = new List<string>();
        var hasRestParameter = false;
        foreach (var p in paramList)
        {
            var parameterProvenance =
                ParameterProvenance(provenance, p, isDefaultExpansion);
            var pProj = preserveLiteralParameters
                && p.Type is LiteralTypeNode
                {
                    LiteralKind: "StringLiteral",
                } literal
                    ? typeResolver.ProjectLiteralStringParameter(
                        literal,
                        parameterProvenance)
                    : typeResolver.Project(
                        p.Type,
                        parameterProvenance,
                        methodGeneric.Scope);
            ValidateMethodParameter(
                pProj,
                p,
                provenance,
                isDefaultExpansion);
            var pType = FormatParameterType(pProj);
            var pName = Naming.ToCSharpParameterName(p.Name);

            if (p.Rest)
            {
                hasRestParameter = true;
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
        foreach (var defaultNote in methodGeneric.DefaultNotes)
            w.AppendLine($"// TypeScript generic default: {defaultNote}.");
        w.AppendLine(RenderOperationMetadata(
            BuildSourceIdentity(
                memberName,
                methodGeneric,
                paramList,
                canonicalParamTypes,
                returnProj),
            memberName,
            returnProj));
        if (!string.Equals(
                emittedName,
                Naming.ToCSharpMemberName(memberName),
                StringComparison.Ordinal))
        {
            w.AppendLine(
                $"[global::Microsoft.JSInterop.DomJavaScriptName(\"{EscapeCSharp(memberName)}\")]");
        }
        if (memberName is "Symbol.iterator" or "Symbol.asyncIterator")
        {
            var symbol = memberName == "Symbol.iterator"
                ? "Iterator"
                : "AsyncIterator";
            w.AppendLine(
                "[global::Microsoft.JSInterop.DomSymbol(" +
                $"global::Microsoft.JSInterop.DomWellKnownSymbol.{symbol})]");
        }
        w.AppendLine(
            $"{csReturn} {emittedName}{methodGeneric.TypeParameterList}(" +
            $"{string.Join(", ", parts)}){methodGeneric.ConstraintSuffix};");

        return
        [
            new MethodSig(
                w.ToString().TrimEnd(),
                CanonicalMethodKey(
                    emittedName,
                    methodGeneric.EmittedArity,
                    canonicalParamTypes),
                returnProj.CanonicalType,
                methodGeneric.CanonicalConstraints,
                declOrdinal,
                optionalParamCount,
                hasRestParameter,
                isDefaultExpansion,
                memberName,
                BuildSourceIdentity(
                    memberName,
                    methodGeneric,
                    paramList,
                    canonicalParamTypes,
                    returnProj),
                TransportIdentity(returnProj.Transport),
                returnProj,
                [method.ReturnType],
                methodGeneric.Scope)
        ];
    }

    private MethodSig BuildMethodSignature(
        string emittedName,
        TypeProjection returnProjection,
        TypeNode? returnSource,
        string javaScriptName,
        IReadOnlyList<ParameterModel> paramList,
        int substituteIndex,
        string? substituteType,
        string? substituteName,
        string? substituteCanonicalType,
        int dropFromIndex,
        string? docText,
        bool deprecated,
        string provenance,
        int declOrdinal,
        GenericDeclaration methodGeneric,
        bool isDefaultExpansion,
        bool preserveLiteralParameters)
    {
        var parts = new List<string>();
        var canonicalParamTypes = new List<string>();
        var optionalParamCount = 0;
        var hasRestParameter = false;
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
            var parameterProvenance =
                ParameterProvenance(provenance, p, isDefaultExpansion);
            var pProj = preserveLiteralParameters
                && p.Type is LiteralTypeNode
                {
                    LiteralKind: "StringLiteral",
                } literal
                    ? typeResolver.ProjectLiteralStringParameter(
                        literal,
                        parameterProvenance)
                    : typeResolver.Project(
                        p.Type,
                        parameterProvenance,
                        methodGeneric.Scope);
            ValidateMethodParameter(
                pProj,
                p,
                provenance,
                isDefaultExpansion);
            var pType = FormatParameterType(pProj);
            var pName = Naming.ToCSharpParameterName(p.Name);

            if (p.Rest)
            {
                hasRestParameter = true;
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
        foreach (var defaultNote in methodGeneric.DefaultNotes)
            w.AppendLine($"// TypeScript generic default: {defaultNote}.");
        w.AppendLine(RenderOperationMetadata(
            BuildSourceIdentity(
                javaScriptName,
                methodGeneric,
                paramList,
                canonicalParamTypes,
                returnProjection),
            javaScriptName,
            returnProjection));
        if (!string.Equals(
                emittedName,
                Naming.ToCSharpMemberName(javaScriptName),
                StringComparison.Ordinal))
        {
            w.AppendLine(
                $"[global::Microsoft.JSInterop.DomJavaScriptName(\"{EscapeCSharp(javaScriptName)}\")]");
        }
        w.AppendLine(
            $"{returnProjection.RenderedType} {emittedName}" +
            $"{methodGeneric.TypeParameterList}(" +
            $"{string.Join(", ", parts)}){methodGeneric.ConstraintSuffix};");

        return new MethodSig(
            w.ToString().TrimEnd(),
            CanonicalMethodKey(
                emittedName,
                methodGeneric.EmittedArity,
                canonicalParamTypes),
            returnProjection.CanonicalType,
            methodGeneric.CanonicalConstraints,
            declOrdinal,
            optionalParamCount,
            hasRestParameter,
            isDefaultExpansion,
            JavaScriptName: javaScriptName,
            SourceIdentity: BuildSourceIdentity(
                javaScriptName,
                methodGeneric,
                paramList,
                canonicalParamTypes,
                returnProjection),
            ReturnTransport: TransportIdentity(returnProjection.Transport),
            ReturnProjection: returnProjection,
            ReturnSources: [returnSource],
            Scope: methodGeneric.Scope);
    }

    private static string RenderOperationMetadata(
        string logicalIdentity,
        string javaScriptName,
        TypeProjection returnProjection)
    {
        var transport = returnProjection.Transport;
        var transportKind = transport?.Kind switch
        {
            "json-value" => "JsonValue",
            "js-reference" => "JsReference",
            "js-stream" => "JsStream",
            "binary" => "Binary",
            "transferable" => "Transferable",
            "runtime-inferred" => "Inferred",
            _ when returnProjection.Identity.Kind == ClrTypeKind.Void => "JsonValue",
            _ => "Unsupported",
        };
        var sourceType = transport?.SourceType
            ?? (returnProjection.Identity.Kind == ClrTypeKind.Void
                ? "void"
                : returnProjection.CSharpType);
        var namedArguments = new List<string>
        {
            $"Nullable = {(returnProjection.IsNullable || transport?.Nullable == true).ToString().ToLowerInvariant()}",
            $"Promise = {returnProjection.Identity.IsAwaitable.ToString().ToLowerInvariant()}",
            $"Streamable = {(transport?.Streamable == true).ToString().ToLowerInvariant()}",
            $"StructuredClone = {(transport?.StructuredClone == true).ToString().ToLowerInvariant()}",
        };
        var unsupportedReason = transport?.Reason
            ?? (transport is null && returnProjection.Identity.Kind != ClrTypeKind.Void
                ? "Source IR does not provide reviewed transport metadata."
                : null);
        if (unsupportedReason is not null)
        {
            namedArguments.Add(
                $"UnsupportedReason = \"{EscapeCSharp(unsupportedReason)}\"");
        }

        return
            "[global::Microsoft.JSInterop.DomOperation(" +
            $"\"{EscapeCSharp(logicalIdentity)}\", " +
            $"\"{EscapeCSharp(javaScriptName)}\", " +
            $"global::Microsoft.JSInterop.DomTransportKind.{transportKind}, " +
            $"\"{EscapeCSharp(sourceType)}\", " +
            $"{string.Join(", ", namedArguments)})]";
    }

    private sealed record IndexAccessorBuild(string Rendered, string CanonicalKey);

    private IndexAccessorBuild BuildIndexAccessor(
        MemberModel member,
        string symbolName,
        int declarationOrdinal,
        GenericScope scope)
    {
        var provenance =
            $"{symbolName}/decl[{declarationOrdinal}]/member[{member.Ordinal}]/indexSignature";
        if (member.Parameters.Count != 1
            || member.Parameters[0].Type is null)
        {
            throw new TypeProjectionException(
                $"Index signature at '{provenance}' must have exactly one typed key parameter.",
                provenance);
        }
        var parameter = member.Parameters[0];
        var keySourceType = parameter.Type!;
        var valueSource = member.ReturnType ?? member.Type
            ?? throw new TypeProjectionException(
                $"Index signature at '{provenance}' has no value type.",
                provenance);
        var key = typeResolver.Project(
            keySourceType,
            $"{provenance}/key",
            scope);
        var value = typeResolver.Project(
            valueSource,
            $"{provenance}/value",
            scope);
        if (key.Identity.Kind is ClrTypeKind.Null or ClrTypeKind.Void
            || value.Identity.Kind is ClrTypeKind.Null or ClrTypeKind.Void)
        {
            throw new TypeProjectionException(
                $"Index signature at '{provenance}' projects an illegal key or value type.",
                provenance);
        }

        var keyKind = key.CanonicalType switch
        {
            "string" => "String",
            "double" or "float" or "decimal" or "byte" or "sbyte" or "short"
                or "ushort" or "int" or "uint" or "long" or "ulong" => "Number",
            _ when keySourceType.CheckerType == "symbol" => "Symbol",
            _ => throw new TypeProjectionException(
                $"Index signature at '{provenance}' uses unsupported key type " +
                $"'{keySourceType.CheckerType ?? key.RenderedType}'.",
                $"{provenance}/key"),
        };
        var keyName = Naming.ToCSharpParameterName(parameter.Name);
        var writer = new CSharpWriter();
        writer.AppendLine(RenderIndexMetadata(
            "Get",
            keyKind,
            keySourceType,
            value));
        writer.AppendLine(
            $"{value.RenderedType} GetIndexedValueBy{keyKind}" +
            $"({key.RenderedType} {keyName});");
        if (!member.Readonly)
        {
            writer.AppendLine(RenderIndexMetadata(
                "Set",
                keyKind,
                keySourceType,
                value));
            writer.AppendLine(
                $"void SetIndexedValueBy{keyKind}({key.RenderedType} {keyName}, " +
                $"{value.RenderedType} value);");
        }
        return new IndexAccessorBuild(
            writer.ToString().TrimEnd(),
            $"{key.CanonicalType}->{value.CanonicalType}:{member.Readonly}");
    }

    private static string RenderIndexMetadata(
        string operation,
        string keyKind,
        TypeNode key,
        TypeProjection value)
    {
        var transport = value.Transport;
        var transportKind = transport?.Kind switch
        {
            "json-value" => "JsonValue",
            "js-reference" => "JsReference",
            "js-stream" => "JsStream",
            "binary" => "Binary",
            "transferable" => "Transferable",
            _ => "Unsupported",
        };
        var keySource = key.Transport?.SourceType
            ?? key.CheckerType
            ?? key.Kind;
        var valueSource = transport?.SourceType
            ?? value.CSharpType;
        return
            "[global::Microsoft.JSInterop.DomIndexAccessor(" +
            $"global::Microsoft.JSInterop.DomAccessorOperation.{operation}, " +
            $"global::Microsoft.JSInterop.DomIndexKeyKind.{keyKind}, " +
            $"\"{EscapeCSharp(keySource)}\", " +
            $"global::Microsoft.JSInterop.DomTransportKind.{transportKind}, " +
            $"\"{EscapeCSharp(valueSource)}\", " +
            $"Nullable = {(value.IsNullable || transport?.Nullable == true).ToString().ToLowerInvariant()}, " +
            $"Streamable = {(transport?.Streamable == true).ToString().ToLowerInvariant()}, " +
            $"StructuredClone = {(transport?.StructuredClone == true).ToString().ToLowerInvariant()})]";
    }

    private static HashSet<(int DeclarationOrdinal, int MemberOrdinal)>
        FindLiteralDispatchMethods(IReadOnlyList<DeclarationModel> declarations)
    {
        var result = new HashSet<(int, int)>();
        var methods = declarations
            .SelectMany(declaration => declaration.Members
                .Where(member => member.Kind == "method" && member.Name is not null)
                .Select(member => (Declaration: declaration, Member: member)))
            .GroupBy(item => item.Member.Name!.Text, StringComparer.Ordinal);
        foreach (var group in methods.Where(group => group.Count() > 1))
        {
            foreach (var item in group.Where(item =>
                item.Member.Parameters.Any(parameter =>
                    parameter.Type is LiteralTypeNode
                    {
                        LiteralKind: "StringLiteral",
                    })))
            {
                result.Add((item.Declaration.Ordinal, item.Member.Ordinal));
            }
        }
        return result;
    }

    private static MethodSig ReplaceReturnType(
        MethodSig signature,
        TypeProjection returnProjection,
        IReadOnlyList<TypeNode?> returnSources)
    {
        var previous = signature.ReturnProjection
            ?? throw new InvalidOperationException(
                "A reconciled method signature requires its return projection.");
        var marker = $"{previous.RenderedType} {MethodName(signature.CanonicalKey)}";
        var markerIndex = signature.Rendered.LastIndexOf(
            marker,
            StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            throw new InvalidOperationException(
                $"Cannot replace return type in '{signature.CanonicalKey}'.");
        }
        var rendered =
            signature.Rendered[..markerIndex] +
            returnProjection.RenderedType +
            signature.Rendered[(markerIndex + previous.RenderedType.Length)..];
        return signature with
        {
            Rendered = rendered,
            ReturnType = returnProjection.CanonicalType,
            ReturnProjection = returnProjection,
            ReturnSources = returnSources,
            ReturnTransport = TransportIdentity(returnProjection.Transport),
            SourceIdentity =
                $"{signature.SourceIdentity}|reconciled-return:" +
                $"{returnProjection.CanonicalType}:" +
                $"{TransportIdentity(returnProjection.Transport)}",
        };
    }

    private static string BuildSourceIdentity(
        string javaScriptName,
        GenericDeclaration generic,
        IReadOnlyList<ParameterModel> parameters,
        IReadOnlyList<string> clrParameterTypes,
        TypeProjection returnProjection)
        => $"js:{javaScriptName}`{generic.EmittedArity}" +
           $"<{generic.CanonicalConstraints}>(" +
           $"{string.Join(",", parameters.OrderBy(parameter => parameter.Ordinal)
               .Select(parameter =>
                   $"{FormatSourceType(parameter.Type)}:" +
                   $"optional={parameter.Optional}:rest={parameter.Rest}"))})" +
           $"[clr:{string.Join(",", clrParameterTypes)}]" +
           $"->{returnProjection.CanonicalType}" +
           $"@{TransportIdentity(returnProjection.Transport)}";

    private static string TransportIdentity(TransportModel? transport)
        => transport is null
            ? "transport:none"
            : $"transport:{transport.Kind}:{transport.Nullable}:" +
              $"{transport.Streamable}:{transport.StructuredClone}:" +
              $"{transport.SourceType}";

    private static string FormatSourceType(TypeNode? type)
        => type switch
        {
            null => "void",
            KeywordTypeNode keyword => keyword.Name,
            LiteralTypeNode literal =>
                $"{literal.LiteralKind}:{literal.Text}",
            ReferenceTypeNode reference =>
                $"{reference.ResolvedSymbol ?? reference.Name}<" +
                $"{string.Join(",", reference.TypeArguments.Select(FormatSourceType))}>",
            UnionTypeNode union =>
                $"union({string.Join("|", union.Types.Select(FormatSourceType))})",
            ArrayTypeNode array => $"{FormatSourceType(array.ElementType)}[]",
            ParenthesizedTypeNode parenthesized =>
                $"({FormatSourceType(parenthesized.InnerType)})",
            _ => $"{type.Kind}:{type.CheckerType}",
        };

    private static string BuildBaseClause(
        IReadOnlyList<DeclarationModel> allDecls,
        TypeResolver typeResolver,
        string symbolName,
        GenericScope declarationScope)
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

                    var resolvedBaseName = hrt.ResolvedSymbol ?? hrt.Expression;
                    if (!typeResolver.IsKnownSymbol(resolvedBaseName)
                        && !typeResolver.IsStandardStructuralHeritage(hrt)
                        && resolvedBaseName is not
                            "ReadonlyMap" and
                            not "Map" and
                            not "ReadonlySet" and
                            not "Set" and
                            not "Iterable" and
                            not "IterableIterator" and
                            not "ArrayIterator" and
                            not "Iterator" and
                            not "IteratorObject" and
                            not "MapIterator" and
                            not "SetIterator" and
                            not "AsyncIterable" and
                            not "AsyncIterableIterator" and
                            not "AsyncIterator" and
                            not "AsyncIteratorObject")
                    {
                        throw new TypeProjectionException(
                            $"Interface '{symbolName}' decl[{decl.Ordinal}] extends unknown symbol '{resolvedBaseName}'. " +
                            "Add it to the symbol index or provide an emitter override.",
                            $"{symbolName}/extends/{resolvedBaseName}");
                    }

                    if (typeResolver.IsKnownSymbol(resolvedBaseName)
                        && !typeResolver.IsInterfaceOrMixin(resolvedBaseName)
                        && !typeResolver.IsDictionarySymbol(resolvedBaseName))
                    {
                        throw new TypeProjectionException(
                            $"Interface '{symbolName}' decl[{decl.Ordinal}] extends '{resolvedBaseName}' which has classification " +
                            $"'{typeResolver.GetClassification(resolvedBaseName)}', not interface/mixin. " +
                            "Only interface/mixin bases are valid in a C# interface base clause.",
                            $"{symbolName}/extends/{resolvedBaseName}");
                    }

                    var heritageProvenance =
                        $"{symbolName}/decl[{decl.Ordinal}]/extends/{resolvedBaseName}";
                    var projection = typeResolver.IsDictionarySymbol(resolvedBaseName)
                        ? typeResolver.ProjectDictionaryContract(
                            hrt,
                            heritageProvenance,
                            declarationScope)
                        : typeResolver.Project(
                            hrt,
                            heritageProvenance,
                            declarationScope);
                    if (projection.Identity.Kind != ClrTypeKind.Reference)
                    {
                        throw new TypeProjectionException(
                            $"Interface '{symbolName}' decl[{decl.Ordinal}] extends " +
                            $"non-reference projection '{projection.RenderedType}'.",
                            $"{symbolName}/extends/{resolvedBaseName}");
                    }
                    var csBase = projection.RenderedType;
                    if (seenBases.Add(csBase))
                        bases.Add(csBase);
                }
            }
        }

        return string.Join(", ", bases);
    }

    private static string CanonicalMethodKey(
        string csName,
        int arity,
        IReadOnlyList<string> canonicalParamTypes)
        => $"{csName}`{arity}({string.Join(",", canonicalParamTypes)})";

    private List<ParameterModel> MergeGlobalParameterForms(
        string ownerSymbol,
        string memberName,
        IReadOnlyList<ParameterModel> ownerParameters,
        TypeProjection ownerReturn,
        GenericScope scope)
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
                    $"{alias.Symbol.Name}/decl[{declaration.Ordinal}]/globalFunction/return",
                    scope);
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
                        $"{ownerSymbol}/{memberName}/parameter[{index}]",
                        scope);
                    var aliasProjection = typeResolver.Project(
                        aliasParameters[index].Type,
                        $"{alias.Symbol.Name}/decl[{declaration.Ordinal}]/" +
                        $"globalFunction/parameter[{aliasParameters[index].Ordinal}]",
                        scope);
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

    private static string ParameterProvenance(
        string provenance,
        ParameterModel parameter,
        bool defaultExpansion)
        => $"{provenance}/parameter[{parameter.Ordinal}]/{parameter.Name}" +
            (defaultExpansion ? "/defaultExpansion" : "");

    private static void ValidateMethodReturn(
        TypeProjection projection,
        string provenance,
        bool defaultExpansion)
    {
        if (projection.Identity.Kind != ClrTypeKind.Null)
            return;
        ThrowIllegalMethodIdentity(
            $"return resolves to illegal standalone CLR type " +
            $"'{projection.RenderedType}'",
            $"{provenance}/return" +
            (defaultExpansion ? "/defaultExpansion" : ""),
            defaultExpansion);
    }

    private static void ValidateMethodParameter(
        TypeProjection projection,
        ParameterModel parameter,
        string provenance,
        bool defaultExpansion)
    {
        if (projection.Identity.Kind is not (ClrTypeKind.Null or ClrTypeKind.Void))
            return;
        ThrowIllegalMethodIdentity(
            $"parameter '{parameter.Name}' resolves to illegal CLR type " +
            $"'{projection.RenderedType}'",
            ParameterProvenance(provenance, parameter, defaultExpansion),
            defaultExpansion);
    }

    private static void ThrowIllegalMethodIdentity(
        string detail,
        string provenance,
        bool defaultExpansion)
    {
        var message = defaultExpansion
            ? $"Default-expanded generic method {detail} at '{provenance}'."
            : $"Method {detail} at '{provenance}'.";
        if (defaultExpansion)
        {
            throw new GenericDeferralException(
                message,
                provenance,
                "generic-method-defaults");
        }
        throw new TypeProjectionException(message, provenance);
    }

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
    /// Returns true if the method is an authoritative event-subscription overload:
    /// must be named addEventListener or removeEventListener, and all type parameters
    /// must be constrained by keyof &lt;SomeEventMap&gt;.
    /// </summary>
    private bool IsEventSubscriptionOverload(MemberModel method)
    {
        var name = method.Name?.Text;
        if (name is not ("addEventListener" or "removeEventListener"))
            return false;
        return method.TypeParameters.All(tp => IsKeyofEventMapConstraint(tp.Constraint));
    }

    private bool IsKeyofEventMapConstraint(TypeNode? constraint)
        => constraint is OperatorTypeNode op &&
           (op.Operator is "keyof" or "KeyOfKeyword") &&
           op.OperandType is ReferenceTypeNode rf &&
           typeResolver.TryGetSymbol(rf.ResolvedSymbol ?? rf.Name, out var symbol) &&
           symbol.Declarations.Any(declaration => declaration.EventMap.IsEventMap);

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
