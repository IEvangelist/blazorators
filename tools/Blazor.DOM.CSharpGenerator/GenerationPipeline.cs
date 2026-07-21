// Main generation pipeline: routes every TypeScript declaration by shape,
// emits logical contracts, and reconciles exact source accounting.
// FAIL-CLOSED: generation with any failure exits nonzero.

using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.Emitters;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Hosts;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Projection;

namespace Blazor.DOM.CSharpGenerator;

public sealed class GenerationPipeline
{
    public const string GeneratorVersion = "1.0.0";
    public const string GeneratedNamespace = "Blazor.DOM";

    private static readonly IReadOnlySet<string> TypeAliasDeclarationKinds =
        new HashSet<string>(["typeAlias"], StringComparer.Ordinal);

    public static GenerationResult Run(
        IrBundle ir,
        string outputDirectory,
        IReadOnlyDictionary<string, EmitterOverrideEntry>? overrides = null,
        bool verboseFailures = false,
        bool emitHosts = false,
        HostPackageOptions? hostPackageOptions = null)
    {
        _ = verboseFailures;
        overrides ??= new Dictionary<string, EmitterOverrideEntry>(
            StringComparer.Ordinal);

        var ledger = new AccountingLedger();
        var resolver = new TypeResolver(
            ir.TypescriptSymbols,
            overrides,
            GeneratedNamespace);
        var writer = new OutputWriter(outputDirectory);
        var routingPlan = DeclarationRouter.Create(
            ir.TypescriptSymbols,
            overrides);

        var dictionaryEmitter = new DictionaryEmitter(
            resolver,
            GeneratorVersion,
            GeneratedNamespace);
        var aliasEmitter = new AliasEmitter(
            resolver,
            GeneratorVersion,
            GeneratedNamespace);
        var callbackEmitter = new CallbackEmitter(
            resolver,
            GeneratorVersion,
            GeneratedNamespace);
        var interfaceEmitter = new InterfaceEmitter(
            resolver,
            GeneratorVersion,
            GeneratedNamespace,
            routingPlan);
        var eventMapEmitter = new EventMapEmitter(
            resolver,
            GeneratorVersion,
            GeneratedNamespace,
            ir.TypescriptSymbols);

        var primaryEmissions = new Dictionary<string, PrimarySymbolEmission>(
            StringComparer.Ordinal);
        foreach (var symbol in ir.TypescriptSymbols.OrderBy(symbol => symbol.Ordinal))
        {
            primaryEmissions.Add(
                symbol.Name,
                EmitPrimary(
                    symbol,
                    routingPlan.Get(symbol),
                    writer,
                    dictionaryEmitter,
                    aliasEmitter,
                    callbackEmitter,
                    interfaceEmitter,
                    eventMapEmitter,
                    overrides));
        }

        var supplemental = new GlobalNamespaceEmitter(
            resolver,
            GeneratorVersion,
            GeneratedNamespace,
            routingPlan,
            primaryEmissions).Emit(writer);

        HostPackageGenerationResult? hostPackages = null;
        if (emitHosts)
        {
            hostPackages = HostPackageEmitter.Emit(
                ir,
                writer,
                overrides,
                resolver,
                routingPlan,
                interfaceEmitter,
                hostPackageOptions);
        }

        foreach (var synthesized in resolver.SynthesizedTypes)
        {
            writer.Write(
                synthesized.Name,
                synthesized.Source,
                "AdvancedTypes");
        }

        var errors = new List<GenerationError>();
        foreach (var symbol in ir.TypescriptSymbols.OrderBy(symbol => symbol.Ordinal))
        {
            ReconcileSymbol(
                symbol,
                routingPlan.Get(symbol),
                primaryEmissions[symbol.Name],
                supplemental.Symbols.GetValueOrDefault(
                    symbol.Name,
                    SupplementalSymbolEmission.None),
                ledger,
                errors);
        }

        var validation = ledger.Validate(ir.TypescriptSymbols.Count);
        var manifest = ledger.BuildManifest(GeneratorVersion, ir.Manifest) with
        {
            SynthesizedTypes = resolver.SynthesizedTypes
                .Select(type => new SynthesizedTypeManifestEntry(
                    type.Name,
                    type.Kind,
                    type.Provenance,
                    type.Fingerprint,
                    type.RelativePath))
                .ToList(),
        };
        writer.WriteManifest(manifest);

        return new GenerationResult(
            validation,
            writer.WrittenFiles,
            errors,
            manifest,
            hostPackages);
    }

