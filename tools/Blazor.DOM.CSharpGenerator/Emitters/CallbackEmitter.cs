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
    private const string CallbackObjectPhase = "callback-object-form";
    private const string CallbackObjectReason =
        "Type-literal callback object arms are deferred because a C# delegate " +
        "represents only a direct function signature.";

    private static readonly IReadOnlySet<string> EmittedDeclarationKinds =
        new HashSet<string>(["interface", "typeAlias"], StringComparer.Ordinal);

    public string Emit(SymbolModel symbol)
    {
        try
        {
            return EmitCore(symbol).Source;
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

        foreach (var objectMember in sourceMembers
            .Where(member => member.CallbackObjectForm))
        {
            memberOutcomes.Add(CreateMemberOutcome(
                objectMember,
                MemberOutcomeStatus.Deferred,
                CallbackObjectPhase,
                CallbackObjectReason));
        }

        foreach (var objectOverload in sourceOverloads
            .Where(overload => overload.SourceMember?.CallbackObjectForm == true))
        {
            overloadOutcomes.Add(CreateOverloadOutcome(
                objectOverload,
                MemberOutcomeStatus.Deferred,
                CallbackObjectPhase,
                CallbackObjectReason,
                CreateParameterOutcomes(
                    objectOverload,
                    MemberOutcomeStatus.Deferred,
                    CallbackObjectPhase,
                    CallbackObjectReason)));
        }

        var directOverloads = sourceOverloads
            .Where(IsDirectCallbackSignature)
            .ToList();
        if (directOverloads.Count == 0)
        {
            throw new CallbackEmitException(
                $"CallbackEmitter: '{symbol.Name}' has no direct call, construct, or function signature. " +
                "Type-literal object forms cannot be represented by a C# delegate.",
                $"{symbol.Name}/callback-signature",
                memberOutcomes,
                partialOverloadOutcomes: overloadOutcomes);
        }

        var primary = directOverloads[0];
        string returnType;
        string parameterList;
        try
        {
            (returnType, parameterList, var outcome) = ProjectSignature(primary);
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
        writer.AppendLine($"namespace {ns};");
        writer.AppendLine();
        writer.XmlDoc(GetDocText(symbol), IsDeprecated(symbol));
        if (symbol.Semantic.SecureContext)
            writer.AppendLine("// Requires secure context (HTTPS).");

        var csName = Naming.ToCSharpTypeName(symbol.Name);
        writer.AppendLine($"public delegate {returnType} {csName}({parameterList});");
        return new CallbackEmitResult(
            writer.ToString(),
            memberOutcomes,
            OverloadOutcomes: overloadOutcomes);
    }

    private static bool IsDirectCallbackSignature(SourceOverloadShape source)
        => source.Kind == "function"
            || (source.SourceMember?.CallbackObjectForm == false
                && source.Kind is "callSignature" or "constructSignature");

    private (string ReturnType, string Parameters, OverloadOutcome Outcome) ProjectSignature(
        SourceOverloadShape source)
    {
        string returnType;
        try
        {
            returnType = source.ReturnType is null
                ? "void"
                : typeResolver.Project(
                    source.ReturnType,
                    $"{source.Provenance}/return").RenderedType;
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
                var projection = typeResolver.Project(parameter.Type, provenance);
                var csType = projection.RenderedType;
                var csName = Naming.ToCSharpParameterName(parameter.Name);
                if (parameter.Rest)
                {
                    var elementType = csType.EndsWith("[]", StringComparison.Ordinal)
                        ? csType[..^2]
                        : csType;
                    parts.Add($"params {elementType}[] {csName}");
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
