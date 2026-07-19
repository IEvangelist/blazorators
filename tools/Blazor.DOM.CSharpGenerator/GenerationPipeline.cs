// Main generation pipeline: loads IR, classifies every symbol, routes to the
// correct emitter, tracks accounting, writes output, and verifies byte identity.

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

    // Explicit overrides for the 3 ambiguous symbols.
    // Each ambiguous symbol MUST appear here or the pipeline fails.
    private static readonly Dictionary<string, string> AmbiguousOverrides =
        new(StringComparer.Ordinal)
        {
            // CSPViolationReportBody: TypeScript interface, WebIDL classifications=[dictionary].
            // Reason: TS shape is a class-like interface but WebIDL says dictionary.
            // Override: treat as dictionary (no live interface behaviour expected).
            ["CSPViolationReportBody"] = "dictionary",

            // Report: TypeScript interface, WebIDL classifications=[dictionary].
            // Override: treat as dictionary.
            ["Report"] = "dictionary",

            // ReportBody: TypeScript interface, WebIDL classifications=[dictionary].
            // Override: treat as dictionary (abstract base).
            ["ReportBody"] = "dictionary",
        };

    public static GenerationResult Run(
        IrBundle ir,
        string outputDirectory,
        bool verboseFailures = false)
    {
        var ledger = new AccountingLedger();
        var resolver = new TypeResolver(ir.TypescriptSymbols);
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
                    symbol, ledger, writer, resolver,
                    dictEmitter, aliasEmitter, callbackEmitter, ifaceEmitter,
                    errors, verboseFailures);
            }
            catch (Exception ex)
            {
                errors.Add(new GenerationError(symbol.Name, ex.Message, ex.GetType().Name));
                ledger.RecordFailed(symbol, ex.Message);
            }
        }

        // Validate accounting: every symbol must have exactly one entry
        var validation = ledger.Validate(ir.TypescriptSymbols.Count);
        var manifest = ledger.BuildManifest(GeneratorVersion, outputDirectory, ir.Manifest);
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
        TypeResolver resolver,
        DictionaryEmitter dictEmitter,
        AliasEmitter aliasEmitter,
        CallbackEmitter callbackEmitter,
        InterfaceEmitter ifaceEmitter,
        List<GenerationError> errors,
        bool verboseFailures)
    {
        // ── Ambiguous: must have an explicit override or fail ──────────────────
        if (symbol.Semantic.Status == "ambiguous")
        {
            if (!AmbiguousOverrides.TryGetValue(symbol.Name, out var overrideClassification))
            {
                ledger.RecordFailed(symbol,
                    $"Ambiguous symbol '{symbol.Name}' has no explicit override. " +
                    "Add it to GenerationPipeline.AmbiguousOverrides with a reviewed classification.");
                errors.Add(new GenerationError(symbol.Name,
                    "Ambiguous symbol without override", "AmbiguousSymbolException"));
                return;
            }
            // Treat as the overridden classification
            ProcessByClassification(
                symbol, overrideClassification, ledger, writer,
                dictEmitter, aliasEmitter, callbackEmitter, ifaceEmitter,
                errors, verboseFailures);
            return;
        }

        // ── Unmatched: part of the authoritative TS API surface, must account for it ─
        if (symbol.Semantic.Status == "unmatched")
        {
            ProcessUnmatched(symbol, ledger, writer, resolver, errors, verboseFailures);
            return;
        }

        // ── Matched: route by classification ──────────────────────────────────
        var classification = symbol.Semantic.Classifications.FirstOrDefault() ?? "";
        ProcessByClassification(
            symbol, classification, ledger, writer,
            dictEmitter, aliasEmitter, callbackEmitter, ifaceEmitter,
            errors, verboseFailures);
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

            default:
                ledger.RecordExcluded(symbol,
                    $"Unknown classification '{classification}': no emitter defined for this kind.");
                break;
        }
    }

    private static void ProcessUnmatched(
        SymbolModel symbol,
        AccountingLedger ledger,
        OutputWriter writer,
        TypeResolver resolver,
        List<GenerationError> errors,
        bool verboseFailures)
    {
        var firstDecl = symbol.Declarations.FirstOrDefault();

        // Event maps (unmatched) -> defer
        if (firstDecl?.EventMap.IsEventMap ?? false)
        {
            ledger.RecordDeferred(symbol, "event-subscription",
                "TS-only event map: deferred to typed event subscription emission phase.");
            return;
        }

        var declKind = firstDecl?.Kind ?? "";

        switch (declKind)
        {
            case "interface":
                // TS-only interfaces (154 unmatched are mostly event maps already handled,
                // or GL extension interfaces). Include them as live interfaces.
                EmitInterface(symbol, ledger, writer, new InterfaceEmitter(
                    resolver, GeneratorVersion, GeneratedNamespace), errors);
                break;

            case "typeAlias":
                // TS-only typeAliases -> alias emitter
                EmitTypedef(symbol, ledger, writer,
                    new AliasEmitter(resolver, GeneratorVersion, GeneratedNamespace), errors);
                break;

            case "globalFunction":
                // TS-only global functions -> deferred to globals phase
                ledger.RecordDeferred(symbol, "globals",
                    "TS-only global function: deferred to global-namespace emission phase.");
                break;

            case "globalVariable":
                // TS-only globals -> deferred to globals phase
                ledger.RecordDeferred(symbol, "globals",
                    "TS-only global variable: deferred to global-namespace emission phase.");
                break;

            default:
                ledger.RecordExcluded(symbol,
                    $"TS-only symbol with unhandled declaration kind '{declKind}': excluded pending manual review.");
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
            var source = emitter.Emit(symbol);
            var csName = Naming.ToCSharpTypeName(symbol.Name);
            var path = writer.Write(csName, source, "Dictionaries");
            ledger.RecordProjected(symbol, path);
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
            var source = emitter.Emit(symbol);
            var csName = Naming.ToCSharpTypeName(symbol.Name);
            var path = writer.Write(csName, source, "Callbacks");
            ledger.RecordProjected(symbol, path);
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
            var source = emitter.Emit(symbol);
            var csName = Naming.ToCSharpTypeName(symbol.Name);
            var path = writer.Write($"I{csName}", source, "Interfaces");
            ledger.RecordProjected(symbol, path);
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
