using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Projection;

namespace Blazor.DOM.CSharpGenerator.Emitters;

internal sealed class GlobalNamespaceEmitter(
    TypeResolver typeResolver,
    string generatorVersion,
    string rootNamespace,
    DeclarationRoutingPlan routingPlan,
    IReadOnlyDictionary<string, PrimarySymbolEmission> primaryEmissions)
{
    private const string FactoryPhase = "factory-constructor";
    private const string GlobalAliasPhase = "global-alias";

    private readonly ContractMemberEmitter _memberEmitter = new(typeResolver);
    private readonly Dictionary<string, SupplementalBuilder> _builders =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, ContractAccumulator> _namespaceContracts =
        new(StringComparer.Ordinal);
    private readonly ContractAccumulator _windowContract = new("IWindow");
    private readonly Dictionary<string, SymbolModel> _symbolIndex =
        routingPlan.Symbols.ToDictionary(
            route => route.Symbol.Name,
            route => route.Symbol,
            StringComparer.Ordinal);
    private readonly Dictionary<DeclarationModel, SymbolModel> _declarationOwners =
        routingPlan.Symbols
            .SelectMany(route => route.Declarations.Select(declaration =>
                (declaration.Declaration, route.Symbol)))
            .ToDictionary(
                item => item.Declaration,
                item => item.Symbol);

    internal SupplementalGenerationResult Emit(OutputWriter writer)
    {
        var supplementalRoutes = routingPlan.SupplementalDeclarations;
        foreach (var route in supplementalRoutes)
            _builders.TryAdd(route.Symbol.Name, new SupplementalBuilder(route.Symbol));

        foreach (var namespaceRoute in supplementalRoutes.Where(route =>
            route.Route == DeclarationRouteKind.Namespace))
        {
            var scope = namespaceRoute.TypeScriptNamespace
                ?? namespaceRoute.Symbol.Name;
            _namespaceContracts.TryAdd(
                scope,
                new ContractAccumulator(
                    NamespaceContractName(scope)));
        }

        var factoryProducts = supplementalRoutes
            .Where(route => route.Route == DeclarationRouteKind.FactoryConstructor)
            .Select(route => EmitFactory(route, writer))
            .Where(product => product is not null)
            .Cast<FactoryProduct>()
            .ToList();

        foreach (var product in factoryProducts)
        {
            var accumulator = product.TypeScriptNamespace is null
                ? _windowContract
                : GetNamespaceContract(product.TypeScriptNamespace);
            if (!accumulator.TryAddProperty(
                    product.Accessor,
                    product.Route,
                    out var collision))
            {
                FailRoute(product.Route, collision!, "GlobalFactoryCollisionException");
            }
        }

        foreach (var route in supplementalRoutes.Where(route =>
            route.Route == DeclarationRouteKind.GlobalFunction))
        {
            EmitGlobalFunction(route);
        }

        foreach (var route in supplementalRoutes.Where(route =>
            route.Route == DeclarationRouteKind.GlobalVariable))
        {
            EmitGlobalVariable(route);
        }

        foreach (var route in supplementalRoutes.Where(route =>
            route.Route == DeclarationRouteKind.Namespace))
        {
            var scope = route.TypeScriptNamespace ?? route.Symbol.Name;
            var namespaceContract = GetNamespaceContract(scope);
            var namespaceReference =
                $"global::{NamespaceName(scope)}.{NamespaceContractName(scope)}";
            var accessorName = Naming.ToCSharpMemberName(
                Naming.ToCSharpSimpleTypeName(scope));
            var accessor = new ContractPropertyResult(
                RenderProperty(
                    route.Declaration.Documentation,
                    namespaceReference,
                    accessorName,
                    mutable: false),
                $"property:{accessorName}",
                namespaceReference,
                Mutable: false);
            var parentScope = Naming.GetTypeScriptNamespace(scope);
            var parentContract = parentScope is null
                ? _windowContract
                : GetNamespaceContract(parentScope);
            if (!parentContract.TryAddProperty(
                    accessor,
                    route,
                    out var collision))
            {
                FailRoute(route, collision!, "NamespaceCollisionException");
                continue;
            }

            namespaceContract.AddContributor(route);
            AddDeclarationOutcome(
                route,
                MemberOutcomeStatus.Projected,
                null,
                "Namespace container emitted as a logical namespace contract " +
                "reachable from IWindow.");
        }

        WriteNamespaceContracts(writer);
        WriteWindowContract(writer);

        foreach (var route in supplementalRoutes)
        {
            var builder = GetBuilder(route.Symbol);
            if (!builder.HasDeclarationOutcome(route.Declaration.Ordinal))
            {
                builder.AddDeclaration(CreateDeclarationOutcome(
                    route,
                    MemberOutcomeStatus.Failed,
                    null,
                    "Declaration router produced no terminal supplemental outcome."));
                builder.Fail(
                    $"Declaration '{route.Symbol.Name}/decl[{route.Declaration.Ordinal}]' " +
                    "has no supplemental emission outcome.",
                    "DeclarationRoutingException");
            }
        }

        return new SupplementalGenerationResult(
            _builders.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Build(),
                StringComparer.Ordinal));
    }

    private FactoryProduct? EmitFactory(
        RoutedDeclaration route,
        OutputWriter writer)
    {
        if (route.Declaration.Type is QueryTypeNode query)
            return EmitFactoryAlias(route, query);

        if (route.Declaration.Type is not TypeLiteralTypeNode typeLiteral)
        {
            AddDeclarationOutcome(
                route,
                MemberOutcomeStatus.Deferred,
                FactoryPhase,
                "Constructor/static alias is not a concrete type literal and is " +
                "deferred to the factory-constructor phase.");
            return null;
        }

        var sourceMembers = SourceAccountingShape.GetMembers(route.Symbol)
            .Where(source =>
                source.Declaration.Ordinal == route.Declaration.Ordinal
                && source.NestedTypeLiteral)
            .OrderBy(source => source.Member.Ordinal)
            .ToList();
        var sourceOverloads = SourceAccountingShape.GetOverloads(
                route.Symbol,
                SourceAccountingShape.GetMembers(route.Symbol))
            .Where(source =>
                source.Declaration.Ordinal == route.Declaration.Ordinal
                && source.SourceMember?.NestedTypeLiteral == true)
            .ToDictionary(
                source => source.MemberOrdinal!.Value,
                source => source);
        var builder = GetBuilder(route.Symbol);
        if (primaryEmissions.TryGetValue(route.Symbol.Name, out var primary)
            && primary.Disposition is SymbolEmissionDisposition.Deferred
                or SymbolEmissionDisposition.Failed)
        {
            var phase = primary.Phase ?? "primary-contract";
            var reason =
                $"Supplemental factory suppressed because primary contract " +
                $"'{route.Symbol.Name}' is {primary.Disposition.ToString().ToLowerInvariant()}: " +
                $"{primary.Reason ?? "no primary contract was emitted"}";
            foreach (var source in sourceMembers)
            {
                AddMemberOutcome(
                    source,
                    MemberOutcomeStatus.Deferred,
                    phase,
                    reason);
                if (sourceOverloads.TryGetValue(source.Member.Ordinal, out var overload))
                {
                    AddOverloadOutcome(
                        overload,
                        MemberOutcomeStatus.Deferred,
                        phase,
                        reason,
                        CreateParameterOutcomes(
                            overload,
                            MemberOutcomeStatus.Deferred,
                            phase,
                            reason));
                }
            }
            AddDeclarationOutcome(
                route,
                MemberOutcomeStatus.Deferred,
                phase,
                reason);
            return null;
        }
        var properties = new Dictionary<string, FactoryProperty>(
            StringComparer.Ordinal);
        var signatures = new Dictionary<string, FactorySignature>(
            StringComparer.Ordinal);
        var orderedOutputs = new List<FactoryOutput>();

        foreach (var source in sourceMembers)
        {
            var member = source.Member;
            try
            {
                switch (member.Kind)
                {
                    case "property":
                        var property = _memberEmitter.EmitProperty(
                            member.Name?.Text ?? member.Kind,
                            member.Type,
                            member.Optional,
                            mutable: !member.Readonly,
                            member.Documentation,
                            source.Provenance);
                        if (properties.TryGetValue(
                                property.CanonicalKey,
                                out var existingProperty))
                        {
                            if (!string.Equals(
                                    existingProperty.Result.CanonicalType,
                                    property.CanonicalType,
                                    StringComparison.Ordinal)
                                || existingProperty.Result.Mutable != property.Mutable)
                            {
                                FailFactoryCollision(
                                    route,
                                    source,
                                    sourceMembers,
                                    sourceOverloads.Values,
                                    $"Factory property '{member.Name?.Text}' collides " +
                                    "with an incompatible type or setter shape.");
                                return null;
                            }

                            AddMemberOutcome(
                                source,
                                MemberOutcomeStatus.Projected,
                                null,
                                $"Deduplicated from member ordinal " +
                                $"{existingProperty.Source.Member.Ordinal}.");
                            break;
                        }

                        properties.Add(
                            property.CanonicalKey,
                            new FactoryProperty(property, source));
                        orderedOutputs.Add(new FactoryOutput(
                            source.Member.Ordinal,
                            property.Rendered,
                            property.CanonicalKey));
                        AddMemberOutcome(
                            source,
                            MemberOutcomeStatus.Projected,
                            null,
                            "Emitted in the logical factory contract.");
                        break;

                    case "method":
                    case "constructSignature":
                    case "callSignature":
                        var callableName = member.Kind switch
                        {
                            "constructSignature" => "Create",
                            "callSignature" => "Invoke",
                            _ => member.Name?.Text ?? member.Kind,
                        };
                        var csharpNameOverride = member.Kind switch
                        {
                            "constructSignature" => "Create",
                            "callSignature" => "Invoke",
                            _ => null,
                        };
                        var callable = _memberEmitter.EmitCallable(
                            member.Name?.Text ?? callableName,
                            member.TypeParameters,
                            member.Parameters,
                            member.ReturnType,
                            member.Documentation,
                            source.Provenance,
                            csharpNameOverride);
                        var overload = sourceOverloads[member.Ordinal];
                        if (callable.Status == MemberOutcomeStatus.Deferred)
                        {
                            AddMemberOutcome(
                                source,
                                MemberOutcomeStatus.Deferred,
                                callable.Phase,
                                callable.Reason);
                            AddOverloadOutcome(
                                overload,
                                MemberOutcomeStatus.Deferred,
                                callable.Phase,
                                callable.Reason,
                                SetParameterStatus(
                                    callable.ParameterOutcomes,
                                    MemberOutcomeStatus.Deferred,
                                    callable.Phase,
                                    callable.Reason));
                            break;
                        }

                        string? deduplicationReason = null;
                        foreach (var signature in callable.Signatures)
                        {
                            if (signatures.TryGetValue(
                                    signature.CanonicalKey,
                                    out var existingSignature))
                            {
                                if (!string.Equals(
                                        existingSignature.Signature.CanonicalReturnType,
                                        signature.CanonicalReturnType,
                                        StringComparison.Ordinal))
                                {
                                    FailFactoryCollision(
                                        route,
                                        source,
                                        sourceMembers,
                                        sourceOverloads.Values,
                                        $"Factory callable '{callableName}' collides for " +
                                        $"canonical signature '{signature.CanonicalKey}' " +
                                        "with an incompatible return type.");
                                    return null;
                                }
                                if (!string.Equals(
                                        existingSignature.Signature.CanonicalConstraints,
                                        signature.CanonicalConstraints,
                                        StringComparison.Ordinal))
                                {
                                    FailFactoryCollision(
                                        route,
                                        source,
                                        sourceMembers,
                                        sourceOverloads.Values,
                                        $"Factory callable '{callableName}' collides for " +
                                        $"canonical signature '{signature.CanonicalKey}' " +
                                        "with incompatible generic constraints.");
                                    return null;
                                }

                                if (signature.HasRestParameter
                                        && !existingSignature.Signature.HasRestParameter
                                    || signature.HasRestParameter
                                        == existingSignature.Signature.HasRestParameter
                                    && signature.OptionalParameterCount
                                        > existingSignature.Signature.OptionalParameterCount)
                                {
                                    ReplaceFactoryOutput(
                                        orderedOutputs,
                                        signature.CanonicalKey,
                                        signature.Rendered);
                                    signatures[signature.CanonicalKey] =
                                        new FactorySignature(signature, source);
                                }
                                deduplicationReason =
                                    $"Deduplicated from member ordinal " +
                                    $"{existingSignature.Source.Member.Ordinal}.";
                                continue;
                            }

                            signatures.Add(
                                signature.CanonicalKey,
                                new FactorySignature(signature, source));
                            orderedOutputs.Add(new FactoryOutput(
                                source.Member.Ordinal,
                                signature.Rendered,
                                signature.CanonicalKey));
                        }

                        AddMemberOutcome(
                            source,
                            MemberOutcomeStatus.Projected,
                            null,
                            deduplicationReason
                                ?? "Emitted in the logical factory contract.");
                        AddOverloadOutcome(
                            overload,
                            MemberOutcomeStatus.Projected,
                            null,
                            deduplicationReason ?? callable.Reason,
                            callable.ParameterOutcomes);
                        break;

                    default:
                        DeferFactoryMember(
                            source,
                            sourceOverloads.GetValueOrDefault(member.Ordinal),
                            $"Factory member kind '{member.Kind}' is not representable " +
                            "in this phase.");
                        break;
                }
            }
            catch (ContractCallableException exception)
            {
                DeferFactoryMember(
                    source,
                    sourceOverloads.GetValueOrDefault(member.Ordinal),
                    exception.Message,
                    exception.ParameterOutcomes);
            }
            catch (TypeProjectionException exception)
            {
                DeferFactoryMember(
                    source,
                    sourceOverloads.GetValueOrDefault(member.Ordinal),
                    exception.Message);
            }
        }

        if (orderedOutputs.Count == 0)
        {
            AddDeclarationOutcome(
                route,
                MemberOutcomeStatus.Deferred,
                FactoryPhase,
                "No factory/static member was representable; every member is " +
                "explicitly deferred to factory-constructor.");
            return null;
        }

        var factoryTypeName = FactoryTypeName(route);
        var sourceText = RenderInterface(
            Naming.ToGeneratedNamespace(rootNamespace, route.Symbol.Name),
            factoryTypeName,
            route.Declaration.Documentation,
            orderedOutputs
                .OrderBy(output => output.Ordinal)
                .ThenBy(output => output.CanonicalKey, StringComparer.Ordinal)
                .Select(output => output.Rendered)
                .ToList());
        var path = writer.Write(
            factoryTypeName,
            sourceText,
            Naming.ToOutputSubdirectory("Factories", route.Symbol.Name));
        builder.AddFile(path);

        var deferredOutcomes = builder.MemberOutcomesFor(route.Declaration.Ordinal)
            .Where(outcome => outcome.Status == MemberOutcomeStatus.Deferred)
            .ToList();
        var deferred = deferredOutcomes.Count > 0;
        var deferredPhases = deferredOutcomes
            .Select(outcome => outcome.Phase)
            .Where(phase => !string.IsNullOrWhiteSpace(phase))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var deferredPhase = deferredPhases.Count == 1
            ? deferredPhases[0]
            : FactoryPhase;
        AddDeclarationOutcome(
            route,
            deferred
                ? MemberOutcomeStatus.Deferred
                : MemberOutcomeStatus.Projected,
            deferred ? deferredPhase : null,
            deferred
                ? "Logical factory contract emitted; unsupported members remain " +
                  "deferred to factory-constructor."
                : "Logical factory contract emitted.");

        var accessorType = Naming.ToCSharpTypeReference(
            rootNamespace,
            route.Symbol.Name,
            interfaceType: false);
        var generatedNamespace = Naming.ToGeneratedNamespace(
            rootNamespace,
            route.Symbol.Name);
        accessorType = route.Symbol.Name.Contains('.', StringComparison.Ordinal)
            ? $"global::{generatedNamespace}.{factoryTypeName}"
            : factoryTypeName;
        var accessorName = FactoryAccessorName(route);
        return new FactoryProduct(
            route,
            route.TypeScriptNamespace,
            new ContractPropertyResult(
                RenderProperty(
                    route.Declaration.Documentation,
                    accessorType,
                    accessorName,
                    mutable: false),
                $"property:{accessorName}",
                accessorType,
                Mutable: false));
    }

    private FactoryProduct? EmitFactoryAlias(
        RoutedDeclaration route,
        QueryTypeNode query)
    {
        var targetIdentity = string.IsNullOrWhiteSpace(query.ResolvedSymbol)
            ? query.ExpressionName
            : query.ResolvedSymbol;
        var targetRoute = routingPlan.SupplementalDeclarations
            .Where(candidate =>
                candidate.Route == DeclarationRouteKind.FactoryConstructor
                && string.Equals(
                    candidate.Symbol.Name,
                    targetIdentity,
                    StringComparison.Ordinal)
                && candidate.Declaration.Type is TypeLiteralTypeNode)
            .OrderBy(candidate => candidate.Declaration.Ordinal)
            .FirstOrDefault();
        if (targetRoute is null)
        {
            AddDeclarationOutcome(
                route,
                MemberOutcomeStatus.Failed,
                null,
                $"Constructor alias '{route.Declaration.Name}' targets " +
                $"'{targetIdentity ?? "<unresolved>"}', which has no concrete " +
                "factory declaration.");
            GetBuilder(route.Symbol).Fail(
                $"Constructor alias '{route.Declaration.Name}' cannot resolve its " +
                "canonical factory target.",
                "MissingFactoryAliasTargetException");
            return null;
        }

        var targetFactoryName = FactoryTypeName(targetRoute);
        var targetNamespace = Naming.ToGeneratedNamespace(
            rootNamespace,
            targetRoute.Symbol.Name);
        var accessorType = targetRoute.Symbol.Name.Contains(
            '.',
            StringComparison.Ordinal)
                ? $"global::{targetNamespace}.{targetFactoryName}"
                : targetFactoryName;
        var accessorName = FactoryAccessorName(route);
        var javaScriptName = route.Declaration.Name.Replace(
            "\"",
            "\\\"",
            StringComparison.Ordinal);
        var accessor = new ContractPropertyResult(
            $"[global::Microsoft.JSInterop.DomGlobalAlias(\"{javaScriptName}\")]\n" +
            RenderProperty(
                route.Declaration.Documentation,
                accessorType,
                accessorName,
                mutable: false),
            $"property:{accessorName}",
            accessorType,
            Mutable: false);
        AddDeclarationOutcome(
            route,
            MemberOutcomeStatus.Projected,
            null,
            $"Legacy constructor alias reuses canonical factory " +
            $"'{targetRoute.Symbol.Name}'.");
        return new FactoryProduct(
            route,
            route.TypeScriptNamespace,
            accessor);
    }

    private void EmitGlobalFunction(RoutedDeclaration route)
    {
        var sourceOverload = SourceAccountingShape.GetOverloads(route.Symbol)
            .Single(overload =>
                overload.Declaration.Ordinal == route.Declaration.Ordinal
                && overload.Kind == "globalFunction");
        ContractCallableResult callable;
        try
        {
            callable = _memberEmitter.EmitCallable(
                route.Declaration.Name,
                route.Declaration.TypeParameters,
                route.Declaration.Parameters,
                route.Declaration.ReturnType,
                route.Declaration.Documentation,
                sourceOverload.Provenance,
                csharpNameOverride: route.TypeScriptNamespace is null
                    && route.Symbol.Name == "toString"
                        ? "GlobalToString"
                        : null);
            if (route.TypeScriptNamespace is null
                && route.Symbol.Name == "toString")
            {
                callable = callable with
                {
                    Signatures = callable.Signatures
                        .Select(signature => signature with
                        {
                            Rendered =
                                "[global::Microsoft.JSInterop.DomGlobalAlias(\"toString\")]\n" +
                                signature.Rendered,
                        })
                        .ToList(),
                };
            }
        }
        catch (ContractCallableException exception)
        {
            AddDeclarationOutcome(
                route,
                MemberOutcomeStatus.Failed,
                null,
                exception.Message);
            AddOverloadOutcome(
                sourceOverload,
                MemberOutcomeStatus.Failed,
                null,
                exception.Message,
                exception.ParameterOutcomes);
            GetBuilder(route.Symbol).Fail(
                exception.Message,
                nameof(ContractCallableException));
            return;
        }

        if (callable.Status == MemberOutcomeStatus.Deferred)
        {
            AddDeclarationOutcome(
                route,
                MemberOutcomeStatus.Deferred,
                callable.Phase,
                callable.Reason);
            AddOverloadOutcome(
                sourceOverload,
                MemberOutcomeStatus.Deferred,
                callable.Phase,
                callable.Reason,
                callable.ParameterOutcomes);
            return;
        }

        if (route.TypeScriptNamespace is not null)
        {
            AddCallableToContract(
                GetNamespaceContract(route.TypeScriptNamespace),
                route,
                sourceOverload,
                callable);
            return;
        }

        if (route.Symbol.Semantic.BindingKind != "globalMember")
        {
            AddCallableToContract(
                _windowContract,
                route,
                sourceOverload,
                callable);
            return;
        }

        var ownerMatch = FindOwnerCallable(route, callable);
        if (ownerMatch.Failure is not null)
        {
            FailGlobalCallable(route, sourceOverload, callable, ownerMatch.Failure);
            return;
        }
        var mergedCallable = MergeOwnerCallable(
            route,
            ownerMatch,
            callable,
            sourceOverload.Provenance);

        if (ownerMatch.Member is not null
            && ownerMatch.Owner is not null
            && primaryEmissions.TryGetValue(
                ownerMatch.Owner.Name,
                out var ownerEmission)
            && ownerEmission.Disposition == SymbolEmissionDisposition.Projected
            && ownerEmission.GeneratedFile is not null
            && !(ownerEmission.MemberOutcomes ?? []).Any(
                outcome => outcome.Status == MemberOutcomeStatus.Failed)
            && HasCompleteWindowContract())
        {
            var ownerOutcome = FindMemberOutcome(
                ownerEmission,
                ownerMatch.DeclarationOrdinal,
                ownerMatch.Member.Ordinal);
            if (ownerOutcome?.Status == MemberOutcomeStatus.Deferred)
            {
                AddDeclarationOutcome(
                    route,
                    MemberOutcomeStatus.Deferred,
                    ownerOutcome.Phase,
                    ownerOutcome.Reason
                        ?? "Canonical Window member is deferred.");
                AddOverloadOutcome(
                    sourceOverload,
                    MemberOutcomeStatus.Deferred,
                    ownerOutcome.Phase,
                    ownerOutcome.Reason
                        ?? "Canonical Window member is deferred.",
                    SetParameterStatus(
                        callable.ParameterOutcomes,
                        MemberOutcomeStatus.Deferred,
                        ownerOutcome.Phase,
                        ownerOutcome.Reason
                            ?? "Canonical Window member is deferred."));
                return;
            }

            AddDeclarationOutcome(
                route,
                MemberOutcomeStatus.Projected,
                null,
                $"Merged with canonical Window-profile member " +
                $"'{ownerMatch.Owner.Name}.{route.Declaration.Name}'.");
            AddOverloadOutcome(
                sourceOverload,
                MemberOutcomeStatus.Projected,
                null,
                "Merged with an identical canonical Window-profile signature.",
                callable.ParameterOutcomes);
            GetBuilder(route.Symbol).AddFile(ownerEmission.GeneratedFile);
            return;
        }

        AddCallableToContract(
            _windowContract,
            route,
            sourceOverload,
            mergedCallable);
    }

    private void EmitGlobalVariable(RoutedDeclaration route)
    {
        if (route.TypeScriptNamespace is null
            && route.Declaration.Name.StartsWith("on", StringComparison.Ordinal))
        {
            var eventName = route.Declaration.Name[2..];
            if (!GetEffectiveEventNames("WindowEventMap").Contains(eventName))
            {
                FailRoute(
                    route,
                    $"Global event handler '{route.Declaration.Name}' has no " +
                    $"authoritative descriptor in WindowEventMap for '{eventName}'.",
                    "GlobalEventMapAssociationException");
                return;
            }
            const string reason =
                "Represented by the authoritative Window EventMap typed descriptor.";
            AddDeclarationOutcome(
                route,
                MemberOutcomeStatus.Projected,
                null,
                reason);
            if (!primaryEmissions.TryGetValue(
                    "WindowEventMap",
                    out var windowEventMap)
                || windowEventMap.Disposition != SymbolEmissionDisposition.Projected
                || windowEventMap.GeneratedFile is null)
            {
                FailRoute(
                    route,
                    "WindowEventMap descriptor catalog was not generated.",
                    "MissingGeneratedContractException");
                return;
            }
            GetBuilder(route.Symbol).AddFile(windowEventMap.GeneratedFile);
            return;
        }

        if (route.TypeScriptNamespace is null
            && route.Declaration.Type is KeywordTypeNode
            {
                Name: "VoidKeyword",
            })
        {
            var aliasName = Naming.ToCSharpMemberName(route.Declaration.Name)
                + "GlobalAlias";
            var aliasProperty = new ContractPropertyResult(
                "[global::Microsoft.JSInterop.DomGlobalAlias(\"" +
                route.Declaration.Name.Replace("\"", "\\\"", StringComparison.Ordinal) +
                "\")]\n" +
                $"global::Microsoft.JSInterop.BrowserUndefined {aliasName} {{ get; }}",
                $"property:{aliasName}",
                "global::Microsoft.JSInterop.BrowserUndefined",
                Mutable: false);
            AddPropertyToContract(_windowContract, route, aliasProperty);
            return;
        }

        ContractPropertyResult property;
        try
        {
            property = _memberEmitter.EmitProperty(
                route.Declaration.Name,
                route.Declaration.Type,
                optional: false,
                mutable: route.Declaration.VariableKind is "var" or "let",
                route.Declaration.Documentation,
                $"{route.Symbol.Name}/decl[{route.Declaration.Ordinal}]/globalVariable");
        }
        catch (TypeProjectionException exception)
        {
            AddDeclarationOutcome(
                route,
                MemberOutcomeStatus.Failed,
                null,
                exception.Message);
            GetBuilder(route.Symbol).Fail(
                exception.Message,
                exception.GetType().Name);
            return;
        }

        if (route.TypeScriptNamespace is not null)
        {
            AddPropertyToContract(
                GetNamespaceContract(route.TypeScriptNamespace),
                route,
                property);
            return;
        }

        if (route.Symbol.Semantic.BindingKind != "globalMember")
        {
            AddPropertyToContract(_windowContract, route, property);
            return;
        }

        var ownerMatch = FindOwnerProperty(route, property);
        if (ownerMatch.Failure is not null)
        {
            AddDeclarationOutcome(
                route,
                MemberOutcomeStatus.Failed,
                null,
                ownerMatch.Failure);
            GetBuilder(route.Symbol).Fail(
                ownerMatch.Failure,
                "GlobalPropertyCollisionException");
            return;
        }
        var mergedProperty = MergeOwnerProperty(route, ownerMatch, property);

        if (ownerMatch.Member is not null
            && ownerMatch.Owner is not null
            && primaryEmissions.TryGetValue(
                ownerMatch.Owner.Name,
                out var ownerEmission)
            && ownerEmission.Disposition == SymbolEmissionDisposition.Projected
            && ownerEmission.GeneratedFile is not null
            && !(ownerEmission.MemberOutcomes ?? []).Any(
                outcome => outcome.Status == MemberOutcomeStatus.Failed)
            && HasCompleteWindowContract())
        {
            var ownerOutcome = FindMemberOutcome(
                ownerEmission,
                ownerMatch.DeclarationOrdinal,
                ownerMatch.Member.Ordinal);
            if (ownerOutcome?.Status == MemberOutcomeStatus.Deferred)
            {
                AddDeclarationOutcome(
                    route,
                    MemberOutcomeStatus.Deferred,
                    ownerOutcome.Phase,
                    ownerOutcome.Reason
                        ?? "Canonical Window property is deferred.");
                return;
            }

            AddDeclarationOutcome(
                route,
                MemberOutcomeStatus.Projected,
                null,
                $"Merged with canonical Window-profile property " +
                $"'{ownerMatch.Owner.Name}.{route.Declaration.Name}', including " +
                "global setter semantics.");
            GetBuilder(route.Symbol).AddFile(ownerEmission.GeneratedFile);
            return;
        }

        AddPropertyToContract(_windowContract, route, mergedProperty);
    }

    private HashSet<string> GetEffectiveEventNames(string eventMap)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        Add(eventMap);
        return names;

        void Add(string mapName)
        {
            if (!visited.Add(mapName)
                || !_symbolIndex.TryGetValue(mapName, out var symbol))
            {
                return;
            }
            foreach (var declaration in symbol.Declarations.Where(declaration =>
                declaration.EventMap.IsEventMap))
            {
                foreach (var member in declaration.Members)
                {
                    if (member.Name is not null)
                        names.Add(member.Name.Text);
                }
                foreach (var heritage in declaration.Heritage
                    .Where(clause => clause.Token == "extends")
                    .SelectMany(clause => clause.Types))
                {
                    var baseName = heritage switch
                    {
                        ReferenceTypeNode reference =>
                            reference.ResolvedSymbol ?? reference.Name,
                        HeritageReferenceTypeNode reference =>
                            reference.ResolvedSymbol ?? reference.Expression,
                        _ => null,
                    };
                    if (baseName is not null)
                        Add(baseName);
                }
            }
        }
    }

    private bool HasCompleteWindowContract()
        => primaryEmissions.TryGetValue("Window", out var emission)
            && emission.Disposition == SymbolEmissionDisposition.Projected
            && emission.GeneratedFile is not null
            && !(emission.MemberOutcomes ?? []).Any(
                outcome => outcome.Status == MemberOutcomeStatus.Failed);

    private ContractCallableResult MergeOwnerCallable(
        RoutedDeclaration route,
        OwnerCallableMatch ownerMatch,
        ContractCallableResult globalCallable,
        string provenance)
    {
        if (ownerMatch.Member is null)
            return globalCallable;

        var ownerParameters = ownerMatch.Member.Parameters
            .OrderBy(parameter => parameter.Ordinal)
            .ToList();
        var globalParameters = route.Declaration.Parameters
            .OrderBy(parameter => parameter.Ordinal)
            .ToList();
        if (ownerParameters.Count != globalParameters.Count)
            return globalCallable;

        var mergedParameters = globalParameters
            .Select((parameter, index) => parameter with
            {
                Optional = parameter.Optional || ownerParameters[index].Optional,
            })
            .ToList();
        var documentation = MergeDocumentation(
            route.Declaration.Documentation,
            ownerMatch.Member.Documentation);
        return _memberEmitter.EmitCallable(
            route.Declaration.Name,
            route.Declaration.TypeParameters,
            mergedParameters,
            route.Declaration.ReturnType,
            documentation,
            provenance);
    }

    private ContractPropertyResult MergeOwnerProperty(
        RoutedDeclaration route,
        OwnerPropertyMatch ownerMatch,
        ContractPropertyResult globalProperty)
    {
        if (ownerMatch.Member is null || ownerMatch.Owner is null)
            return globalProperty;

        var ownerDeclaration = ownerMatch.Owner.Declarations.Single(declaration =>
            declaration.Ordinal == ownerMatch.DeclarationOrdinal);
        var ownerMembers = ownerDeclaration.Members.Where(member =>
            member.Name?.Text
                == (route.Symbol.Semantic.WebIdlMemberName
                    ?? route.Declaration.Name)).ToList();
        var ownerMutable = ownerMembers.Any(member =>
            member.Kind == "setter"
            || member is { Kind: "property", Readonly: false });
        var documentation = MergeDocumentation(
            route.Declaration.Documentation,
            ownerMatch.Member.Documentation);
        return _memberEmitter.EmitProperty(
            route.Declaration.Name,
            route.Declaration.Type,
            optional: false,
            mutable: globalProperty.Mutable || ownerMutable,
            documentation,
            $"{route.Symbol.Name}/decl[{route.Declaration.Ordinal}]/globalVariable");
    }

    private OwnerCallableMatch FindOwnerCallable(
        RoutedDeclaration route,
        ContractCallableResult globalCallable)
    {
        var ownerName = route.Symbol.Semantic.WebIdlName;
        if (string.IsNullOrWhiteSpace(ownerName)
            || !_symbolIndex.TryGetValue(ownerName, out var owner))
        {
            return new OwnerCallableMatch(
                null,
                null,
                -1,
                $"Global function '{route.Symbol.Name}' names missing Window " +
                $"owner '{ownerName ?? "(none)"}'.");
        }

        var name = route.Symbol.Semantic.WebIdlMemberName
            ?? route.Declaration.Name;
        foreach (var declaration in owner.Declarations
            .Where(declaration => declaration.Kind == "interface")
            .OrderBy(declaration => declaration.Ordinal))
        {
            foreach (var member in declaration.Members
                .Where(member =>
                    member.Kind == "method"
                    && member.Name?.Text == name)
                .OrderBy(member => member.Ordinal))
            {
                ContractCallableResult ownerCallable;
                try
                {
                    ownerCallable = _memberEmitter.EmitCallable(
                        member.Name!.Text,
                        member.TypeParameters,
                        member.Parameters,
                        member.ReturnType,
                        member.Documentation,
                        $"{owner.Name}/decl[{declaration.Ordinal}]/" +
                        $"member[{member.Ordinal}]/{member.Name.Text}");
                }
                catch (ContractCallableException)
                {
                    continue;
                }

                var globalSignatures = globalCallable.Signatures
                    .OrderBy(signature => signature.CanonicalKey, StringComparer.Ordinal)
                    .ToList();
                var ownerSignatures = ownerCallable.Signatures
                    .OrderBy(signature => signature.CanonicalKey, StringComparer.Ordinal)
                    .ToList();
                if (globalSignatures.Select(signature => signature.CanonicalKey)
                    .SequenceEqual(
                        ownerSignatures.Select(signature => signature.CanonicalKey),
                        StringComparer.Ordinal))
                {
                    if (!globalSignatures
                        .Select(signature => signature.CanonicalReturnType)
                        .SequenceEqual(
                            ownerSignatures.Select(signature =>
                                signature.CanonicalReturnType),
                            StringComparer.Ordinal))
                    {
                        return new OwnerCallableMatch(
                            owner,
                            member,
                            declaration.Ordinal,
                            $"Global function '{route.Symbol.Name}' collides with " +
                            $"'{owner.Name}.{name}' using an identical canonical C# " +
                            "signature but incompatible return types.");
                    }
                    return new OwnerCallableMatch(
                        owner,
                        member,
                        declaration.Ordinal,
                        null);
                }
            }
        }

        return new OwnerCallableMatch(
            owner,
            null,
            -1,
            null);
    }

    private OwnerPropertyMatch FindOwnerProperty(
        RoutedDeclaration route,
        ContractPropertyResult globalProperty)
    {
        var ownerName = route.Symbol.Semantic.WebIdlName;
        if (string.IsNullOrWhiteSpace(ownerName)
            || !_symbolIndex.TryGetValue(ownerName, out var owner))
        {
            return new OwnerPropertyMatch(
                null,
                null,
                -1,
                $"Global variable '{route.Symbol.Name}' names missing Window " +
                $"owner '{ownerName ?? "(none)"}'.");
        }

        var name = route.Symbol.Semantic.WebIdlMemberName
            ?? route.Declaration.Name;
        foreach (var declaration in owner.Declarations
            .Where(declaration => declaration.Kind == "interface")
            .OrderBy(declaration => declaration.Ordinal))
        {
            var members = declaration.Members
                .Where(member =>
                    member.Name?.Text == name
                    && member.Kind is "property" or "getter" or "setter")
                .OrderBy(member => member.Ordinal)
                .ToList();
            foreach (var member in members.Where(member =>
                member.Kind is "property" or "getter"))
            {
                TypeProjection ownerProjection;
                try
                {
                    ownerProjection = typeResolver.Project(
                        member.Kind == "getter"
                            ? member.ReturnType
                            : member.Type,
                        $"{owner.Name}/decl[{declaration.Ordinal}]/" +
                        $"{name}/{member.Kind}");
                }
                catch (TypeProjectionException)
                {
                    continue;
                }

                var ownerIdentity = AccessorTypeIdentity.Create(
                    ownerProjection,
                    member.Optional,
                    member.Kind == "getter"
                        ? member.ReturnType
                        : member.Type);
                if (globalProperty.TypeIdentity is not null
                    ? !ownerIdentity.StructurallyEquals(
                        globalProperty.TypeIdentity)
                    : !string.Equals(
                        ownerProjection.CanonicalType,
                        globalProperty.CanonicalType,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var setter in members.Where(candidate =>
                    candidate.Kind == "setter"))
                {
                    if (setter.Parameters.Count != 1)
                    {
                        return new OwnerPropertyMatch(
                            owner,
                            member,
                            declaration.Ordinal,
                            $"Window-profile setter '{owner.Name}.{name}' does " +
                            "not have exactly one parameter.");
                    }
                }

                return new OwnerPropertyMatch(
                    owner,
                    member,
                    declaration.Ordinal,
                    null);
            }
        }

        return new OwnerPropertyMatch(
            owner,
            null,
            -1,
            $"Global variable '{route.Symbol.Name}' has no compatible canonical " +
            $"property on Window-profile owner '{owner.Name}'.");
    }

    private void AddCallableToContract(
        ContractAccumulator accumulator,
        RoutedDeclaration route,
        SourceOverloadShape sourceOverload,
        ContractCallableResult callable)
    {
        if (!accumulator.TryAddCallable(
                callable,
                route,
                out var collisionReason))
        {
            FailGlobalCallable(
                route,
                sourceOverload,
                callable,
                collisionReason!);
            return;
        }

        AddDeclarationOutcome(
            route,
            MemberOutcomeStatus.Projected,
            null,
            route.TypeScriptNamespace is null
                ? "Emitted in the partial IWindow global contract."
                : $"Emitted in namespace contract " +
                  $"'{route.TypeScriptNamespace}'.");
        AddOverloadOutcome(
            sourceOverload,
            MemberOutcomeStatus.Projected,
            null,
            callable.Reason,
            callable.ParameterOutcomes);
    }

    private void AddPropertyToContract(
        ContractAccumulator accumulator,
        RoutedDeclaration route,
        ContractPropertyResult property)
    {
        if (!accumulator.TryAddProperty(
                property,
                route,
                out var collisionReason))
        {
            AddDeclarationOutcome(
                route,
                MemberOutcomeStatus.Failed,
                null,
                collisionReason!);
            GetBuilder(route.Symbol).Fail(
                collisionReason!,
                "GlobalPropertyCollisionException");
            return;
        }

        AddDeclarationOutcome(
            route,
            MemberOutcomeStatus.Projected,
            null,
            route.TypeScriptNamespace is null
                ? "Emitted in the partial IWindow global contract."
                : $"Emitted in namespace contract " +
                  $"'{route.TypeScriptNamespace}'.");
    }

    private void FailGlobalCallable(
        RoutedDeclaration route,
        SourceOverloadShape sourceOverload,
        ContractCallableResult callable,
        string reason)
    {
        AddDeclarationOutcome(
            route,
            MemberOutcomeStatus.Failed,
            null,
            reason);
        AddOverloadOutcome(
            sourceOverload,
            MemberOutcomeStatus.Failed,
            null,
            reason,
            SetParameterStatus(
                callable.ParameterOutcomes,
                MemberOutcomeStatus.NotAttemptedAfterFailure,
                null,
                $"Not emitted because canonical collision validation failed: " +
                reason));
        GetBuilder(route.Symbol).Fail(
            reason,
            "GlobalCallableCollisionException");
    }

    private void DeferFactoryMember(
        SourceMemberShape source,
        SourceOverloadShape? overload,
        string reason,
        IReadOnlyList<ParameterOutcome>? parameterOutcomes = null)
    {
        AddMemberOutcome(
            source,
            MemberOutcomeStatus.Deferred,
            FactoryPhase,
            reason);
        if (overload is not null)
        {
            AddOverloadOutcome(
                overload,
                MemberOutcomeStatus.Deferred,
                FactoryPhase,
                reason,
                parameterOutcomes is null
                    ? CreateParameterOutcomes(
                        overload,
                        MemberOutcomeStatus.Deferred,
                        FactoryPhase,
                        reason)
                    : SetParameterStatus(
                        parameterOutcomes,
                        MemberOutcomeStatus.Deferred,
                        FactoryPhase,
                        reason));
        }
    }

    private void FailFactoryCollision(
        RoutedDeclaration route,
        SourceMemberShape collisionSource,
        IReadOnlyList<SourceMemberShape> sourceMembers,
        IEnumerable<SourceOverloadShape> sourceOverloads,
        string reason)
    {
        var builder = GetBuilder(route.Symbol);
        foreach (var source in sourceMembers)
        {
            var status = source.QualifiedKey == collisionSource.QualifiedKey
                ? MemberOutcomeStatus.Failed
                : builder.TryGetMember(source.QualifiedKey)?.Status
                    == MemberOutcomeStatus.Deferred
                    ? MemberOutcomeStatus.Deferred
                    : MemberOutcomeStatus.NotAttemptedAfterFailure;
            var phase = status == MemberOutcomeStatus.Deferred
                ? FactoryPhase
                : null;
            builder.AddOrReplaceMember(CreateMemberOutcome(
                source,
                status,
                phase,
                status == MemberOutcomeStatus.Failed
                    ? reason
                    : status == MemberOutcomeStatus.Deferred
                        ? builder.TryGetMember(source.QualifiedKey)?.Reason
                          ?? reason
                        : $"Not emitted because factory collision validation " +
                          $"failed: {reason}"));
        }

        foreach (var overload in sourceOverloads)
        {
            var memberStatus = overload.SourceMember is null
                ? MemberOutcomeStatus.NotAttemptedAfterFailure
                : builder.TryGetMember(overload.SourceMember.QualifiedKey)?.Status
                    ?? MemberOutcomeStatus.NotAttemptedAfterFailure;
            var phase = memberStatus == MemberOutcomeStatus.Deferred
                ? FactoryPhase
                : null;
            var overloadReason = memberStatus == MemberOutcomeStatus.Deferred
                ? builder.TryGetMember(overload.SourceMember!.QualifiedKey)?.Reason
                    ?? reason
                : memberStatus == MemberOutcomeStatus.Failed
                    ? reason
                    : $"Not emitted because factory collision validation failed: " +
                      reason;
            builder.AddOrReplaceOverload(CreateOverloadOutcome(
                overload,
                memberStatus,
                phase,
                overloadReason,
                CreateParameterOutcomes(
                    overload,
                    memberStatus,
                    phase,
                    overloadReason)));
        }

        builder.AddOrReplaceDeclaration(CreateDeclarationOutcome(
            route,
            MemberOutcomeStatus.Failed,
            null,
            reason));
        builder.Fail(reason, "FactoryCollisionException");
    }

    private void FailRoute(
        RoutedDeclaration route,
        string reason,
        string exceptionType)
    {
        AddDeclarationOutcome(
            route,
            MemberOutcomeStatus.Failed,
            null,
            reason);
        GetBuilder(route.Symbol).Fail(reason, exceptionType);
    }

    private void WriteNamespaceContracts(OutputWriter writer)
    {
        foreach (var pair in _namespaceContracts
            .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var scope = pair.Key;
            var contract = pair.Value;
            var source = RenderInterface(
                NamespaceName(scope),
                contract.TypeName,
                new DocumentationModel(
                    $"Logical contract for the TypeScript {scope} namespace.",
                    [],
                    false),
                contract.RenderedMembers);
            var path = writer.Write(
                contract.TypeName,
                source,
                NamespaceOutputSubdirectory(scope));
            foreach (var contributor in contract.Contributors)
                GetBuilder(contributor.Symbol).AddFile(path);
        }
    }

    private void WriteWindowContract(OutputWriter writer)
    {
        if (_windowContract.RenderedMembers.Count == 0)
            return;

        var source = RenderInterface(
            rootNamespace,
            "IWindow",
            new DocumentationModel(
                "Window-profile global declarations and logical constructor " +
                "accessors.",
                [],
                false),
            _windowContract.RenderedMembers);
        var path = writer.Write("IWindow.Globals", source, "Globals");
        foreach (var contributor in _windowContract.Contributors)
            GetBuilder(contributor.Symbol).AddFile(path);
    }

    private ContractAccumulator GetNamespaceContract(string scope)
    {
        if (!_namespaceContracts.TryGetValue(scope, out var contract))
        {
            contract = new ContractAccumulator(NamespaceContractName(scope));
            _namespaceContracts.Add(scope, contract);
        }
        return contract;
    }

    private SupplementalBuilder GetBuilder(SymbolModel symbol)
        => _builders.TryGetValue(symbol.Name, out var builder)
            ? builder
            : throw new InvalidOperationException(
                $"No supplemental outcome builder exists for '{symbol.Name}'.");

    private void AddDeclarationOutcome(
        RoutedDeclaration route,
        MemberOutcomeStatus status,
        string? phase,
        string reason)
        => GetBuilder(route.Symbol).AddOrReplaceDeclaration(
            CreateDeclarationOutcome(route, status, phase, reason));

    private void AddMemberOutcome(
        SourceMemberShape source,
        MemberOutcomeStatus status,
        string? phase,
        string reason)
        => GetBuilder(_declarationOwners[source.Declaration])
            .AddOrReplaceMember(CreateMemberOutcome(
                source,
                status,
                phase,
                reason));

    private void AddOverloadOutcome(
        SourceOverloadShape source,
        MemberOutcomeStatus status,
        string? phase,
        string reason,
        IReadOnlyList<ParameterOutcome> parameterOutcomes)
        => GetBuilder(_declarationOwners[source.Declaration])
            .AddOrReplaceOverload(CreateOverloadOutcome(
                source,
                status,
                phase,
                reason,
                parameterOutcomes));

    private static DeclarationOutcome CreateDeclarationOutcome(
        RoutedDeclaration route,
        MemberOutcomeStatus status,
        string? phase,
        string reason)
        => new(
            route.Declaration.Ordinal,
            route.Declaration.Kind,
            status,
            phase,
            reason,
            $"{route.Symbol.Name}/decl[{route.Declaration.Ordinal}]/" +
            $"{route.Declaration.Kind}",
            SourceAccountingShape.FormatLocation(route.Declaration.Location));

    private static MemberOutcome CreateMemberOutcome(
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

    private static OverloadOutcome CreateOverloadOutcome(
        SourceOverloadShape source,
        MemberOutcomeStatus status,
        string? phase,
        string reason,
        IReadOnlyList<ParameterOutcome> parameterOutcomes)
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
            parameterOutcomes);

    private static IReadOnlyList<ParameterOutcome> CreateParameterOutcomes(
        SourceOverloadShape source,
        MemberOutcomeStatus status,
        string? phase,
        string reason)
        => source.Parameters
            .OrderBy(parameter => parameter.Ordinal)
            .Select(parameter => new ParameterOutcome(
                parameter.Ordinal,
                parameter.Name,
                status,
                phase,
                reason,
                $"{source.Provenance}/parameter[{parameter.Ordinal}]/" +
                $"{parameter.Name}",
                SourceAccountingShape.FormatLocation(parameter.Location)))
            .ToList();

    private static IReadOnlyList<ParameterOutcome> SetParameterStatus(
        IReadOnlyList<ParameterOutcome> outcomes,
        MemberOutcomeStatus status,
        string? phase,
        string reason)
        => outcomes.Select(outcome => outcome with
        {
            Status = status,
            Phase = phase,
            Reason = reason,
        }).ToList();

    private static DocumentationModel MergeDocumentation(
        DocumentationModel primary,
        DocumentationModel secondary)
    {
        var text = string.Join(
            "\n\n",
            new[] { primary.Text, secondary.Text }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal));
        return new DocumentationModel(
            text,
            primary.Tags
                .Concat(secondary.Tags)
                .Distinct()
                .ToList(),
            primary.Deprecated || secondary.Deprecated);
    }

    private static MemberOutcome? FindMemberOutcome(
        PrimarySymbolEmission emission,
        int declarationOrdinal,
        int memberOrdinal)
        => emission.MemberOutcomes?.SingleOrDefault(outcome =>
            outcome.DeclarationOrdinal == declarationOrdinal
            && outcome.Ordinal == memberOrdinal);

    private string NamespaceName(string scope)
        => Naming.ToGeneratedNamespace(
            rootNamespace,
            $"{scope}.__NamespaceContract");

    private static string NamespaceContractName(string scope)
        => $"I{Naming.ToCSharpSimpleTypeName(scope)}Namespace";

    private static string NamespaceOutputSubdirectory(string scope)
        => Path.Combine(
            "Namespaces",
            string.Join(
                Path.DirectorySeparatorChar.ToString(),
                scope.Split('.', StringSplitOptions.RemoveEmptyEntries)
                    .Select(Naming.ToNamespaceSegment)));

    private static string FactoryTypeName(RoutedDeclaration route)
        => $"I{Naming.ToCSharpSimpleTypeName(route.Symbol.Name)}" +
            (route.Declaration.ConstructorObject ? "Factory" : "Statics");

    private static string FactoryAccessorName(RoutedDeclaration route)
        => $"{Naming.ToCSharpSimpleTypeName(route.Declaration.Name)}" +
            (route.Declaration.ConstructorObject ? "Constructor" : "Statics");

    private string RenderInterface(
        string generatedNamespace,
        string typeName,
        DocumentationModel documentation,
        IReadOnlyList<string> members)
    {
        var writer = new CSharpWriter();
        writer.AppendLine("#nullable enable");
        writer.AppendLine(CSharpWriter.AutoGeneratedHeader(
            "Blazor.DOM.CSharpGenerator",
            generatorVersion));
        writer.AppendLine($"namespace {generatedNamespace};");
        writer.AppendLine();
        writer.XmlDoc(documentation.Text, documentation.Deprecated);
        writer.Block($"public partial interface {typeName}", () =>
        {
            for (var index = 0; index < members.Count; index++)
            {
                if (index > 0)
                    writer.AppendLine();
                foreach (var line in members[index]
                    .Split('\n', StringSplitOptions.None))
                {
                    writer.AppendLine(line);
                }
            }
        });
        return writer.ToString();
    }

    private static string RenderProperty(
        DocumentationModel documentation,
        string type,
        string name,
        bool mutable)
    {
        var writer = new CSharpWriter();
        writer.XmlDoc(documentation.Text, documentation.Deprecated);
        writer.AppendLine(mutable
            ? $"{type} {name} {{ get; set; }}"
            : $"{type} {name} {{ get; }}");
        return writer.ToString().TrimEnd();
    }

    private static void ReplaceFactoryOutput(
        List<FactoryOutput> outputs,
        string canonicalKey,
        string rendered)
    {
        var index = outputs.FindIndex(output =>
            output.CanonicalKey == canonicalKey);
        if (index >= 0)
            outputs[index] = outputs[index] with { Rendered = rendered };
    }

    private sealed record FactoryProduct(
        RoutedDeclaration Route,
        string? TypeScriptNamespace,
        ContractPropertyResult Accessor);

    private sealed record FactoryProperty(
        ContractPropertyResult Result,
        SourceMemberShape Source);

    private sealed record FactorySignature(
        ContractSignature Signature,
        SourceMemberShape Source);

    private sealed record FactoryOutput(
        int Ordinal,
        string Rendered,
        string CanonicalKey);

    private sealed record OwnerCallableMatch(
        SymbolModel? Owner,
        MemberModel? Member,
        int DeclarationOrdinal,
        string? Failure);

    private sealed record OwnerPropertyMatch(
        SymbolModel? Owner,
        MemberModel? Member,
        int DeclarationOrdinal,
        string? Failure);

    private sealed class ContractAccumulator(string typeName)
    {
        private readonly Dictionary<string, ContractEntry> _entries =
            new(StringComparer.Ordinal);
        private readonly List<string> _order = [];
        private readonly HashSet<RoutedDeclaration> _contributors = [];

        internal string TypeName { get; } = typeName;
        internal IReadOnlyList<RoutedDeclaration> Contributors =>
            _contributors
                .OrderBy(route => route.Symbol.Ordinal)
                .ThenBy(route => route.Declaration.Ordinal)
                .ToList();
        internal IReadOnlyList<string> RenderedMembers => _order
            .Select(key => _entries[key].Rendered)
            .ToList();

        internal void AddContributor(RoutedDeclaration route)
            => _contributors.Add(route);

        internal bool TryAddProperty(
            ContractPropertyResult property,
            RoutedDeclaration contributor,
            out string? collisionReason)
        {
            if (_entries.TryGetValue(property.CanonicalKey, out var existing))
            {
                if (!PropertyTypesEqual(existing, property)
                    || existing.Mutable != property.Mutable)
                {
                    collisionReason =
                        $"Property collision for '{property.CanonicalKey}' has " +
                        "incompatible canonical type or setter semantics.";
                    return false;
                }
                _contributors.Add(contributor);
                collisionReason = null;
                return true;
            }

            _entries.Add(
                property.CanonicalKey,
                new ContractEntry(
                    property.Rendered,
                    property.CanonicalType,
                    "",
                    property.Mutable,
                    OptionalParameterCount: 0,
                    HasRestParameter: false,
                    property.TypeIdentity));
            _order.Add(property.CanonicalKey);
            _contributors.Add(contributor);
            collisionReason = null;
            return true;
        }

        internal bool TryAddCallable(
            ContractCallableResult callable,
            RoutedDeclaration contributor,
            out string? collisionReason)
        {
            var stagedEntries = new Dictionary<string, ContractEntry>(
                _entries,
                StringComparer.Ordinal);
            var stagedOrder = _order.ToList();
            foreach (var signature in callable.Signatures)
            {
                var key = $"method:{signature.CanonicalKey}";
                if (stagedEntries.TryGetValue(key, out var existing))
                {
                    if (!string.Equals(
                            existing.CanonicalType,
                            signature.CanonicalReturnType,
                            StringComparison.Ordinal))
                    {
                        collisionReason =
                            $"Callable collision for '{signature.CanonicalKey}' " +
                            "has incompatible return types.";
                        return false;
                    }
                    if (!string.Equals(
                            existing.CanonicalConstraints,
                            signature.CanonicalConstraints,
                            StringComparison.Ordinal))
                    {
                        collisionReason =
                            $"Callable collision for '{signature.CanonicalKey}' " +
                            "has incompatible generic constraints.";
                        return false;
                    }
                    if (signature.HasRestParameter && !existing.HasRestParameter
                        || signature.HasRestParameter == existing.HasRestParameter
                        && signature.OptionalParameterCount
                            > existing.OptionalParameterCount)
                    {
                        stagedEntries[key] = existing with
                        {
                            Rendered = signature.Rendered,
                            OptionalParameterCount =
                                signature.OptionalParameterCount,
                            HasRestParameter = signature.HasRestParameter,
                        };
                    }
                    continue;
                }

                stagedEntries.Add(
                    key,
                    new ContractEntry(
                        signature.Rendered,
                        signature.CanonicalReturnType,
                        signature.CanonicalConstraints,
                        Mutable: false,
                        signature.OptionalParameterCount,
                        signature.HasRestParameter));
                stagedOrder.Add(key);
            }

            _entries.Clear();
            foreach (var pair in stagedEntries)
                _entries.Add(pair.Key, pair.Value);
            _order.Clear();
            _order.AddRange(stagedOrder);
            _contributors.Add(contributor);
            collisionReason = null;
            return true;
        }

        private sealed record ContractEntry(
            string Rendered,
            string CanonicalType,
            string CanonicalConstraints,
            bool Mutable,
            int OptionalParameterCount,
            bool HasRestParameter,
            AccessorTypeIdentity? TypeIdentity = null);

        private static bool PropertyTypesEqual(
            ContractEntry existing,
            ContractPropertyResult property)
            => existing.TypeIdentity is not null
                && property.TypeIdentity is not null
                    ? existing.TypeIdentity.StructurallyEquals(
                        property.TypeIdentity)
                    : string.Equals(
                        existing.CanonicalType,
                        property.CanonicalType,
                        StringComparison.Ordinal);
    }

    private sealed class SupplementalBuilder(SymbolModel symbol)
    {
        private readonly HashSet<string> _files = new(StringComparer.Ordinal);
        private readonly Dictionary<int, DeclarationOutcome> _declarations = [];
        private readonly Dictionary<string, MemberOutcome> _members =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, OverloadOutcome> _overloads =
            new(StringComparer.Ordinal);
        private string? _failureReason;
        private string? _exceptionType;

        internal IReadOnlyList<MemberOutcome> MemberOutcomesFor(int declarationOrdinal)
            => _members.Values
                .Where(outcome =>
                    outcome.DeclarationOrdinal == declarationOrdinal)
                .ToList();

        internal void AddFile(string path) => _files.Add(path);
        internal bool HasDeclarationOutcome(int ordinal)
            => _declarations.ContainsKey(ordinal);
        internal MemberOutcome? TryGetMember(string qualifiedKey)
            => _members.GetValueOrDefault(qualifiedKey);

        internal void AddDeclaration(DeclarationOutcome outcome)
            => _declarations.Add(outcome.Ordinal, outcome);
        internal void AddOrReplaceDeclaration(DeclarationOutcome outcome)
            => _declarations[outcome.Ordinal] = outcome;
        internal void AddOrReplaceMember(MemberOutcome outcome)
            => _members[outcome.QualifiedKey
                ?? $"{symbol.Name}/decl[{outcome.DeclarationOrdinal}]/" +
                   $"member[{outcome.Ordinal}]"] = outcome;
        internal void AddOrReplaceOverload(OverloadOutcome outcome)
            => _overloads[outcome.QualifiedKey] = outcome;

        internal void Fail(string reason, string exceptionType)
        {
            _failureReason ??= reason;
            _exceptionType ??= exceptionType;
        }

        internal SupplementalSymbolEmission Build()
            => new(
                _files.OrderBy(path => path, StringComparer.Ordinal).ToList(),
                _failureReason,
                _exceptionType,
                _members.Values
                    .OrderBy(outcome => outcome.DeclarationOrdinal)
                    .ThenBy(outcome => outcome.Ordinal)
                    .ThenBy(outcome => outcome.QualifiedKey, StringComparer.Ordinal)
                    .ToList(),
                _declarations.Values
                    .OrderBy(outcome => outcome.Ordinal)
                    .ToList(),
                _overloads.Values
                    .OrderBy(outcome => outcome.DeclarationOrdinal)
                    .ThenBy(outcome => outcome.MemberOrdinal)
                    .ThenBy(outcome => outcome.QualifiedKey, StringComparer.Ordinal)
                    .ToList());
    }
}
