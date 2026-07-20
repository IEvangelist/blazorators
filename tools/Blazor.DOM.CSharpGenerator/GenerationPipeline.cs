// Main generation pipeline: loads IR, classifies every symbol, routes to the
// correct emitter, tracks accounting, writes output, and verifies byte identity.
// FAIL-CLOSED: Generation with any failures exits nonzero.
// Ambiguous symbols fail unless emitter-overrides.json contains an explicit, rationale-backed entry.

using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.Emitters;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Projection;

namespace Blazor.DOM.CSharpGenerator;

public sealed class GenerationPipeline
{
    public const string GeneratorVersion = "1.0.0";
    public const string GeneratedNamespace = "Blazor.DOM";

    public static GenerationResult Run(
        IrBundle ir,
        string outputDirectory,
        IReadOnlyDictionary<string, EmitterOverrideEntry>? overrides = null,
        bool verboseFailures = false)
    {
        // If no overrides dict passed, use empty (all ambiguous symbols fail)
        overrides ??= new Dictionary<string, EmitterOverrideEntry>(StringComparer.Ordinal);

        var ledger = new AccountingLedger();
        var resolver = new TypeResolver(ir.TypescriptSymbols, overrides);
        var writer = new OutputWriter(outputDirectory);

        var dictEmitter = new DictionaryEmitter(resolver, GeneratorVersion, GeneratedNamespace);
        var aliasEmitter = new AliasEmitter(resolver, GeneratorVersion, GeneratedNamespace);
        var callbackEmitter = new CallbackEmitter(resolver, GeneratorVersion, GeneratedNamespace);
        var ifaceEmitter = new InterfaceEmitter(resolver, GeneratorVersion, GeneratedNamespace);

        var errors = new List<GenerationError>();

        foreach (var symbol in ir.TypescriptSymbols.OrderBy(s => s.Ordinal))
        {
            try
            {
                ProcessSymbol(
                    symbol, ledger, writer,
                    dictEmitter, aliasEmitter, callbackEmitter, ifaceEmitter,
                    errors, overrides, verboseFailures);
            }
            catch (Exception ex)
            {
                errors.Add(new GenerationError(symbol.Name, ex.Message, ex.GetType().Name));
                ledger.RecordFailed(symbol, ex.Message);
            }
        }

        // Validate accounting: every symbol must have exactly one entry
        var validation = ledger.Validate(ir.TypescriptSymbols.Count);
        var manifest = ledger.BuildManifest(GeneratorVersion, ir.Manifest);
        writer.WriteManifest(manifest);

        return new GenerationResult(
            validation,
            writer.WrittenFiles,
            errors,
            manifest);
    }

    private static void ProcessSymbol(
        SymbolModel symbol,
        AccountingLedger ledger,
        OutputWriter writer,
        DictionaryEmitter dictEmitter,
        AliasEmitter aliasEmitter,
        CallbackEmitter callbackEmitter,
        InterfaceEmitter ifaceEmitter,
        List<GenerationError> errors,
        IReadOnlyDictionary<string, EmitterOverrideEntry> overrides,
        bool verboseFailures)
    {
        // ── Ambiguous: must have an explicit override in the external overrides file ──
        if (symbol.Semantic.Status == "ambiguous")
        {
            if (!overrides.TryGetValue(symbol.Name, out var overrideEntry))
            {
                var failReason =
                    $"Ambiguous symbol '{symbol.Name}' has no explicit override in emitter-overrides.json. " +
                    "Add an entry with a reviewed classification and non-empty rationale.";
                ledger.RecordFailed(symbol, failReason);
                errors.Add(new GenerationError(symbol.Name, failReason, "AmbiguousSymbolException"));
                return;
            }
            _ = overrideEntry;
        }

        var classification = EffectiveClassificationPolicy.Classify(symbol, overrides).Name;
        ProcessByClassification(
            symbol, classification, ledger, writer,
            dictEmitter, aliasEmitter, callbackEmitter, ifaceEmitter,
            errors, overrides, verboseFailures);
    }

