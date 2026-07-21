using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Projection;

namespace Blazor.DOM.CSharpGenerator.Emitters;

internal sealed record ContractSignature(
    string Rendered,
    string CanonicalKey,
    string CanonicalReturnType,
    string CanonicalConstraints,
    int OptionalParameterCount,
    bool HasRestParameter);

internal sealed record ContractCallableResult(
    IReadOnlyList<ContractSignature> Signatures,
    IReadOnlyList<string> ShapeKeys,
    MemberOutcomeStatus Status,
    string? Phase,
    string Reason,
    IReadOnlyList<ParameterOutcome> ParameterOutcomes);

internal sealed record ContractPropertyResult(
    string Rendered,
    string CanonicalKey,
    string CanonicalType,
    bool Mutable,
    AccessorTypeIdentity? TypeIdentity = null);

internal sealed class ContractCallableException(
    string message,
    string provenance,
    IReadOnlyList<ParameterOutcome> parameterOutcomes)
    : TypeProjectionException(message, provenance)
{
    public IReadOnlyList<ParameterOutcome> ParameterOutcomes { get; } =
        parameterOutcomes;
}

internal sealed class ContractMemberEmitter(TypeResolver typeResolver)
{
    internal ContractCallableResult EmitCallable(
        string jsName,
        IReadOnlyList<TypeParameterModel> typeParameters,
        IReadOnlyList<ParameterModel> parameters,
        TypeNode? returnType,
        DocumentationModel documentation,
        string provenance,
        string? csharpNameOverride = null)
    {
        if (typeParameters.Count > 0 && IsEventSubscriptionOverload(jsName, typeParameters))
        {
            const string eventReason =
                "Mapped to the Window target's DomEventDescriptor<TEvent> " +
                "subscription contract.";
            return new ContractCallableResult(
                [],
                [BuildTypeScriptShapeKey(
                    jsName,
                    typeParameters,
                    parameters,
                    returnType)],
                MemberOutcomeStatus.Projected,
                null,
                eventReason,
                CreateParameterOutcomes(
                    parameters,
                    provenance,
                    MemberOutcomeStatus.Projected,
                    null,
                    eventReason));
        }

        GenericDeclaration generic;
        try
        {
            generic = typeResolver.CreateGenericDeclaration(
                typeParameters,
                provenance,
                canonicalPrefix: "!!");
        }
        catch (GenericDeferralException exception)
        {
            return new ContractCallableResult(
                [],
                [BuildTypeScriptShapeKey(
                    jsName,
                    typeParameters,
                    parameters,
                    returnType)],
                MemberOutcomeStatus.Deferred,
                exception.Phase,
                exception.Message,
                CreateParameterOutcomes(
                    parameters,
                    provenance,
                    MemberOutcomeStatus.Deferred,
                    exception.Phase,
                    exception.Message));
        }
        IReadOnlyList<GenericDeclaration> defaultExpansions;
        try
        {
            defaultExpansions = typeResolver.CreateDefaultExpandedDeclarations(
                typeParameters,
                provenance,
                canonicalPrefix: "!!");
        }
        catch (GenericDeferralException exception)
        {
            return CreateGenericDeferral(
                jsName,
                typeParameters,
                parameters,
                returnType,
                provenance,
                exception);
        }

        TypeProjection returnProjection;
        try
        {
            returnProjection = typeResolver.Project(
                returnType,
                $"{provenance}/return",
                generic.Scope);
            ValidateReturnIdentity(
                returnProjection,
                $"{provenance}/return",
                defaultExpansion: false);
        }
        catch (GenericDeferralException exception)
        {
            return CreateGenericDeferral(
                jsName,
                typeParameters,
                parameters,
                returnType,
                provenance,
                exception);
        }
        catch (TypeProjectionException exception)
        {
            throw new ContractCallableException(
                exception.Message,
                exception.Provenance,
                CreateParameterOutcomes(
                    parameters,
                    provenance,
                    MemberOutcomeStatus.NotAttemptedAfterFailure,
                    null,
                    $"Not attempted because return projection failed: {exception.Message}"));
        }

        var emittedName = csharpNameOverride
            ?? Naming.ToCSharpMemberName(jsName);
        if (returnProjection.Identity.IsAwaitable
            && !emittedName.EndsWith("Async", StringComparison.Ordinal))
        {
            emittedName += "Async";
        }

        var orderedParameters = parameters
            .OrderBy(parameter => parameter.Ordinal)
            .ToList();
        var parameterOutcomes = new List<ParameterOutcome>();
        var projections = new List<TypeProjection>();
        for (var index = 0; index < orderedParameters.Count; index++)
        {
            var parameter = orderedParameters[index];
            var parameterProvenance =
                $"{provenance}/parameter[{parameter.Ordinal}]/{parameter.Name}";
            try
            {
                TypeProjection projection;
                if (TryGetBoolOptionsUnion(
                        parameter.Type,
                        out var optionsTypeName))
                {
                    projection = typeResolver.Project(
                        new ReferenceTypeNode(
                            optionsTypeName,
                            optionsTypeName,
                            []),
                        $"{parameterProvenance}/options",
                        generic.Scope);
                }
                else
                {
                    projection = typeResolver.Project(
                        parameter.Type,
                        parameterProvenance,
                        generic.Scope);
                }
                if (projection.Identity.Kind is ClrTypeKind.Null or ClrTypeKind.Void)
                {
                    throw new TypeProjectionException(
                        $"Parameter '{parameter.Name}' at '{parameterProvenance}' " +
                        $"resolves to '{projection.CSharpType}' and cannot be emitted.",
                        parameterProvenance);
                }
                projections.Add(projection);
                parameterOutcomes.Add(CreateParameterOutcome(
                    parameter,
                    parameterProvenance,
                    MemberOutcomeStatus.Projected,
                    null,
                    "emitted"));
            }
            catch (GenericDeferralException exception)
            {
                return CreateGenericDeferral(
                    jsName,
                    typeParameters,
                    parameters,
                    returnType,
                    provenance,
                    exception);
            }
            catch (TypeProjectionException exception)
            {
                parameterOutcomes.Add(CreateParameterOutcome(
                    parameter,
                    parameterProvenance,
                    MemberOutcomeStatus.Failed,
                    null,
                    exception.Message));
                parameterOutcomes.AddRange(orderedParameters
                    .Skip(index + 1)
                    .Select(later => CreateParameterOutcome(
                        later,
                        $"{provenance}/parameter[{later.Ordinal}]/{later.Name}",
                        MemberOutcomeStatus.NotAttemptedAfterFailure,
                        null,
                        $"Not attempted because parameter '{parameter.Name}' " +
                        $"failed: {exception.Message}")));
                throw new ContractCallableException(
                    exception.Message,
                    exception.Provenance,
                    parameterOutcomes);
            }
        }

        var boolOptionsIndex = orderedParameters.FindIndex(parameter =>
            TryGetBoolOptionsUnion(parameter.Type, out _));
        IReadOnlyList<ContractSignature> signatures;
        string reason;
        if (boolOptionsIndex >= 0)
        {
            _ = TryGetBoolOptionsUnion(
                orderedParameters[boolOptionsIndex].Type,
                out var optionsTypeName);
            var optionsType = typeResolver.Project(
                new ReferenceTypeNode(optionsTypeName, optionsTypeName, []),
                $"{provenance}/options",
                generic.Scope);
            signatures =
            [
                BuildSignature(
                    emittedName,
                    returnProjection,
                    orderedParameters,
                    projections,
                    documentation,
                    generic,
                    dropFromIndex: boolOptionsIndex),
                BuildSignature(
                    emittedName,
                    returnProjection,
                    orderedParameters,
                    projections,
                    new DocumentationModel("", [], false),
                    generic,
                    substituteIndex: boolOptionsIndex,
                    substituteType: "bool",
                    substituteCanonicalType: "bool",
                    substituteName: "capture"),
                BuildSignature(
                    emittedName,
                    returnProjection,
                    orderedParameters,
                    projections,
                    new DocumentationModel("", [], false),
                    generic,
                    substituteIndex: boolOptionsIndex,
                    substituteType: $"{optionsType.RenderedType}?",
                    substituteCanonicalType: optionsType.CanonicalType,
                    substituteName: orderedParameters[boolOptionsIndex].Name),
            ];
            reason =
                $"Emitted all optional boolean/{optionsTypeName} forms as " +
                "no-options, boolean, and options overloads.";
        }
        else
        {
            signatures =
            [
                BuildSignature(
                    emittedName,
                    returnProjection,
                    orderedParameters,
                    projections,
                    documentation,
                    generic)
            ];
            reason = "emitted";
        }

        var allSignatures = signatures.ToList();
        try
        {
            foreach (var expansion in defaultExpansions)
            {
                var expandedReturn = typeResolver.Project(
                    returnType,
                    $"{provenance}/return/defaultExpansion",
                    expansion.Scope);
                ValidateReturnIdentity(
                    expandedReturn,
                    $"{provenance}/return/defaultExpansion",
                    defaultExpansion: true);
                var expandedProjections = orderedParameters
                    .Select((parameter, index) => typeResolver.Project(
                        TryGetBoolOptionsUnion(parameter.Type, out var expandedOptions)
                            ? new ReferenceTypeNode(
                                expandedOptions,
                                expandedOptions,
                                [])
                            : parameter.Type,
                        $"{provenance}/parameter[{parameter.Ordinal}]/" +
                        $"{parameter.Name}/defaultExpansion",
                        expansion.Scope))
                    .ToList();
                var invalidParameter = expandedProjections
                    .Select((projection, index) => (projection, index))
                    .FirstOrDefault(item =>
                        item.projection.Identity.Kind
                            is ClrTypeKind.Null or ClrTypeKind.Void);
                if (invalidParameter.projection is not null)
                {
                    var sourceParameter =
                        orderedParameters[invalidParameter.index];
                    var invalidProvenance =
                        $"{provenance}/parameter[{sourceParameter.Ordinal}]/" +
                        $"{sourceParameter.Name}/defaultExpansion";
                    throw new TypeProjectionException(
                        $"Default-expanded parameter '{sourceParameter.Name}' at " +
                        $"'{invalidProvenance}' resolves to illegal CLR type " +
                        $"'{invalidParameter.projection.RenderedType}'.",
                        invalidProvenance);
                }
                if (boolOptionsIndex >= 0)
                {
                    _ = TryGetBoolOptionsUnion(
                        orderedParameters[boolOptionsIndex].Type,
                        out var expandedOptionsTypeName);
                    var expandedOptionsType = typeResolver.Project(
                        new ReferenceTypeNode(
                            expandedOptionsTypeName,
                            expandedOptionsTypeName,
                            []),
                        $"{provenance}/options/defaultExpansion",
                        expansion.Scope);
                    allSignatures.Add(BuildSignature(
                        emittedName,
                        expandedReturn,
                        orderedParameters,
                        expandedProjections,
                        documentation,
                        expansion,
                        dropFromIndex: boolOptionsIndex));
                    allSignatures.Add(BuildSignature(
                        emittedName,
                        expandedReturn,
                        orderedParameters,
                        expandedProjections,
                        new DocumentationModel("", [], false),
                        expansion,
                        substituteIndex: boolOptionsIndex,
                        substituteType: "bool",
                        substituteCanonicalType: "bool",
                        substituteName: "capture"));
                    allSignatures.Add(BuildSignature(
                        emittedName,
                        expandedReturn,
                        orderedParameters,
                        expandedProjections,
                        new DocumentationModel("", [], false),
                        expansion,
                        substituteIndex: boolOptionsIndex,
                        substituteType: $"{expandedOptionsType.RenderedType}?",
                        substituteCanonicalType: expandedOptionsType.CanonicalType,
                        substituteName: orderedParameters[boolOptionsIndex].Name));
                }
                else
                {
                    allSignatures.Add(BuildSignature(
                        emittedName,
                        expandedReturn,
                        orderedParameters,
                        expandedProjections,
                        documentation,
                        expansion));
                }
            }
        }
        catch (GenericDeferralException exception)
        {
            return CreateGenericDeferral(
                jsName,
                typeParameters,
                parameters,
                returnType,
                provenance,
                exception);
        }
        catch (TypeProjectionException exception)
        {
            return CreateGenericDeferral(
                jsName,
                typeParameters,
                parameters,
                returnType,
                provenance,
                new GenericDeferralException(
                    $"Default-expanded generic callable '{jsName}' cannot be emitted: " +
                    exception.Message,
                    exception.Provenance,
                    "generic-method-defaults"));
        }

        return new ContractCallableResult(
            allSignatures,
            allSignatures.Select(signature => signature.CanonicalKey).ToList(),
            MemberOutcomeStatus.Projected,
            null,
            defaultExpansions.Count == 0
                ? reason
                : $"{reason}; emitted {defaultExpansions.Count} " +
                  "default-expanded overload set(s).",
            parameterOutcomes);
    }

