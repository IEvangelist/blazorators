// Dictionary emitter: projects TypeScript interfaces matched to WebIDL "dictionary"
// classification into C# records with init-only nullable properties.
// Inheritance: C# records are NOT sealed by default. When the base type is also a known
// dictionary (record), the generated record properly inherits from it.
// If the base type is not a known dictionary, the emitter fails closed with provenance.
// Optional members -> nullable types.
// FAIL-CLOSED: All member projections must succeed; throws on any failure.
// #nullable enable is always emitted.

using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Projection;

namespace Blazor.DOM.CSharpGenerator.Emitters;

public sealed record DictionaryEmitResult(
    string Source,
    IReadOnlyList<MemberOutcome> MemberOutcomes,
    IReadOnlyList<DeclarationOutcome>? DeclarationOutcomes = null,
    IReadOnlyList<OverloadOutcome>? OverloadOutcomes = null);

public sealed class DictionaryEmitException(
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

public sealed class DictionaryEmitter(TypeResolver typeResolver, string generatorVersion, string ns)
{
    private static readonly IReadOnlySet<string> EmittedDeclarationKinds =
        new HashSet<string>(["interface", "typeAlias"], StringComparer.Ordinal);

    /// <summary>
    /// Emits a C# record file for a symbol classified as a WebIDL dictionary.
    /// Throws <see cref="TypeProjectionException"/> if any member fails to project.
    /// The caller must NOT write any output until Emit returns successfully.
    /// </summary>
    public string Emit(SymbolModel symbol)
    {
        try
        {
            return EmitCore(symbol).Source;
        }
        catch (DictionaryEmitException exception)
        {
            throw new TypeProjectionException(
                exception.Message,
                exception.Provenance);
        }
    }

    public DictionaryEmitResult EmitWithOutcomes(SymbolModel symbol)
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
        catch (DictionaryEmitException ex)
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
            throw CompleteFailure(symbol, ex.Message, $"{symbol.Name}/dictionary-emitter", []);
        }
    }

    private DictionaryEmitResult EmitCore(SymbolModel symbol)
    {
        // Collect all interface/typeAlias declarations — dictionaries may be merged.
        var decls = symbol.Declarations
            .Where(d => d.Kind is "interface" or "typeAlias")
            .OrderBy(d => d.Ordinal)
            .ToList();

        if (decls.Count == 0)
            throw new InvalidOperationException(
                $"DictionaryEmitter: '{symbol.Name}' has no interface/typeAlias declaration.");

        // Use first declaration for documentation and header.
        var primaryDecl = decls[0];
        var csName = Naming.ToCSharpSimpleTypeName(symbol.Name);

        // ── Collect all member outputs; deduplicate by member name; fail before writing ──
        var propertyOutputs = new List<(string DocLines, string PropertyLine, bool HasJsonAttr, string JsonAttr)>();
        var seenMemberNames = new HashSet<string>(StringComparer.Ordinal);
        var memberOutcomes = new List<MemberOutcome>();

        foreach (var decl in decls)
        {
            foreach (var member in decl.Members.OrderBy(m => m.Ordinal))
            {
                var memberName = member.Name?.Text;
                try
                {
                    if (member.Kind is not ("property" or "getter"))
                    {
                        throw new TypeProjectionException(
                            $"DictionaryEmitter: '{symbol.Name}' decl[{decl.Ordinal}] contains unsupported " +
                            $"member kind '{member.Kind}'.",
                            $"{symbol.Name}/decl[{decl.Ordinal}]/member[{member.Ordinal}]/{member.Kind}");
                    }

                    if (memberName is null)
                    {
                        throw new TypeProjectionException(
                            $"DictionaryEmitter: '{symbol.Name}' decl[{decl.Ordinal}] contains an unnamed " +
                            $"{member.Kind} member.",
                            $"{symbol.Name}/decl[{decl.Ordinal}]/member[{member.Ordinal}]/{member.Kind}");
                    }

                    if (!seenMemberNames.Add(memberName))
                    {
                        memberOutcomes.Add(new MemberOutcome(
                            member.Ordinal,
                            memberName,
                            member.Kind,
                            MemberOutcomeStatus.Projected,
                            null,
                            "Deduplicated from an earlier declaration.",
                            decl.Ordinal));
                        continue;
                    }

                    var entry = BuildProperty(member, symbol.Name, decl.Ordinal);
                    if (entry is null)
                    {
                        memberOutcomes.Add(new MemberOutcome(
                            member.Ordinal,
                            memberName,
                            member.Kind,
                            MemberOutcomeStatus.Deferred,
                            "dictionary-null-void",
                            "Dictionary member resolves to null/void and is deferred to the dictionary null/void phase.",
                            decl.Ordinal));
                        continue;
                    }

                    propertyOutputs.Add(entry.Value);
                    memberOutcomes.Add(new MemberOutcome(
                        member.Ordinal,
                        memberName,
                        member.Kind,
                        MemberOutcomeStatus.Projected,
                        null,
                        null,
                        decl.Ordinal));
                }
                catch (TypeProjectionException ex)
                {
                    memberOutcomes.Add(new MemberOutcome(
                        member.Ordinal,
                        memberName ?? "",
                        member.Kind,
                        MemberOutcomeStatus.Failed,
                        null,
                        ex.Message,
                        decl.Ordinal));
                    throw new DictionaryEmitException(
                        ex.Message,
                        ex.Provenance,
                        memberOutcomes);
                }
            }
        }

        // ── All members projected; build output ───────────────────────────────
        var w = new CSharpWriter();
        w.AppendLine("#nullable enable");
        w.AppendLine(CSharpWriter.AutoGeneratedHeader("Blazor.DOM.CSharpGenerator", generatorVersion));
        w.AppendLine("using System.Text.Json.Serialization;");
        w.AppendLine();
        w.AppendLine($"namespace {Naming.ToGeneratedNamespace(ns, symbol.Name)};");
        w.AppendLine();

        var docText = primaryDecl.Documentation?.Text ?? "";
        var deprecated = primaryDecl.Documentation?.Deprecated ?? false;
        w.XmlDoc(docText, deprecated);

        if (symbol.Semantic.SecureContext)
            w.AppendLine("// Requires secure context (HTTPS).");
        if (symbol.Semantic.Serializable)
            w.AppendLine("// Serializable dictionary (supports structured clone).");

        // NOTE: C# records are NOT sealed by default. When the base type is also a dictionary
        // (record) in our symbol index, we inherit from it correctly.
        // If the base is not a known dictionary, we fail closed — an unknown base cannot be safely inherited.
        string baseClause;
        try
        {
            baseClause = BuildBaseClause(symbol.Name, decls);
        }
        catch (TypeProjectionException exception)
        {
            throw new DictionaryEmitException(
                exception.Message,
                exception.Provenance,
                memberOutcomes);
        }

        var header = string.IsNullOrEmpty(baseClause)
            ? $"public record {csName}"
            : $"public record {csName} : {baseClause}";

        w.Block(header, () =>
        {
            for (var i = 0; i < propertyOutputs.Count; i++)
            {
                if (i > 0) w.AppendLine();
                var (docLines, propLine, hasJsonAttr, jsonAttr) = propertyOutputs[i];
                if (!string.IsNullOrEmpty(docLines)) w.AppendLine(docLines.TrimEnd());
                if (hasJsonAttr) w.AppendLine(jsonAttr);
                w.AppendLine(propLine);
            }
        });

        return new DictionaryEmitResult(w.ToString(), memberOutcomes);
    }

    private static DictionaryEmitException CompleteFailure(
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
        return new DictionaryEmitException(
            message,
            provenance,
            outcomes.MemberOutcomes,
            outcomes.DeclarationOutcomes,
            outcomes.OverloadOutcomes);
    }

    private (string DocLines, string PropertyLine, bool HasJsonAttr, string JsonAttr)? BuildProperty(
        MemberModel member,
        string symbolName,
        int declarationOrdinal)
    {
        var memberName = member.Name?.Text;
        if (memberName is null) return null;

        var provenance =
            $"{symbolName}/decl[{declarationOrdinal}]/member[{member.Ordinal}]/{member.Kind}/{memberName}";
        // Throws on failure — fail closed, no comment fallback
        var projection = typeResolver.Project(member.Type, provenance);

        var docText = member.Documentation?.Text ?? "";
        var deprecated = member.Documentation?.Deprecated ?? false;

        var csName = Naming.ToCSharpMemberName(memberName);
        var csType = projection.RenderedType;

        // Skip properties that resolve to 'null' (e.g., type?: undefined in TypeScript)
        if (csType is "null" or "void")
            return null;

        // Optional members become nullable unless already nullable
        if (member.Optional && !projection.IsNullable)
            csType = (projection with { IsNullable = true }).RenderedType;

        var docW = new CSharpWriter();
        docW.XmlDoc(docText, deprecated);
        var docLines = docW.ToString();

        var hasJsonAttr = !string.Equals(memberName, csName, StringComparison.Ordinal);
        var jsonAttr = hasJsonAttr ? $"[JsonPropertyName(\"{memberName}\")]" : "";
        var propLine = $"public {csType} {csName} {{ get; init; }}";

        return (docLines, propLine, hasJsonAttr, jsonAttr);
    }

    /// <summary>
    /// Builds the C# record inheritance clause for a dictionary.
    /// C# records are NOT sealed by default; inheritance is legal when the base is also a dictionary record.
    /// Throws <see cref="TypeProjectionException"/> if the base type exists but is not a known dictionary.
    /// Returns an empty string if there is no base.
    /// </summary>
    private string BuildBaseClause(string symbolName, IReadOnlyList<DeclarationModel> decls)
    {
        var bases = new List<string>();
        foreach (var decl in decls)
        {
            foreach (var heritage in decl.Heritage.Where(h => h.Token == "extends"))
            {
                foreach (var typeNode in heritage.Types)
                {
                    if (typeNode is not HeritageReferenceTypeNode referenceNode)
                    {
                        throw new TypeProjectionException(
                            $"DictionaryEmitter: '{symbolName}' decl[{decl.Ordinal}] has unsupported heritage node kind '{typeNode.Kind}'. " +
                            "Non-reference heritage requires a dedicated generic-heritage phase.",
                            $"{symbolName}/extends");
                    }

                    if (referenceNode.TypeArguments.Count > 0)
                    {
                        throw new TypeProjectionException(
                            $"DictionaryEmitter: '{symbolName}' decl[{decl.Ordinal}] extends generic type '{referenceNode.Expression}<...>'. " +
                            "Generic heritage requires a dedicated generic-heritage phase.",
                            $"{symbolName}/extends/{referenceNode.Expression}");
                    }

                    bases.Add(referenceNode.ResolvedSymbol ?? referenceNode.Expression);
                }
            }
        }

        bases = bases.Distinct(StringComparer.Ordinal).ToList();

        if (bases.Count == 0) return "";

        // Validate: every base must be a known dictionary so record inheritance is safe.
        var csNames = new List<string>();
        foreach (var baseName in bases)
        {
            if (!typeResolver.IsDictionarySymbol(baseName))
                throw new TypeProjectionException(
                    $"DictionaryEmitter: '{symbolName}' extends '{baseName}' which is not a known dictionary symbol. " +
                    "Record inheritance requires the base to also be a dictionary (record). " +
                    "Add an emitter override or remove the unsupported inheritance.",
                    $"{symbolName}/extends/{baseName}");
            csNames.Add(typeResolver.GetCSharpTypeReference(baseName));
        }

        // C# records support single inheritance only.
        if (csNames.Count > 1)
            throw new TypeProjectionException(
                $"DictionaryEmitter: '{symbolName}' extends multiple bases ({string.Join(", ", bases)}). " +
                "C# records support single inheritance only.",
                $"{symbolName}/extends");

        return csNames[0];
    }
}