    private static void ProcessByClassification(
        SymbolModel symbol,
        string classification,
        AccountingLedger ledger,
        OutputWriter writer,
        DictionaryEmitter dictEmitter,
        AliasEmitter aliasEmitter,
        CallbackEmitter callbackEmitter,
        InterfaceEmitter ifaceEmitter,
        List<GenerationError> errors,
        IReadOnlyDictionary<string, EmitterOverrideEntry> overrides,
        bool verboseFailures)
    {
        // Check for event map -> defer
        var firstDecl = symbol.Declarations.FirstOrDefault();
        if (firstDecl?.EventMap.IsEventMap ?? false)
        {
            ledger.RecordDeferred(symbol, "event-subscription",
                "Event map interface is deferred to the typed event subscription emission phase.");
            return;
        }

        // Check Worker-only exposure -> exclude
        if (symbol.Semantic.ExposedOnWorker && !symbol.Semantic.ExposedOnWindow
            && symbol.Semantic.Exposures.Count > 0)
        {
            ledger.RecordExcluded(symbol,
                "Worker-only: symbol is exposed exclusively on Worker scope (not Window).");
            return;
        }

        switch (classification)
        {
            case "enum":
                EmitEnum(symbol, ledger, writer, errors);
                break;

            case "dictionary":
                EmitDictionary(symbol, ledger, writer, dictEmitter, errors);
                break;

            case "typedef":
                EmitTypedef(symbol, ledger, writer, aliasEmitter, errors);
                break;

            case "callback":
            case "callbackInterface":
                EmitCallback(symbol, ledger, writer, callbackEmitter, errors);
                break;

            case "interface":
            case "mixin":
                EmitInterface(symbol, ledger, writer, ifaceEmitter, errors);
                break;

            case "namespace":
                // Namespace members are surfaced as individual globalFunction/globalVariable symbols;
                // the namespace container itself is deferred to the namespace phase.
                ledger.RecordDeferred(symbol, "namespace",
                    "Namespace container: individual members are emitted as static globals. " +
                    "Namespace type itself deferred to namespace-container phase.");
                break;

            case "globalFunction":
                ledger.RecordDeferred(symbol, "globals",
                    "TS-only global function: deferred to global-namespace emission phase.");
                break;

            case "globalVariable":
                ledger.RecordDeferred(symbol, "globals",
                    "TS-only global variable: deferred to global-namespace emission phase.");
                break;

            default:
                ledger.RecordExcluded(symbol,
                    $"Unknown classification '{classification}': no emitter defined for this kind.");
                break;
        }
    }

    // ── Per-emitter wrappers ──────────────────────────────────────────────────

    private static void EmitEnum(
        SymbolModel symbol, AccountingLedger ledger,
        OutputWriter writer, List<GenerationError> errors)
    {
        try
        {
            var source = EnumEmitter.Emit(symbol, GeneratorVersion, GeneratedNamespace);
            var csName = Naming.ToCSharpTypeName(symbol.Name);
            var path = writer.Write(csName, source, "Enums");
            ledger.RecordProjected(symbol, path);
        }
        catch (Exception ex)
        {
            errors.Add(new GenerationError(symbol.Name, ex.Message, ex.GetType().Name));
            ledger.RecordFailed(symbol, ex.Message);
        }
    }