    private static ContractCallableResult CreateGenericDeferral(
        string jsName,
        IReadOnlyList<TypeParameterModel> typeParameters,
        IReadOnlyList<ParameterModel> parameters,
        TypeNode? returnType,
        string provenance,
        GenericDeferralException exception)
        => new(
            [],
            [BuildTypeScriptShapeKey(
                jsName,
                typeParameters,
                parameters,
                returnType)],
            MemberOutcomeStatus.Deferred,
            exception.Phase,
            exception.Message,
            CreateParameterOutcomes(
                parameters,
                provenance,
                MemberOutcomeStatus.Deferred,
                exception.Phase,
                exception.Message));

    private static void ValidateReturnIdentity(
        TypeProjection projection,
        string provenance,
        bool defaultExpansion)
    {
        if (projection.Identity.Kind != ClrTypeKind.Null)
            return;
        var message = defaultExpansion
            ? $"Default-expanded return resolves to illegal standalone CLR type " +
              $"'{projection.RenderedType}' at '{provenance}'."
            : $"Return resolves to illegal standalone CLR type " +
              $"'{projection.RenderedType}' at '{provenance}'.";
        throw new TypeProjectionException(message, provenance);
    }

    internal ContractPropertyResult EmitProperty(
        string jsName,
        TypeNode? type,
        bool optional,
        bool mutable,
        DocumentationModel documentation,
        string provenance)
    {
        var projection = typeResolver.Project(type, provenance);
        if (projection.Identity.Kind is ClrTypeKind.Null or ClrTypeKind.Void)
        {
            throw new TypeProjectionException(
                $"Property '{jsName}' at '{provenance}' resolves to " +
                $"'{projection.CSharpType}' and cannot be emitted.",
                provenance);
        }

        var effective = projection with
        {
            IsNullable = projection.IsNullable || optional,
        };
        var csharpName = Naming.ToCSharpMemberName(jsName);
        var writer = new CSharpWriter();
        writer.XmlDoc(documentation.Text, documentation.Deprecated);
        writer.AppendLine(mutable
            ? $"{effective.RenderedType} {csharpName} {{ get; set; }}"
            : $"{effective.RenderedType} {csharpName} {{ get; }}");
        return new ContractPropertyResult(
            writer.ToString().TrimEnd(),
            $"property:{csharpName}",
            effective.CanonicalType,
            mutable,
            AccessorTypeIdentity.Create(effective, optional, type));
    }