    private static PrimarySymbolEmission EmitPrimary(
        SymbolModel symbol,
        SymbolRouting routing,
        OutputWriter writer,
        DictionaryEmitter dictionaryEmitter,
        AliasEmitter aliasEmitter,
        CallbackEmitter callbackEmitter,
        InterfaceEmitter interfaceEmitter,
        EventMapEmitter eventMapEmitter,
        IReadOnlyDictionary<string, EmitterOverrideEntry> overrides)
    {
        if (symbol.Semantic.Status == "ambiguous"
            && !overrides.ContainsKey(symbol.Name))
        {
            return new PrimarySymbolEmission(
                SymbolEmissionDisposition.Failed,
                Reason:
                    $"Ambiguous symbol '{symbol.Name}' has no explicit override " +
                    "in emitter-overrides.json. Add a reviewed classification " +
                    "with a non-empty rationale.",
                ExceptionType: "AmbiguousSymbolException");
        }

        if (routing.FailureReason is not null)
        {
            return new PrimarySymbolEmission(
                SymbolEmissionDisposition.Failed,
                Reason: routing.FailureReason,
                ExceptionType: "DeclarationRoutingException");
        }

        if (routing.PrimaryRoute is null)
            return PrimarySymbolEmission.None;

        var primaryDeclarations = routing.Declarations.Where(route =>
            route.Route == routing.PrimaryRoute).ToList();
        if (primaryDeclarations.Any(route =>
            route.Declaration.EventMap.IsEventMap))
        {
            return EmitEventMap(symbol, writer, eventMapEmitter);
        }

        if (symbol.Semantic.ExposedOnWorker
            && !symbol.Semantic.ExposedOnWindow
            && symbol.Semantic.Exposures.Count > 0)
        {
            return new PrimarySymbolEmission(
                SymbolEmissionDisposition.Excluded,
                Reason:
                    "Worker-only: symbol is exposed exclusively on Worker scope " +
                    "(not Window).");
        }

        return routing.PrimaryRoute switch
        {
            DeclarationRouteKind.Enum => EmitEnum(symbol, writer),
            DeclarationRouteKind.Dictionary => EmitDictionary(
                symbol,
                writer,
                dictionaryEmitter),
            DeclarationRouteKind.Typedef => EmitTypedef(
                symbol,
                writer,
                aliasEmitter),
            DeclarationRouteKind.Callback => EmitCallback(
                symbol,
                writer,
                callbackEmitter),
            DeclarationRouteKind.Interface => EmitInterface(
                symbol,
                writer,
                interfaceEmitter),
            _ => new PrimarySymbolEmission(
                SymbolEmissionDisposition.Failed,
                Reason:
                    $"Primary route '{routing.PrimaryRoute}' is not handled for " +
                    $"'{symbol.Name}'.",
                ExceptionType: "DeclarationRoutingException"),
        };
    }

    private static PrimarySymbolEmission EmitEventMap(
        SymbolModel symbol,
        OutputWriter writer,
        EventMapEmitter emitter)
    {
        try
        {
            var result = emitter.Emit(symbol);
            var path = writer.Write(
                Naming.ToCSharpSimpleTypeName(symbol.Name),
                result.Source,
                Naming.ToOutputSubdirectory("EventMaps", symbol.Name));
            return new PrimarySymbolEmission(
                SymbolEmissionDisposition.Projected,
                path,
                MemberOutcomes: result.MemberOutcomes,
                DeclarationOutcomes: result.DeclarationOutcomes,
                OverloadOutcomes: result.OverloadOutcomes);
        }
        catch (Exception exception)
        {
            return CompletePrimaryFailure(
                symbol,
                exception,
                new HashSet<string>(["interface"], StringComparer.Ordinal),
                $"{symbol.Name}/event-map-emitter");
        }
    }

    private static PrimarySymbolEmission EmitEnum(
        SymbolModel symbol,
        OutputWriter writer)
    {
        try
        {
            var source = EnumEmitter.Emit(
                symbol,
                GeneratorVersion,
                GeneratedNamespace);
            var path = writer.Write(
                Naming.ToCSharpSimpleTypeName(symbol.Name),
                source,
                Naming.ToOutputSubdirectory("Enums", symbol.Name));
            var outcomes = EmitterOutcomeReconciler.CompleteSuccess(
                symbol,
                [],
                TypeAliasDeclarationKinds);
            return Projected(path, outcomes);
        }
        catch (GenericDeferralException exception)
        {
            return Deferred(exception);
        }
        catch (Exception exception)
        {
            return CompletePrimaryFailure(
                symbol,
                exception,
                TypeAliasDeclarationKinds,
                $"{symbol.Name}/enum-emitter");
        }
    }