    private static void EmitDictionary(
        SymbolModel symbol, AccountingLedger ledger,
        OutputWriter writer, DictionaryEmitter emitter, List<GenerationError> errors)
    {
        try
        {
            var result = emitter.EmitWithOutcomes(symbol);
            var csName = Naming.ToCSharpTypeName(symbol.Name);
            var path = writer.Write(csName, result.Source, "Dictionaries");
            ledger.RecordProjected(
                symbol,
                path,
                result.MemberOutcomes,
                result.DeclarationOutcomes,
                result.OverloadOutcomes);
        }
        catch (DictionaryEmitException ex)
        {
            errors.Add(new GenerationError(symbol.Name, ex.Message, ex.GetType().Name));
            ledger.RecordFailed(
                symbol,
                ex.Message,
                ex.PartialOutcomes,
                ex.PartialDeclarationOutcomes,
                ex.PartialOverloadOutcomes);
        }
        catch (Exception ex)
        {
            errors.Add(new GenerationError(symbol.Name, ex.Message, ex.GetType().Name));
            ledger.RecordFailed(symbol, ex.Message);
        }
    }

    private static void EmitTypedef(
        SymbolModel symbol, AccountingLedger ledger,
        OutputWriter writer, AliasEmitter emitter, List<GenerationError> errors)
    {
        try
        {
            var source = emitter.Emit(symbol);
            var csName = Naming.ToCSharpTypeName(symbol.Name);
            var path = writer.Write(csName, source, "Typedefs");
            ledger.RecordProjected(symbol, path);
        }
        catch (Exception ex)
        {
            errors.Add(new GenerationError(symbol.Name, ex.Message, ex.GetType().Name));
            ledger.RecordFailed(symbol, ex.Message);
        }
    }

    private static void EmitCallback(
        SymbolModel symbol, AccountingLedger ledger,
        OutputWriter writer, CallbackEmitter emitter, List<GenerationError> errors)
    {
        try
        {
            var result = emitter.EmitWithOutcomes(symbol);
            var csName = Naming.ToCSharpTypeName(symbol.Name);
            var path = writer.Write(csName, result.Source, "Callbacks");
            ledger.RecordProjected(
                symbol,
                path,
                result.MemberOutcomes,
                result.DeclarationOutcomes,
                result.OverloadOutcomes);
        }
        catch (CallbackEmitException ex)
        {
            errors.Add(new GenerationError(symbol.Name, ex.Message, ex.GetType().Name));
            ledger.RecordFailed(
                symbol,
                ex.Message,
                ex.PartialOutcomes,
                ex.PartialDeclarationOutcomes,
                ex.PartialOverloadOutcomes);
        }
        catch (Exception ex)
        {
            errors.Add(new GenerationError(symbol.Name, ex.Message, ex.GetType().Name));
            ledger.RecordFailed(symbol, ex.Message);
        }
    }

    private static void EmitInterface(
        SymbolModel symbol, AccountingLedger ledger,
        OutputWriter writer, InterfaceEmitter emitter, List<GenerationError> errors)
    {
        try
        {
            var result = emitter.Emit(symbol);
            var csName = Naming.ToCSharpTypeName(symbol.Name);
            var path = writer.Write($"I{csName}", result.Source, "Interfaces");
            ledger.RecordProjected(
                symbol,
                path,
                result.MemberOutcomes,
                result.DeclarationOutcomes,
                result.OverloadOutcomes);
        }
        catch (InterfaceEmitException ex)
        {
            errors.Add(new GenerationError(symbol.Name, ex.Message, ex.GetType().Name));
            ledger.RecordFailed(
                symbol,
                ex.Message,
                ex.PartialOutcomes,
                ex.PartialDeclarationOutcomes,
                ex.PartialOverloadOutcomes);
        }
        catch (Exception ex)
        {
            errors.Add(new GenerationError(symbol.Name, ex.Message, ex.GetType().Name));
            ledger.RecordFailed(symbol, ex.Message);
        }
    }
}

public sealed record GenerationResult(
    AccountingValidationResult Validation,
    IReadOnlyList<GeneratedFile> WrittenFiles,
    IReadOnlyList<GenerationError> Errors,
    EmitterManifest Manifest);

public sealed record GenerationError(
    string SymbolName,
    string Message,
    string ExceptionType);