    private static ContractSignature BuildSignature(
        string emittedName,
        TypeProjection returnProjection,
        IReadOnlyList<ParameterModel> parameters,
        IReadOnlyList<TypeProjection> projections,
        DocumentationModel documentation,
        GenericDeclaration generic,
        int substituteIndex = -1,
        string? substituteType = null,
        string? substituteCanonicalType = null,
        string? substituteName = null,
        int dropFromIndex = -1)
    {
        var parts = new List<string>();
        var canonicalTypes = new List<string>();
        var optionalCount = 0;
        var hasRestParameter = false;
        for (var index = 0; index < parameters.Count; index++)
        {
            if (dropFromIndex >= 0 && index >= dropFromIndex)
                break;

            var parameter = parameters[index];
            if (index == substituteIndex && substituteType is not null)
            {
                parts.Add(
                    $"{substituteType} {Naming.ToCSharpParameterName(
                        substituteName ?? parameter.Name)}");
                canonicalTypes.Add(
                    substituteCanonicalType ?? substituteType.TrimEnd('?'));
                continue;
            }

            var projection = projections[index];
            var type = projection.RenderedType;
            var name = Naming.ToCSharpParameterName(parameter.Name);
            if (parameter.Rest)
            {
                hasRestParameter = true;
                var elementType = type.EndsWith("[]", StringComparison.Ordinal)
                    ? type[..^2]
                    : type;
                parts.Add($"params {elementType}[] {name}");
                canonicalTypes.Add(projection.CanonicalType);
            }
            else if (parameter.Optional)
            {
                var optionalProjection = projection with
                {
                    IsNullable = projection.IsNullable
                        || projection.Identity.Kind == ClrTypeKind.Reference,
                };
                parts.Add($"{optionalProjection.RenderedType} {name} = default");
                canonicalTypes.Add(optionalProjection.CanonicalType);
                optionalCount++;
            }
            else
            {
                parts.Add($"{type} {name}");
                canonicalTypes.Add(projection.CanonicalType);
            }
        }

