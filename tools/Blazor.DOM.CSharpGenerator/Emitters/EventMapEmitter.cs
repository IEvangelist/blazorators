using System.Security.Cryptography;
using System.Text;
using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Projection;

namespace Blazor.DOM.CSharpGenerator.Emitters;

public sealed record EventMapEmitResult(
    string Source,
    IReadOnlyList<MemberOutcome> MemberOutcomes,
    IReadOnlyList<DeclarationOutcome> DeclarationOutcomes,
    IReadOnlyList<OverloadOutcome> OverloadOutcomes);

public sealed class EventMapEmitter(
    TypeResolver typeResolver,
    string generatorVersion,
    string ns,
    IReadOnlyList<SymbolModel> symbols)
{
    private static readonly IReadOnlySet<string> EmittedDeclarationKinds =
        new HashSet<string>(["interface"], StringComparer.Ordinal);

    private readonly IReadOnlyDictionary<string, SymbolModel> _symbols =
        symbols.ToDictionary(symbol => symbol.Name, StringComparer.Ordinal);

    private sealed record EventEntry(
        string Name,
        string MemberName,
        TypeProjection Projection,
        string SourceType,
        bool Deprecated,
        string Documentation,
        IReadOnlyList<string> Provenance);

    public EventMapEmitResult Emit(SymbolModel symbol)
    {
        var declarations = EventMapDeclarations(symbol);
        if (declarations.Count == 0)
        {
            throw new InvalidOperationException(
                $"EventMapEmitter: '{symbol.Name}' is not an EventMap.");
        }

        var entries = CollectEffectiveEvents(
            symbol,
            new HashSet<string>(StringComparer.Ordinal));
        var source = Render(symbol, declarations, entries);
        var outcomes = declarations
            .SelectMany(declaration => declaration.Members.Select(member =>
                new MemberOutcome(
                    member.Ordinal,
                    member.Name?.Text ?? member.Kind,
                    member.Kind,
                    MemberOutcomeStatus.Projected,
                    Reason: "Emitted as a typed DOM event descriptor.",
                    DeclarationOrdinal: declaration.Ordinal)))
            .ToList();
        var complete = EmitterOutcomeReconciler.CompleteSuccess(
            symbol,
            outcomes,
            EmittedDeclarationKinds);
        return new EventMapEmitResult(
            source,
            complete.MemberOutcomes,
            complete.DeclarationOutcomes,
            complete.OverloadOutcomes);
    }

    private IReadOnlyList<EventEntry> CollectEffectiveEvents(
        SymbolModel symbol,
        HashSet<string> path)
    {
        if (!path.Add(symbol.Name))
        {
            throw new TypeProjectionException(
                $"Event map inheritance cycle detected at '{symbol.Name}'.",
                $"{symbol.Name}/event-map/heritage");
        }

        try
        {
            var candidates = new List<EventEntry>();
            foreach (var declaration in EventMapDeclarations(symbol))
            {
                foreach (var heritage in declaration.Heritage
                    .Where(clause => clause.Token == "extends"))
                {
                    foreach (var type in heritage.Types)
                    {
                        var baseName = type switch
                        {
                            ReferenceTypeNode reference =>
                                reference.ResolvedSymbol ?? reference.Name,
                            HeritageReferenceTypeNode reference =>
                                reference.ResolvedSymbol ?? reference.Expression,
                            _ => throw new TypeProjectionException(
                                $"Event map '{symbol.Name}' has unsupported heritage " +
                                $"shape '{type.Kind}'.",
                                $"{symbol.Name}/event-map/heritage"),
                        };
                        if (!_symbols.TryGetValue(baseName, out var baseSymbol)
                            || EventMapDeclarations(baseSymbol).Count == 0)
                        {
                            throw new TypeProjectionException(
                                $"Event map '{symbol.Name}' extends unresolved EventMap " +
                                $"'{baseName}'.",
                                $"{symbol.Name}/event-map/heritage/{baseName}");
                        }
                        candidates.AddRange(CollectEffectiveEvents(baseSymbol, path));
                    }
                }

                foreach (var member in declaration.Members.OrderBy(member => member.Ordinal))
                {
                    if (member.Kind != "property" || member.Name is null || member.Type is null)
                    {
                        throw new TypeProjectionException(
                            $"Event map '{symbol.Name}' contains unsupported member " +
                            $"'{member.Kind}'.",
                            $"{symbol.Name}/decl[{declaration.Ordinal}]/" +
                            $"member[{member.Ordinal}]");
                    }
                    var provenance =
                        $"{symbol.Name}/decl[{declaration.Ordinal}]/" +
                        $"member[{member.Ordinal}]/{member.Name.Text}";
                    var projection = typeResolver.Project(
                        member.Type,
                        $"{provenance}/payload");
                    if (projection.Transport?.Kind is not ("js-reference" or "json-value")
                        || projection.Transport.Kind == "js-reference"
                        && projection.Identity.Kind != ClrTypeKind.Reference)
                    {
                        throw new TypeProjectionException(
                            $"Event '{member.Name.Text}' on '{symbol.Name}' must use " +
                            "a reviewed live-reference or JSON-value payload transport.",
                            provenance);
                    }
                    var sourceType = projection.Transport.SourceType;
                    candidates.Add(new EventEntry(
                        member.Name.Text,
                        "",
                        ReplaceDynamicPayload(projection),
                        sourceType,
                        member.Documentation.Deprecated,
                        member.Documentation.Text,
                        [provenance]));
                }
            }

            return ReconcileNames(candidates);
        }
        finally
        {
            path.Remove(symbol.Name);
        }
    }

    private static TypeProjection ReplaceDynamicPayload(TypeProjection projection)
    {
        if (!projection.CSharpType.Contains("object", StringComparison.Ordinal))
            return projection;

        var rendered = projection.CSharpType.Replace(
            "object",
            "global::Microsoft.JSInterop.DomDynamicValue",
            StringComparison.Ordinal);
        return projection with
        {
            CSharpType = rendered,
            Identity = projection.Identity with
            {
                CanonicalName = projection.Identity.CanonicalName.Replace(
                    "object",
                    "Microsoft.JSInterop.DomDynamicValue",
                    StringComparison.Ordinal),
            },
        };
    }

    private static IReadOnlyList<EventEntry> ReconcileNames(
        IReadOnlyList<EventEntry> candidates)
    {
        var semantic = candidates
            .GroupBy(
                entry => (
                    entry.Name,
                    entry.Projection.CanonicalType,
                    Transport: TransportFingerprint(entry.Projection.Transport)),
                new EventSemanticComparer())
            .Select(group =>
            {
                var first = group.First();
                return first with
                {
                    Deprecated = group.Any(entry => entry.Deprecated),
                    Documentation = string.Join(
                        "\n\n",
                        group.Select(entry => entry.Documentation)
                            .Where(text => !string.IsNullOrWhiteSpace(text))
                            .Distinct(StringComparer.Ordinal)),
                    Provenance = group.SelectMany(entry => entry.Provenance)
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)
                        .ToList(),
                };
            })
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .ThenBy(entry => entry.Projection.CanonicalType, StringComparer.Ordinal)
            .ToList();

        var used = new HashSet<string>(StringComparer.Ordinal);
        return semantic.Select(entry =>
        {
            var baseName = Naming.ToCSharpMemberName(entry.Name).TrimStart('@');
            var collision = semantic.Count(candidate =>
                string.Equals(
                    Naming.ToCSharpMemberName(candidate.Name).TrimStart('@'),
                    baseName,
                    StringComparison.OrdinalIgnoreCase)) > 1;
            var name = collision
                ? $"{baseName}_{StableSuffix(
                    entry.Name + "|" + entry.Projection.CanonicalType)}"
                : baseName;
            while (!used.Add(name))
                name += "_";
            return entry with { MemberName = name };
        }).ToList();
    }

    private string Render(
        SymbolModel symbol,
        IReadOnlyList<DeclarationModel> declarations,
        IReadOnlyList<EventEntry> entries)
    {
        var writer = new CSharpWriter();
        writer.AppendLine("#nullable enable");
        writer.AppendLine(CSharpWriter.AutoGeneratedHeader(
            "Blazor.DOM.CSharpGenerator",
            generatorVersion));
        writer.AppendLine(
            $"namespace {Naming.ToGeneratedNamespace(ns, symbol.Name)};");
        writer.AppendLine();
        var docs = string.Join(
            "\n\n",
            declarations.Select(declaration => declaration.Documentation.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.Ordinal));
        writer.XmlDoc(docs, declarations.Any(declaration =>
            declaration.Documentation.Deprecated));
        writer.Block(
            $"public static partial class {Naming.ToCSharpSimpleTypeName(symbol.Name)}",
            () =>
            {
                for (var index = 0; index < entries.Count; index++)
                {
                    var entry = entries[index];
                    writer.XmlDoc(entry.Documentation, entry.Deprecated);
                    writer.AppendLine(
                        $"public static global::Microsoft.JSInterop.DomEventDescriptor<" +
                        $"{entry.Projection.RenderedType}> {entry.MemberName} {{ get; }} =");
                    var factory = entry.Projection.Transport?.Kind == "json-value"
                        ? "Value"
                        : "Reference";
                    writer.AppendLine(
                        $"    global::Microsoft.JSInterop.DomEventDescriptor<" +
                        $"{entry.Projection.RenderedType}>.{factory}(");
                    writer.AppendLine($"        \"{Escape(entry.Name)}\",");
                    writer.AppendLine($"        \"{Escape(symbol.Name)}\",");
                    writer.AppendLine($"        \"{Escape(entry.SourceType)}\",");
                    if (factory == "Value")
                    {
                        writer.AppendLine(
                            $"        {entry.Projection.IsNullable.ToString().ToLowerInvariant()},");
                    }
                    writer.AppendLine(
                        $"        {entry.Deprecated.ToString().ToLowerInvariant()},");
                    for (var provenanceIndex = 0;
                        provenanceIndex < entry.Provenance.Count;
                        provenanceIndex++)
                    {
                        var suffix = provenanceIndex < entry.Provenance.Count - 1
                            ? ","
                            : ");";
                        writer.AppendLine(
                            $"        \"{Escape(entry.Provenance[provenanceIndex])}\"{suffix}");
                    }
                    if (index < entries.Count - 1)
                        writer.AppendLine();
                }
            });
        return writer.ToString();
    }

    private static IReadOnlyList<DeclarationModel> EventMapDeclarations(
        SymbolModel symbol) =>
        symbol.Declarations
            .Where(declaration =>
                declaration.Kind == "interface"
                && declaration.EventMap.IsEventMap)
            .OrderBy(declaration => declaration.Ordinal)
            .ToList();

    private static string TransportFingerprint(TransportModel? transport) =>
        transport is null
            ? ""
            : $"{transport.Kind}|{transport.Nullable}|{transport.SourceType}|" +
              $"{transport.Streamable}|{transport.StructuredClone}|{transport.Reason}";

    private static string StableSuffix(string value) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..8];

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    private sealed class EventSemanticComparer :
        IEqualityComparer<(string Name, string Payload, string Transport)>
    {
        public bool Equals(
            (string Name, string Payload, string Transport) x,
            (string Name, string Payload, string Transport) y) =>
            string.Equals(x.Name, y.Name, StringComparison.Ordinal)
            && string.Equals(x.Payload, y.Payload, StringComparison.Ordinal)
            && string.Equals(x.Transport, y.Transport, StringComparison.Ordinal);

        public int GetHashCode(
            (string Name, string Payload, string Transport) value) =>
            HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(value.Name),
                StringComparer.Ordinal.GetHashCode(value.Payload),
                StringComparer.Ordinal.GetHashCode(value.Transport));
    }
}
