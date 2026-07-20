using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Projection;

namespace Blazor.DOM.CSharpGenerator.Emitters;

internal sealed record ContractSignature(
    string Rendered,
    string CanonicalKey,
    string CanonicalReturnType,
    int OptionalParameterCount);

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
    bool Mutable);

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
        if (typeParameters.Count > 0)
        {
            if (IsEventSubscriptionOverload(jsName, typeParameters))
            {
                const string phase = "event-subscription";
                const string eventReason =
                    "Event-map-keyed generic overload is deferred to the typed " +
                    "event-subscription phase.";
                return new ContractCallableResult(
                    [],
                    [BuildTypeScriptShapeKey(
                        jsName,
                        typeParameters,
                        parameters,
                        returnType)],
                    MemberOutcomeStatus.Deferred,
                    phase,
                    eventReason,
                    CreateParameterOutcomes(
                        parameters,
                        provenance,
                        MemberOutcomeStatus.Deferred,
                        phase,
                        eventReason));
            }

            throw new ContractCallableException(
                $"Generic callable '{jsName}' at '{provenance}' requires the " +
                "generic-emission phase.",
                provenance,
                CreateParameterOutcomes(
                    parameters,
                    provenance,
                    MemberOutcomeStatus.NotAttemptedAfterFailure,
                    null,
                    "Not attempted because the callable is generic."));
        }

        TypeProjection returnProjection;
        try
        {
            returnProjection = typeResolver.Project(
                returnType,
                $"{provenance}/return");
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
                        $"{parameterProvenance}/options");
                }
                else
                {
                    projection = typeResolver.Project(
                        parameter.Type,
                        parameterProvenance);
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
                $"{provenance}/options");
            signatures =
            [
                BuildSignature(
                    emittedName,
                    returnProjection,
                    orderedParameters,
                    projections,
                    documentation,
                    dropFromIndex: boolOptionsIndex),
                BuildSignature(
                    emittedName,
                    returnProjection,
                    orderedParameters,
                    projections,
                    new DocumentationModel("", [], false),
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
                    documentation)
            ];
            reason = "emitted";
        }

        return new ContractCallableResult(
            signatures,
            signatures.Select(signature => signature.CanonicalKey).ToList(),
            MemberOutcomeStatus.Projected,
            null,
            reason,
            parameterOutcomes);
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
            mutable);
    }

    private static ContractSignature BuildSignature(
        string emittedName,
        TypeProjection returnProjection,
        IReadOnlyList<ParameterModel> parameters,
        IReadOnlyList<TypeProjection> projections,
        DocumentationModel documentation,
        int substituteIndex = -1,
        string? substituteType = null,
        string? substituteCanonicalType = null,
        string? substituteName = null,
        int dropFromIndex = -1)
    {
        var parts = new List<string>();
        var canonicalTypes = new List<string>();
        var optionalCount = 0;
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
        writer.AppendLine(
            $"{returnProjection.RenderedType} {emittedName}(" +
            $"{string.Join(", ", parts)});");
        return new ContractSignature(
            writer.ToString().TrimEnd(),
            $"{emittedName}({string.Join(",", canonicalTypes)})",
            returnProjection.CanonicalType,
            optionalCount);
    }

    private static bool IsEventSubscriptionOverload(
        string name,
        IReadOnlyList<TypeParameterModel> typeParameters)
        => name is "addEventListener" or "removeEventListener"
            && typeParameters.All(parameter =>
                parameter.Constraint is OperatorTypeNode
                {
                    Operator: "keyof" or "KeyOfKeyword",
                    OperandType: ReferenceTypeNode reference,
                }
                && reference.Name.EndsWith(
                    "EventMap",
                    StringComparison.Ordinal));

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