    private static PrimarySymbolEmission EmitDictionary(
        SymbolModel symbol,
        OutputWriter writer,
        DictionaryEmitter emitter)
    {
        try
        {
            var result = emitter.EmitWithOutcomes(symbol);
            var path = writer.Write(
                Naming.ToCSharpSimpleTypeName(symbol.Name),
                result.Source,
                Naming.ToOutputSubdirectory("Dictionaries", symbol.Name));
            return new PrimarySymbolEmission(
                SymbolEmissionDisposition.Projected,
                path,
                MemberOutcomes: result.MemberOutcomes,
                DeclarationOutcomes: result.DeclarationOutcomes,
                OverloadOutcomes: result.OverloadOutcomes);
        }
        catch (GenericDeferralException exception)
        {
            return Deferred(exception);
        }
        catch (DictionaryEmitException exception)
        {
            return Failed(
                exception,
                exception.PartialOutcomes,
                exception.PartialDeclarationOutcomes,
                exception.PartialOverloadOutcomes);
        }
        catch (Exception exception)
        {
            return CompletePrimaryFailure(
                symbol,
                exception,
                new HashSet<string>(
                    ["interface", "typeAlias"],
                    StringComparer.Ordinal),
                $"{symbol.Name}/dictionary-emitter");
        }
    }

    private static PrimarySymbolEmission EmitTypedef(
        SymbolModel symbol,
        OutputWriter writer,
        AliasEmitter emitter)
    {
        try
        {
            var source = emitter.Emit(symbol);
            var path = writer.Write(
                Naming.ToCSharpSimpleTypeName(symbol.Name),
                source,
                Naming.ToOutputSubdirectory("Typedefs", symbol.Name));
            var outcomes = EmitterOutcomeReconciler.CompleteSuccess(
                symbol,
                [],
                TypeAliasDeclarationKinds);
            return Projected(path, outcomes);
        }
        catch (GenericDeferralException exception)
        {
            return Deferred(exception);
        }
        catch (Exception exception)
        {
            return CompletePrimaryFailure(
                symbol,
                exception,
                TypeAliasDeclarationKinds,
                $"{symbol.Name}/typedef-emitter");
        }
    }

    private static PrimarySymbolEmission EmitCallback(
        SymbolModel symbol,
        OutputWriter writer,
        CallbackEmitter emitter)
    {
        try
        {
            var result = emitter.EmitWithOutcomes(symbol);
            var path = writer.Write(
                Naming.ToCSharpSimpleTypeName(symbol.Name),
                result.Source,
                Naming.ToOutputSubdirectory("Callbacks", symbol.Name));
            return new PrimarySymbolEmission(
                SymbolEmissionDisposition.Projected,
                path,
                MemberOutcomes: result.MemberOutcomes,
                DeclarationOutcomes: result.DeclarationOutcomes,
                OverloadOutcomes: result.OverloadOutcomes);
        }
        catch (GenericDeferralException exception)
        {
            return Deferred(exception);
        }
        catch (CallbackEmitException exception)
        {
            return Failed(
                exception,
                exception.PartialOutcomes,
                exception.PartialDeclarationOutcomes,
                exception.PartialOverloadOutcomes);
        }
        catch (Exception exception)
        {
            return CompletePrimaryFailure(
                symbol,
                exception,
                new HashSet<string>(
                    ["interface", "typeAlias"],
                    StringComparer.Ordinal),
                $"{symbol.Name}/callback-emitter");
        }
    }

    private static PrimarySymbolEmission EmitInterface(
        SymbolModel symbol,
        OutputWriter writer,
        InterfaceEmitter emitter)
    {
        try
        {
            var result = emitter.Emit(symbol);
            var path = writer.Write(
                $"I{Naming.ToCSharpSimpleTypeName(symbol.Name)}",
                result.Source,
                Naming.ToOutputSubdirectory("Interfaces", symbol.Name));
            return new PrimarySymbolEmission(
                SymbolEmissionDisposition.Projected,
                path,
                MemberOutcomes: result.MemberOutcomes,
                DeclarationOutcomes: result.DeclarationOutcomes,
                OverloadOutcomes: result.OverloadOutcomes);
        }
        catch (GenericDeferralException exception)
        {
            return Deferred(exception);
        }
        catch (InterfaceEmitException exception)
        {
            return Failed(
                exception,
                exception.PartialOutcomes,
                exception.PartialDeclarationOutcomes,
                exception.PartialOverloadOutcomes);
        }
        catch (Exception exception)
        {
            return CompletePrimaryFailure(
                symbol,
                exception,
                new HashSet<string>(["interface"], StringComparer.Ordinal),
                $"{symbol.Name}/interface-emitter");
        }
    }

