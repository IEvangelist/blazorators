// Callback emitter: projects TypeScript callback signatures into C# delegates.
// Every source call/construct/function signature and parameter receives a qualified outcome.

using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Projection;

namespace Blazor.DOM.CSharpGenerator.Emitters;

public sealed record CallbackEmitResult(
    string Source,
    IReadOnlyList<MemberOutcome> MemberOutcomes,
    IReadOnlyList<DeclarationOutcome>? DeclarationOutcomes = null,
    IReadOnlyList<OverloadOutcome>? OverloadOutcomes = null);

public sealed class CallbackEmitException(
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

public sealed class CallbackEmitter(TypeResolver typeResolver, string generatorVersion, string ns)
{
    private static readonly IReadOnlySet<string> EmittedDeclarationKinds =
        new HashSet<string>(["interface", "typeAlias"], StringComparer.Ordinal);

    public string Emit(SymbolModel symbol)
    {
        try
        {
            return EmitCore(symbol).Source;
        }
        catch (GenericDeferralException)
        {
            throw;
        }
        catch (CallbackEmitException exception)
        {
            throw new TypeProjectionException(
                exception.Message,
                exception.Provenance);
        }
    }

    public CallbackEmitResult EmitWithOutcomes(SymbolModel symbol)
    {
        try
        {
            var result = EmitCore(symbol);
            var outcomes = EmitterOutcomeReconciler.CompleteSuccess(
                symbol,
                result.MemberOutcomes,
                EmittedDeclarationKinds,
                result.OverloadOutcomes);
            return result with
            {
                MemberOutcomes = outcomes.MemberOutcomes,
                DeclarationOutcomes = outcomes.DeclarationOutcomes,
                OverloadOutcomes = outcomes.OverloadOutcomes,
            };
        }
        catch (GenericDeferralException)
        {
            throw;
        }
        catch (CallbackEmitException exception)
        {
            throw CompleteFailure(
                symbol,
                exception.Message,
                exception.Provenance,
                exception.PartialOutcomes,
                exception.PartialOverloadOutcomes);
        }
        catch (MemberOutcomeReconciliationException exception)
        {
            throw CompleteFailure(
                symbol,
                exception.Message,
                exception.Provenance,
                exception.PartialOutcomes,
                []);
        }
        catch (TypeProjectionException exception)
        {
            throw CompleteFailure(
                symbol,
                exception.Message,
                exception.Provenance,
                [],
                []);
        }
        catch (Exception exception)
        {
            throw CompleteFailure(
                symbol,
                exception.Message,
                $"{symbol.Name}/callback-emitter",
                [],
                []);
        }
    }

    private CallbackEmitResult EmitCore(SymbolModel symbol)
    {
        var sourceMembers = SourceAccountingShape.GetMembers(symbol);
        var sourceOverloads = SourceAccountingShape.GetOverloads(
            symbol,
            sourceMembers);
        var memberOutcomes = new List<MemberOutcome>();
        var overloadOutcomes = new List<OverloadOutcome>();
        var generic = typeResolver.CreateGenericDeclaration(
            symbol,
            symbol.Name);

        var objectOverloads = sourceOverloads
            .Where(overload => overload.SourceMember?.CallbackObjectForm == true)
            .ToList();

        var directOverloads = sourceOverloads
            .Where(IsDirectCallbackSignature)
            .ToList();
        if (directOverloads.Count == 0 && objectOverloads.Count == 0)
        {
            throw new CallbackEmitException(
                $"CallbackEmitter: '{symbol.Name}' has no direct call, construct, or function signature. " +
                "No callable function or callback-object signature was found.",
                $"{symbol.Name}/callback-signature",
                memberOutcomes,
                partialOverloadOutcomes: overloadOutcomes);
        }

        var hasFunction = directOverloads.Count > 0;
        var primary = directOverloads.FirstOrDefault();
        if (primary?.TypeParameters.Count > 0)
        {
            throw new GenericDeferralException(
                $"Callback signature at '{primary.Provenance}' declares its own " +
                "generic parameters; a C# delegate cannot preserve generic Invoke arity.",
                primary.Provenance,
                "generic-callback-signature");
        }
        string returnType = "";
        string parameterList = "";
        if (primary is not null)
        {
            try
            {
                (returnType, parameterList, var outcome) = ProjectSignature(
                    primary,
                    generic.Scope);
                overloadOutcomes.Add(outcome);
                if (primary.SourceMember is not null)
                    memberOutcomes.Add(CreateMemberOutcome(
                        primary.SourceMember,
                        MemberOutcomeStatus.Projected,
                        null,
                        null));
            }
            catch (CallbackSignatureProjectionException exception)
            {
                overloadOutcomes.Add(exception.Outcome);
                if (primary.SourceMember is not null)
                {
                    memberOutcomes.Add(CreateMemberOutcome(
                        primary.SourceMember,
                        MemberOutcomeStatus.Failed,
                        null,
                        exception.Message));
                }

                throw new CallbackEmitException(
                    exception.Message,
                    exception.Provenance,
                    memberOutcomes,
                    partialOverloadOutcomes: overloadOutcomes);
            }
        }

        foreach (var additional in directOverloads.Skip(1))
        {
            const string phase = "callback-overloads";
            const string reason =
                "Additional callback signatures are deferred because a C# delegate has one Invoke signature.";
            overloadOutcomes.Add(CreateOverloadOutcome(
                additional,
                MemberOutcomeStatus.Deferred,
                phase,
                reason,
                CreateParameterOutcomes(
                    additional,
                    MemberOutcomeStatus.Deferred,
                    phase,
                    reason)));
            if (additional.SourceMember is not null)
            {
                memberOutcomes.Add(CreateMemberOutcome(
                    additional.SourceMember,
                    MemberOutcomeStatus.Deferred,
                    phase,
                    reason));
            }
        }

        var objectMethods = new List<CallbackObjectMethod>();
        foreach (var objectOverload in objectOverloads)
        {
            try
            {
                var (objectReturn, objectParameters, objectOutcome) =
                    ProjectSignature(objectOverload, generic.Scope);
                overloadOutcomes.Add(objectOutcome);
                var sourceMember = objectOverload.SourceMember!;
                memberOutcomes.Add(CreateMemberOutcome(
                    sourceMember,
                    MemberOutcomeStatus.Projected,
                    null,
                    "Emitted as a typed callback-object interface arm."));
                objectMethods.Add(new CallbackObjectMethod(
                    Naming.ToCSharpMemberName(objectOverload.Name),
                    objectReturn,
                    objectParameters));
            }
            catch (CallbackSignatureProjectionException exception)
            {
                overloadOutcomes.Add(exception.Outcome);
                var sourceMember = objectOverload.SourceMember!;
                memberOutcomes.Add(CreateMemberOutcome(
                    sourceMember,
                    MemberOutcomeStatus.Failed,
                    null,
                    exception.Message));
                throw new CallbackEmitException(
                    exception.Message,
                    exception.Provenance,
                    memberOutcomes,
                    partialOverloadOutcomes: overloadOutcomes);
            }
        }

        var accountedMemberKeys = memberOutcomes
            .Where(outcome => outcome.QualifiedKey is not null)
            .Select(outcome => outcome.QualifiedKey!)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var sourceMember in sourceMembers
            .Where(member =>
                !member.NestedTypeLiteral
                && !accountedMemberKeys.Contains(member.QualifiedKey)))
        {
            memberOutcomes.Add(CreateMemberOutcome(
                sourceMember,
                MemberOutcomeStatus.Failed,
                null,
                $"Callback member kind '{sourceMember.Member.Kind}' is not a callable signature."));
        }

        var unsupported = memberOutcomes.FirstOrDefault(
            outcome => outcome.Status == MemberOutcomeStatus.Failed);
        if (unsupported is not null)
        {
            throw new CallbackEmitException(
                unsupported.Reason ?? "Callback contains an unsupported member.",
                unsupported.Provenance ?? unsupported.QualifiedKey ?? symbol.Name,
                memberOutcomes,
                partialOverloadOutcomes: overloadOutcomes);
        }

        var writer = new CSharpWriter();
        writer.AppendLine("#nullable enable");
        writer.AppendLine(CSharpWriter.AutoGeneratedHeader(
            "Blazor.DOM.CSharpGenerator",
            generatorVersion));
        writer.AppendLine($"namespace {Naming.ToGeneratedNamespace(ns, symbol.Name)};");
        writer.AppendLine();
        writer.XmlDoc(GetDocText(symbol), IsDeprecated(symbol));
        if (symbol.Semantic.SecureContext)
            writer.AppendLine("// Requires secure context (HTTPS).");
        foreach (var defaultNote in generic.DefaultNotes)
            writer.AppendLine($"// TypeScript generic default: {defaultNote}.");

        var csName = Naming.ToCSharpSimpleTypeName(symbol.Name);
        if (objectMethods.Count == 0)
        {
            writer.AppendLine(
                $"public delegate {returnType} {csName}{generic.TypeParameterList}(" +
                $"{parameterList}){generic.ConstraintSuffix};");
        }
        else
        {
            var functionName = $"{csName}Function";
            var objectName = $"I{csName}CallbackObject";
            if (hasFunction)
            {
                writer.AppendLine(
                    $"public delegate {returnType} {functionName}{generic.TypeParameterList}(" +
                    $"{parameterList}){generic.ConstraintSuffix};");
                writer.AppendLine();
            }
            writer.Block(
                $"public interface {objectName}{generic.TypeParameterList}{generic.ConstraintSuffix}",
                () =>
                {
                    foreach (var method in objectMethods)
                    {
                        writer.AppendLine(
                            $"{method.ReturnType} {method.Name}({method.Parameters});");
                    }
                });
            writer.AppendLine();
            writer.Block(
                $"public readonly struct {csName}{generic.TypeParameterList}" +
                $"{generic.ConstraintSuffix}",
                () =>
                {
                    if (hasFunction)
                        writer.AppendLine($"private readonly {functionName}{generic.TypeParameterList}? _function;");
                    writer.AppendLine($"private readonly {objectName}{generic.TypeParameterList}? _callbackObject;");
                    writer.AppendLine();
                    if (hasFunction)
                    {
                        writer.AppendLine($"private {csName}({functionName}{generic.TypeParameterList} function)");
                        writer.OpenBrace();
                        writer.AppendLine("_function = function ?? throw new ArgumentNullException(nameof(function));");
                        writer.AppendLine("_callbackObject = null;");
                        writer.CloseBrace();
                        writer.AppendLine();
                    }
                    writer.AppendLine($"private {csName}({objectName}{generic.TypeParameterList} callbackObject)");
                    writer.OpenBrace();
                    if (hasFunction)
                        writer.AppendLine("_function = null;");
                    writer.AppendLine("_callbackObject = callbackObject ?? throw new ArgumentNullException(nameof(callbackObject));");
                    writer.CloseBrace();
                    writer.AppendLine();
                    if (hasFunction)
                    {
                        writer.AppendLine($"public static {csName}{generic.TypeParameterList} FromFunction(" +
                            $"{functionName}{generic.TypeParameterList} function) => new(function);");
                    }
                    writer.AppendLine($"public static {csName}{generic.TypeParameterList} FromCallbackObject(" +
                        $"{objectName}{generic.TypeParameterList} callbackObject) => new(callbackObject);");
                    if (hasFunction)
                        writer.AppendLine("public bool IsFunction => _function is not null;");
                    writer.AppendLine("public bool IsCallbackObject => _callbackObject is not null;");
                    if (hasFunction)
                    {
                        writer.AppendLine($"public {functionName}{generic.TypeParameterList} GetFunction() => _function " +
                            "?? throw new InvalidOperationException(\"The callback contains an object arm.\");");
                    }
                    writer.AppendLine($"public {objectName}{generic.TypeParameterList} GetCallbackObject() => _callbackObject " +
                        "?? throw new InvalidOperationException(\"The callback contains a function arm.\");");
                });
        }
        return new CallbackEmitResult(
            writer.ToString(),
            memberOutcomes,
            OverloadOutcomes: overloadOutcomes);
    }

    private sealed record CallbackObjectMethod(
        string Name,
        string ReturnType,
        string Parameters);

    private static bool IsDirectCallbackSignature(SourceOverloadShape source)
        => source.Kind == "function"
            || (source.SourceMember?.CallbackObjectForm == false
                && source.Kind is "callSignature" or "constructSignature");

    private (string ReturnType, string Parameters, OverloadOutcome Outcome) ProjectSignature(
        SourceOverloadShape source,
        GenericScope declarationScope)
    {
        string returnType;
        try
        {
            var returnProjection = source.ReturnType is null
                ? null
                : typeResolver.Project(
                    source.ReturnType,
                    $"{source.Provenance}/return",
                    declarationScope);
            if (returnProjection?.Identity.Kind == ClrTypeKind.Null)
            {
                throw new TypeProjectionException(
                    $"Callback return at '{source.Provenance}' resolves to illegal " +
                    $"standalone CLR type '{returnProjection.RenderedType}'.",
                    $"{source.Provenance}/return");
            }
            returnType = returnProjection?.RenderedType ?? "void";
        }
        catch (GenericDeferralException)
        {
            throw;
        }
        catch (TypeProjectionException exception)
        {
            var unattemptedParameters = CreateParameterOutcomes(
                source,
                MemberOutcomeStatus.NotAttemptedAfterFailure,
                null,
                $"Not attempted because callback return projection failed: {exception.Message}");
            throw new CallbackSignatureProjectionException(
                exception.Message,
                exception.Provenance,
                CreateOverloadOutcome(
                    source,
                    MemberOutcomeStatus.Failed,
                    null,
                    exception.Message,
                    unattemptedParameters));
        }

        var parts = new List<string>();
        var parameterOutcomes = new List<ParameterOutcome>();
        var parameters = source.Parameters.OrderBy(parameter => parameter.Ordinal).ToList();
        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            var provenance =
                $"{source.Provenance}/parameter[{parameter.Ordinal}]/{parameter.Name}";
            try
            {
                var projection = typeResolver.Project(
                    parameter.Type,
                    provenance,
                    declarationScope);
                if (projection.Identity.Kind is ClrTypeKind.Null or ClrTypeKind.Void)
                {
                    throw new TypeProjectionException(
                        $"Callback parameter '{parameter.Name}' resolves to illegal CLR " +
                        $"type '{projection.RenderedType}'.",
                        provenance);
                }
                var csType = projection.RenderedType;
                var csName = Naming.ToCSharpParameterName(parameter.Name);
                if (parameter.Rest)
                {
                    var elementType = csType.EndsWith("[]", StringComparison.Ordinal)
                        ? csType[..^2]
                        : csType;
                    parts.Add($"params {elementType}[] {csName}");
                }
                else if (parameter.Optional)
                {
                    var optionalProjection = projection with
                    {
                        IsNullable = projection.IsNullable
                            || projection.Identity.Kind == ClrTypeKind.Reference,
                    };
                    parts.Add(
                        $"{optionalProjection.RenderedType} {csName} = default");
                }
                else
                {
                    parts.Add($"{csType} {csName}");
                }

                parameterOutcomes.Add(CreateParameterOutcome(
                    source,
                    parameter,
                    MemberOutcomeStatus.Projected,
                    null,
                    "projected"));
            }
            catch (GenericDeferralException)
            {
                throw;
            }
            catch (TypeProjectionException exception)
            {
                parameterOutcomes.Add(CreateParameterOutcome(
                    source,
                    parameter,
                    MemberOutcomeStatus.Failed,
                    null,
                    exception.Message));
                parameterOutcomes.AddRange(parameters
                    .Skip(index + 1)
                    .Select(later => CreateParameterOutcome(
                        source,
                        later,
                        MemberOutcomeStatus.NotAttemptedAfterFailure,
                        null,
                        $"Not attempted because parameter '{parameter.Name}' failed: {exception.Message}")));
                throw new CallbackSignatureProjectionException(
                    exception.Message,
                    exception.Provenance,
                    CreateOverloadOutcome(
                        source,
                        MemberOutcomeStatus.Failed,
                        null,
                        exception.Message,
                        parameterOutcomes));
            }
        }

        return (
            returnType,
            string.Join(", ", parts),
            CreateOverloadOutcome(
                source,
                MemberOutcomeStatus.Projected,
                null,
                "projected",
                parameterOutcomes));
    }

    private static CallbackEmitException CompleteFailure(
        SymbolModel symbol,
        string message,
        string provenance,
        IReadOnlyList<MemberOutcome> partialMemberOutcomes,
        IReadOnlyList<OverloadOutcome> partialOverloadOutcomes)
    {
        var outcomes = EmitterOutcomeReconciler.CompleteFailure(
            symbol,
            partialMemberOutcomes,
            EmittedDeclarationKinds,
            message,
            provenance,
            partialOverloadOutcomes);
        return new CallbackEmitException(
            message,
            provenance,
            outcomes.MemberOutcomes,
            outcomes.DeclarationOutcomes,
            outcomes.OverloadOutcomes);
    }

    private static MemberOutcome CreateMemberOutcome(
        SourceMemberShape source,
        MemberOutcomeStatus status,
        string? phase,
        string? reason)
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
            .Select(parameter => CreateParameterOutcome(
                source,
                parameter,
                status,
                phase,
                reason))
            .ToList();

    private static ParameterOutcome CreateParameterOutcome(
        SourceOverloadShape source,
        ParameterModel parameter,
        MemberOutcomeStatus status,
        string? phase,
        string reason)
        => new(
            parameter.Ordinal,
            parameter.Name,
            status,
            phase,
            reason,
            $"{source.Provenance}/parameter[{parameter.Ordinal}]/{parameter.Name}",
            SourceAccountingShape.FormatLocation(parameter.Location));

    private static string GetDocText(SymbolModel symbol)
        => symbol.Declarations
            .Select(declaration => declaration.Documentation?.Text)
            .FirstOrDefault(text => !string.IsNullOrEmpty(text)) ?? "";

    private static bool IsDeprecated(SymbolModel symbol)
        => symbol.Declarations.Any(
            declaration => declaration.Documentation?.Deprecated ?? false);

    private sealed class CallbackSignatureProjectionException(
        string message,
        string provenance,
        OverloadOutcome outcome)
        : TypeProjectionException(message, provenance)
    {
        public OverloadOutcome Outcome { get; } = outcome;
    }
}