        var writer = new CSharpWriter();
        writer.XmlDoc(documentation.Text, documentation.Deprecated);
        foreach (var defaultNote in generic.DefaultNotes)
            writer.AppendLine($"// TypeScript generic default: {defaultNote}.");
        writer.AppendLine(
            $"{returnProjection.RenderedType} {emittedName}" +
            $"{generic.TypeParameterList}({string.Join(", ", parts)})" +
            $"{generic.ConstraintSuffix};");
        return new ContractSignature(
            writer.ToString().TrimEnd(),
            $"{emittedName}`{generic.EmittedArity}(" +
            $"{string.Join(",", canonicalTypes)})",
            returnProjection.CanonicalType,
            generic.CanonicalConstraints,
            optionalCount,
            hasRestParameter);
    }

    private bool IsEventSubscriptionOverload(
        string name,
        IReadOnlyList<TypeParameterModel> typeParameters)
        => name is "addEventListener" or "removeEventListener"
            && typeParameters.All(parameter =>
                parameter.Constraint is OperatorTypeNode
                {
                    Operator: "keyof" or "KeyOfKeyword",
                    OperandType: ReferenceTypeNode reference,
                }
                && typeResolver.TryGetSymbol(
                    reference.ResolvedSymbol ?? reference.Name,
                    out var symbol)
                && symbol.Declarations.Any(declaration =>
                    declaration.EventMap.IsEventMap));

    private static bool TryGetBoolOptionsUnion(
        TypeNode? type,
        out string optionsTypeName)
    {
        optionsTypeName = "";
        if (type is ParenthesizedTypeNode parenthesized)
            type = parenthesized.InnerType;
        if (type is not UnionTypeNode union)
            return false;

        var arms = union.Types
            .Where(arm => !IsNullish(arm))
            .ToList();
        if (arms.Count != 2
            || !arms.Any(arm => arm is KeywordTypeNode keyword
                && (keyword.Name is "BooleanKeyword" or "boolean"
                    || keyword.CheckerType == "boolean")))
        {
            return false;
        }

        var options = arms.OfType<ReferenceTypeNode>().FirstOrDefault(reference =>
            reference.Name is
                "EventListenerOptions" or
                "AddEventListenerOptions");
        if (options is null)
            return false;
        optionsTypeName = options.ResolvedSymbol ?? options.Name;
        return true;
    }

    private static bool IsNullish(TypeNode type)
        => type is KeywordTypeNode keyword
            && (keyword.Name is "NullKeyword" or "UndefinedKeyword"
                || keyword.CheckerType is "null" or "undefined")
            || type is LiteralTypeNode literal
            && literal.LiteralKind is
                "NullLiteral" or
                "NullKeyword" or
                "UndefinedKeyword";

    private static IReadOnlyList<ParameterOutcome> CreateParameterOutcomes(
        IReadOnlyList<ParameterModel> parameters,
        string provenance,
        MemberOutcomeStatus status,
        string? phase,
        string reason)
        => parameters
            .OrderBy(parameter => parameter.Ordinal)
            .Select(parameter => CreateParameterOutcome(
                parameter,
                $"{provenance}/parameter[{parameter.Ordinal}]/{parameter.Name}",
                status,
                phase,
                reason))
            .ToList();

    private static ParameterOutcome CreateParameterOutcome(
        ParameterModel parameter,
        string provenance,
        MemberOutcomeStatus status,
        string? phase,
        string reason)
        => new(
            parameter.Ordinal,
            parameter.Name,
            status,
            phase,
            reason,
            provenance,
            SourceAccountingShape.FormatLocation(parameter.Location));

    private static string BuildTypeScriptShapeKey(
        string name,
        IReadOnlyList<TypeParameterModel> typeParameters,
        IReadOnlyList<ParameterModel> parameters,
        TypeNode? returnType)
        => $"typescript:{name}<{string.Join(",", typeParameters.Select(
            parameter => $"{parameter.Name}:{FormatType(parameter.Constraint)}"))}>(" +
            $"{string.Join(",", parameters.OrderBy(parameter => parameter.Ordinal)
                .Select(parameter =>
                    $"{FormatType(parameter.Type)}:{parameter.Optional}:{parameter.Rest}"))})" +
            $":{FormatType(returnType)}";

    private static string FormatType(TypeNode? type)
        => type switch
        {
            null => "void",
            KeywordTypeNode keyword => keyword.Name,
            ReferenceTypeNode reference =>
                $"{reference.ResolvedSymbol ?? reference.Name}<" +
                $"{string.Join(",", reference.TypeArguments.Select(FormatType))}>",
            HeritageReferenceTypeNode heritage =>
                $"{heritage.ResolvedSymbol ?? heritage.Expression}<" +
                $"{string.Join(",", heritage.TypeArguments.Select(FormatType))}>",
            UnionTypeNode union =>
                $"union({string.Join("|", union.Types.Select(FormatType))})",
            IntersectionTypeNode intersection =>
                $"intersection({string.Join("&", intersection.Types.Select(FormatType))})",
            ArrayTypeNode array => $"{FormatType(array.ElementType)}[]",
            LiteralTypeNode literal =>
                $"{literal.LiteralKind}:{literal.Text}",
            ParenthesizedTypeNode parenthesized =>
                $"({FormatType(parenthesized.InnerType)})",
            FunctionTypeNode function =>
                BuildTypeScriptShapeKey(
                    "function",
                    function.TypeParameters,
                    function.Parameters,
                    function.ReturnType),
            OperatorTypeNode operation =>
                $"{operation.Operator} {FormatType(operation.OperandType)}",
            QueryTypeNode query =>
                $"typeof {query.ResolvedSymbol ?? query.ExpressionName}",
            _ => $"{type.Kind}:{type.CheckerType}",
        };
}