    private static PrimarySymbolEmission Projected(
        string path,
        StructuredEmitterOutcomes outcomes)
        => new(
            SymbolEmissionDisposition.Projected,
            path,
            MemberOutcomes: outcomes.MemberOutcomes,
            DeclarationOutcomes: outcomes.DeclarationOutcomes,
            OverloadOutcomes: outcomes.OverloadOutcomes);

    private static PrimarySymbolEmission Failed(
        Exception exception,
        IReadOnlyList<MemberOutcome> members,
        IReadOnlyList<DeclarationOutcome> declarations,
        IReadOnlyList<OverloadOutcome> overloads)
        => new(
            SymbolEmissionDisposition.Failed,
            Reason: exception.Message,
            ExceptionType: exception.GetType().Name,
            MemberOutcomes: members,
            DeclarationOutcomes: declarations,
            OverloadOutcomes: overloads);

    private static PrimarySymbolEmission Deferred(
        GenericDeferralException exception)
        => new(
            SymbolEmissionDisposition.Deferred,
            Phase: exception.Phase,
            Reason: exception.Message);

    private static PrimarySymbolEmission CompletePrimaryFailure(
        SymbolModel symbol,
        Exception exception,
        IReadOnlySet<string> emittedKinds,
        string provenance)
    {
        var projection = exception as TypeProjectionException;
        var outcomes = EmitterOutcomeReconciler.CompleteFailure(
            symbol,
            [],
            emittedKinds,
            exception.Message,
            projection?.Provenance ?? provenance);
        return new PrimarySymbolEmission(
            SymbolEmissionDisposition.Failed,
            Reason: exception.Message,
            ExceptionType: exception.GetType().Name,
            MemberOutcomes: outcomes.MemberOutcomes,
            DeclarationOutcomes: outcomes.DeclarationOutcomes,
            OverloadOutcomes: outcomes.OverloadOutcomes);
    }

    private static void ReconcileSymbol(
        SymbolModel symbol,
        SymbolRouting routing,
        PrimarySymbolEmission primary,
        SupplementalSymbolEmission supplemental,
        AccountingLedger ledger,
        List<GenerationError> errors)
    {
        var declarations = MergeDeclarations(
            primary.DeclarationOutcomes,
            supplemental.DeclarationOutcomes);
        var members = MergeMembers(
            primary.MemberOutcomes,
            supplemental.MemberOutcomes);
        var overloads = MergeOverloads(
            primary.OverloadOutcomes,
            supplemental.OverloadOutcomes);
        var failureReasons = new[]
        {
            routing.FailureReason,
            primary.Disposition == SymbolEmissionDisposition.Failed
                ? primary.Reason
                : null,
            supplemental.FailureReason,
        }
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.Ordinal)
            .Cast<string>()
            .ToList();

        if (failureReasons.Count > 0)
        {
            var reason = string.Join(" | ", failureReasons);
            ledger.RecordFailed(
                symbol,
                reason,
                members,
                declarations,
                overloads);
            errors.Add(new GenerationError(
                symbol.Name,
                reason,
                supplemental.ExceptionType
                    ?? primary.ExceptionType
                    ?? "DeclarationRoutingException"));
            return;
        }

        if (primary.Disposition == SymbolEmissionDisposition.Deferred)
        {
            ledger.RecordDeferred(
                symbol,
                primary.Phase ?? "declaration-routing",
                primary.Reason ?? "The primary declaration is deferred.",
                members,
                declarations,
                overloads);
            return;
        }

        var files = new[] { primary.GeneratedFile }
            .Where(path => path is not null)
            .Cast<string>()
            .Concat(supplemental.GeneratedFiles)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var projectedOutcome = declarations.Any(outcome =>
                outcome.Status == MemberOutcomeStatus.Projected)
            || members.Any(outcome =>
                outcome.Status == MemberOutcomeStatus.Projected)
            || overloads.Any(outcome =>
                outcome.Status == MemberOutcomeStatus.Projected);

        if (files.Count > 0 || projectedOutcome)
        {
            if (files.Count == 0)
            {
                var reason =
                    $"Symbol '{symbol.Name}' has projected source outcomes but no " +
                    "generated or canonical merged file.";
                ledger.RecordFailed(
                    symbol,
                    reason,
                    members,
                    declarations,
                    overloads);
                errors.Add(new GenerationError(
                    symbol.Name,
                    reason,
                    "MissingGeneratedContractException"));
                return;
            }

            ledger.RecordProjected(
                symbol,
                files[0],
                members,
                declarations,
                overloads);
            return;
        }

        if (primary.Disposition == SymbolEmissionDisposition.Excluded)
        {
            ledger.RecordExcluded(
                symbol,
                primary.Reason ?? "Excluded from the Window profile.",
                members,
                declarations,
                overloads);
            return;
        }

        var phases = declarations
            .Where(outcome => outcome.Status == MemberOutcomeStatus.Deferred)
            .Select(outcome => outcome.Phase)
            .Concat(members
                .Where(outcome => outcome.Status == MemberOutcomeStatus.Deferred)
                .Select(outcome => outcome.Phase))
            .Concat(overloads
                .Where(outcome => outcome.Status == MemberOutcomeStatus.Deferred)
                .Select(outcome => outcome.Phase))
            .Append(primary.Phase)
            .Where(phase => !string.IsNullOrWhiteSpace(phase))
            .Distinct(StringComparer.Ordinal)
            .Cast<string>()
            .ToList();
        var phase = phases.Count == 1 ? phases[0] : "declaration-routing";
        var reasons = declarations
            .Where(outcome => outcome.Status == MemberOutcomeStatus.Deferred)
            .Select(outcome => outcome.Reason)
            .Concat(overloads
                .Where(outcome => outcome.Status == MemberOutcomeStatus.Deferred)
                .Select(outcome => outcome.Reason))
            .Append(primary.Reason)
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.Ordinal)
            .Cast<string>()
            .ToList();
        ledger.RecordDeferred(
            symbol,
            phase,
            reasons.Count == 0
                ? "All routed declarations are explicitly deferred."
                : string.Join(" | ", reasons),
            members,
            declarations,
            overloads);
    }

    private static IReadOnlyList<DeclarationOutcome> MergeDeclarations(
        IReadOnlyList<DeclarationOutcome>? primary,
        IReadOnlyList<DeclarationOutcome> supplemental)
    {
        var result = (primary ?? [])
            .ToDictionary(outcome => outcome.Ordinal);
        foreach (var outcome in supplemental)
            result[outcome.Ordinal] = outcome;
        return result.Values
            .OrderBy(outcome => outcome.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<MemberOutcome> MergeMembers(
        IReadOnlyList<MemberOutcome>? primary,
        IReadOnlyList<MemberOutcome> supplemental)
    {
        var result = (primary ?? [])
            .ToDictionary(MemberKey, StringComparer.Ordinal);
        foreach (var outcome in supplemental)
            result[MemberKey(outcome)] = outcome;
        return result.Values
            .OrderBy(outcome => outcome.DeclarationOrdinal)
            .ThenBy(outcome => outcome.Ordinal)
            .ThenBy(MemberKey, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<OverloadOutcome> MergeOverloads(
        IReadOnlyList<OverloadOutcome>? primary,
        IReadOnlyList<OverloadOutcome> supplemental)
    {
        var result = (primary ?? [])
            .ToDictionary(
                outcome => outcome.QualifiedKey,
                StringComparer.Ordinal);
        foreach (var outcome in supplemental)
            result[outcome.QualifiedKey] = outcome;
        return result.Values
            .OrderBy(outcome => outcome.DeclarationOrdinal)
            .ThenBy(outcome => outcome.MemberOrdinal)
            .ThenBy(outcome => outcome.QualifiedKey, StringComparer.Ordinal)
            .ToList();
    }

    private static string MemberKey(MemberOutcome outcome)
        => outcome.QualifiedKey
            ?? $"decl[{outcome.DeclarationOrdinal}]/member[{outcome.Ordinal}]";
}

public sealed record GenerationResult(
    AccountingValidationResult Validation,
    IReadOnlyList<GeneratedFile> WrittenFiles,
    IReadOnlyList<GenerationError> Errors,
    EmitterManifest Manifest,
    HostPackageGenerationResult? HostPackages = null);

public sealed record GenerationError(
    string SymbolName,
    string Message,
    string ExceptionType);
