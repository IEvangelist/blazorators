using System.Security.Cryptography;
using System.Text;
using Blazor.DOM.CSharpGenerator.Emitters;
using Blazor.DOM.CSharpGenerator.Output;

namespace Blazor.DOM.CSharpGenerator.Projection;

public sealed record SynthesizedTypeDefinition(
    string Name,
    string Kind,
    string Provenance,
    string Fingerprint,
    string RelativePath,
    string Source);

internal sealed record SynthesizedTupleElement(
    string SourceName,
    string CSharpName,
    TypeProjection Projection,
    bool Optional,
    bool Rest);

internal sealed record SynthesizedProperty(
    string SourceName,
    string CSharpName,
    TypeProjection Projection,
    bool Optional,
    string Documentation,
    bool Deprecated);

internal sealed class SynthesizedTypeRegistry(
    string generatedNamespace,
    string generatorVersion = "1.0.0")
{
    private readonly Dictionary<string, SynthesizedTypeDefinition> _byIdentity =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _nameFingerprints =
        new(StringComparer.Ordinal);

    public IReadOnlyList<SynthesizedTypeDefinition> Definitions
        => _byIdentity.Values
            .OrderBy(definition => definition.RelativePath, StringComparer.Ordinal)
            .ToList();

    public string RegisterTuple(
        string provenance,
        IReadOnlyList<SynthesizedTupleElement> elements)
    {
        var fingerprint =
            $"tuple({string.Join(",", elements.Select(element =>
                $"{element.SourceName}:{element.Projection.CanonicalType}:" +
                $"{element.Optional}:{element.Rest}"))})";
        return Register(
            "Tuple",
            provenance,
            fingerprint,
            name => EmitTuple(name, elements));
    }

    public string RegisterJsonRecord(
        string provenance,
        IReadOnlyList<SynthesizedProperty> properties)
    {
        var fingerprint =
            $"record({string.Join(",", properties.Select(property =>
                $"{property.SourceName}:{property.Projection.CanonicalType}:" +
                $"{property.Optional}"))})";
        return Register(
            "Record",
            provenance,
            fingerprint,
            name => EmitRecord(name, properties));
    }

    public string RegisterStringDomain(
        string provenance,
        IReadOnlyList<string> values)
    {
        var ordered = values
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        var fingerprint = $"string-domain({string.Join("|", ordered)})";
        return Register(
            "String",
            provenance,
            fingerprint,
            name => EmitStringDomain(name, ordered));
    }

    public string RegisterTypeScriptError()
    {
        const string identity = "Standard:TypeScript.Error";
        if (_byIdentity.TryGetValue(identity, out var existing))
            return QualifiedStandard(existing.Name);

        const string name = "ITypeScriptError";
        const string fingerprint =
            "typescript/lib/lib.es5.d.ts:Error{name:string;message:string;stack?:string}";
        _byIdentity.Add(
            identity,
            new SynthesizedTypeDefinition(
                name,
                "Standard",
                "typescript/lib/lib.es5.d.ts/Error",
                fingerprint,
                Path.Combine("StandardTypes", $"{name}.g.cs"),
                EmitTypeScriptError(name)));
        return QualifiedStandard(name);
    }

    public string RegisterUnion(
        string provenance,
        NormalizedUnion normalized,
        IReadOnlyList<ProjectedUnionArm> arms,
        GenericScope? scope)
    {
        var parameters = scope?.GetAllParameters()
            .Where(parameter => parameter.Substitution is null)
            .GroupBy(parameter => parameter.CanonicalIdentity, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList() ?? [];
        var genericList = parameters.Count == 0
            ? ""
            : $"<{string.Join(", ", parameters.Select(parameter => parameter.CSharpName))}>";
        var fingerprint =
            $"union({string.Join("|", arms.Select(arm =>
                $"{arm.Source.Fingerprint}:{arm.Projection?.CanonicalType ?? arm.Source.Special.ToString()}"))})" +
            $"<params:{string.Join(",", parameters.Select(parameter => parameter.CanonicalIdentity))}>";
        return Register(
            "Union",
            provenance,
            fingerprint,
            name => UnionWrapperEmitter.Emit(
                name,
                $"{name}{genericList}",
                "",
                $"{generatedNamespace}.AdvancedTypes",
                generatorVersion,
                arms,
                "",
                false,
                string.Join(" | ", arms.Select(arm =>
                    arm.Source.Type.CheckerType ?? arm.Source.Type.Kind))),
            genericList,
            includeProvenanceInIdentity: false);
    }

    private string Register(
        string kind,
        string provenance,
        string fingerprint,
        Func<string, string> emit,
        string typeArguments = "",
        bool includeProvenanceInIdentity = true)
    {
        var identity = includeProvenanceInIdentity
            ? $"{kind}:{provenance}:{fingerprint}"
            : $"{kind}:{fingerprint}";
        if (_byIdentity.TryGetValue(identity, out var existing))
            return Qualified(existing.Name) + typeArguments;

        var owner = provenance
            .Split(['/', '['], 2, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? "Anonymous";
        var ownerName = Naming.ToCSharpSimpleTypeName(owner);
        var hash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..10];
        var name = $"{ownerName}{kind}Shape_{hash}";
        if (_nameFingerprints.TryGetValue(name, out var existingFingerprint)
            && !string.Equals(
                existingFingerprint,
                fingerprint,
                StringComparison.Ordinal))
        {
            throw new GenericDeferralException(
                $"Synthesized CLR identity '{name}' at '{provenance}' collides with " +
                "a different advanced type shape.",
                provenance,
                "synthesized-identity-collision");
        }
        _nameFingerprints[name] = fingerprint;

        var relativePath = Path.Combine("AdvancedTypes", $"{name}.g.cs");
        _byIdentity.Add(
            identity,
            new SynthesizedTypeDefinition(
                name,
                kind,
                provenance,
                fingerprint,
                relativePath,
                emit(name)));
        return Qualified(name) + typeArguments;
    }

    private string Qualified(string name)
        => $"global::{generatedNamespace}.AdvancedTypes.{name}";

    private string QualifiedStandard(string name)
        => $"global::{generatedNamespace}.StandardTypes.{name}";

    private string EmitTypeScriptError(string name)
    {
        var writer = Header();
        writer.AppendLine();
        writer.AppendLine($"namespace {generatedNamespace}.StandardTypes;");
        writer.AppendLine();
        writer.Block($"public partial interface {name}", () =>
        {
            EmitStandardErrorProperty(writer, "name", "string", "Name", nullable: false);
            writer.AppendLine();
            EmitStandardErrorProperty(writer, "message", "string", "Message", nullable: false);
            writer.AppendLine();
            EmitStandardErrorProperty(writer, "stack", "string?", "Stack", nullable: true);
        });
        return writer.ToString();
    }

    private static void EmitStandardErrorProperty(
        CSharpWriter writer,
        string sourceName,
        string type,
        string memberName,
        bool nullable)
    {
        writer.AppendLine(
            "[global::Microsoft.JSInterop.DomAccessor(" +
            $"\"{sourceName}\", " +
            "global::Microsoft.JSInterop.DomAccessorOperation.Get, " +
            "global::Microsoft.JSInterop.DomTransportKind.JsonValue, " +
            $"\"{(nullable ? "string | undefined" : "string")}\", " +
            $"Nullable = {nullable.ToString().ToLowerInvariant()}, " +
            "Streamable = false, StructuredClone = true)]");
        writer.AppendLine($"{type} {memberName} {{ get; }}");
    }

    private string EmitRecord(
        string name,
        IReadOnlyList<SynthesizedProperty> properties)
    {
        var writer = Header();
        writer.AppendLine("using System.Text.Json.Serialization;");
        writer.AppendLine();
        writer.AppendLine($"namespace {generatedNamespace}.AdvancedTypes;");
        writer.AppendLine();
        writer.Block($"public sealed record {name}", () =>
        {
            foreach (var property in properties)
            {
                writer.XmlDoc(property.Documentation, property.Deprecated);
                writer.AppendLine(
                    $"[JsonPropertyName(\"{EscapeString(property.SourceName)}\")]");
                var type = RenderOptional(property.Projection, property.Optional);
                var required = property.Optional ? "" : "required ";
                var initializer = property.Optional ? " = default;" : "";
                writer.AppendLine(
                    $"public {required}{type} {property.CSharpName} {{ get; init; }}{initializer}");
                writer.AppendLine();
            }
        });
        return writer.ToString();
    }

    private string EmitTuple(
        string name,
        IReadOnlyList<SynthesizedTupleElement> elements)
    {
        var writer = Header();
        writer.AppendLine("using System.Collections.Generic;");
        writer.AppendLine("using System.Text.Json;");
        writer.AppendLine("using System.Text.Json.Serialization;");
        writer.AppendLine();
        writer.AppendLine($"namespace {generatedNamespace}.AdvancedTypes;");
        writer.AppendLine();
        writer.AppendLine($"[JsonConverter(typeof({name}JsonConverter))]");
        writer.Block($"public sealed record {name}", () =>
        {
            foreach (var element in elements)
            {
                var type = element.Rest
                    ? $"IReadOnlyList<{element.Projection.RenderedType}>?"
                    : RenderOptional(element.Projection, element.Optional);
                var initializer = element.Optional || element.Rest
                    ? " = default;"
                    : "";
                var required = element.Optional || element.Rest ? "" : "required ";
                writer.AppendLine(
                    $"public {required}{type} {element.CSharpName} {{ get; init; }}{initializer}");
            }
        });
        writer.AppendLine();
        writer.Block(
            $"internal sealed class {name}JsonConverter : JsonConverter<{name}>",
            () =>
        {
            EmitTupleRead(writer, name, elements);
            writer.AppendLine();
            EmitTupleWrite(writer, name, elements);
        });
        return writer.ToString();
    }

    private string EmitStringDomain(
        string name,
        IReadOnlyList<string> values)
    {
        var writer = Header();
        writer.AppendLine("using System.Runtime.Serialization;");
        writer.AppendLine("using System.Text.Json.Serialization;");
        writer.AppendLine();
        writer.AppendLine($"namespace {generatedNamespace}.AdvancedTypes;");
        writer.AppendLine();
        writer.AppendLine($"[JsonConverter(typeof(JsonStringEnumConverter<{name}>))]");
        writer.Block($"public enum {name}", () =>
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < values.Count; index++)
            {
                var value = values[index];
                var memberName = Naming.ToEnumMemberName(value);
                if (!names.Add(memberName))
                {
                    throw new GenericDeferralException(
                        $"Finite string values at '{name}' collide on CLR enum member " +
                        $"'{memberName}'.",
                        name,
                        "synthesized-identity-collision");
                }
                writer.AppendLine(
                    $"[EnumMember(Value = \"{EscapeString(value)}\")]");
                writer.AppendLine("#if NET9_0_OR_GREATER");
                writer.AppendLine(
                    $"[JsonStringEnumMemberName(\"{EscapeString(value)}\")]");
                writer.AppendLine("#endif");
                writer.AppendLine(
                    index == values.Count - 1 ? memberName : $"{memberName},");
                if (index != values.Count - 1)
                    writer.AppendLine();
            }
        });
        return writer.ToString();
    }

    private static void EmitTupleRead(
        CSharpWriter writer,
        string name,
        IReadOnlyList<SynthesizedTupleElement> elements)
    {
        writer.AppendLine(
            $"public override {name} Read(ref Utf8JsonReader reader, " +
            "Type typeToConvert, JsonSerializerOptions options)");
        writer.OpenBrace();
        writer.AppendLine("if (reader.TokenType != JsonTokenType.StartArray)");
        writer.AppendLine(
            "    throw new JsonException(\"Expected a JSON array for a TypeScript tuple.\");");
        writer.AppendLine("reader.Read();");
        foreach (var element in elements)
        {
            var local = $"value{elements.IndexOf(element)}";
            if (element.Rest)
            {
                writer.AppendLine(
                    $"var {local} = new List<{element.Projection.RenderedType}>();");
                writer.AppendLine("while (reader.TokenType != JsonTokenType.EndArray)");
                writer.OpenBrace();
                writer.AppendLine(
                    $"{local}.Add(JsonSerializer.Deserialize<" +
                    $"{element.Projection.RenderedType}>(ref reader, options)!);");
                writer.AppendLine("reader.Read();");
                writer.CloseBrace();
                continue;
            }
            if (element.Optional)
            {
                writer.AppendLine(
                    $"{RenderOptional(element.Projection, true)} {local} = default;");
                writer.AppendLine("if (reader.TokenType != JsonTokenType.EndArray)");
                writer.OpenBrace();
                writer.AppendLine(
                    $"{local} = JsonSerializer.Deserialize<" +
                    $"{element.Projection.RenderedType}>(ref reader, options);");
                writer.AppendLine("reader.Read();");
                writer.CloseBrace();
                continue;
            }
            writer.AppendLine("if (reader.TokenType == JsonTokenType.EndArray)");
            writer.AppendLine(
                "    throw new JsonException(\"TypeScript tuple has too few elements.\");");
            writer.AppendLine(
                $"var {local} = JsonSerializer.Deserialize<" +
                $"{element.Projection.RenderedType}>(ref reader, options)!;");
            writer.AppendLine("reader.Read();");
        }
        writer.AppendLine("if (reader.TokenType != JsonTokenType.EndArray)");
        writer.AppendLine(
            "    throw new JsonException(\"TypeScript tuple has too many elements.\");");
        writer.AppendLine($"return new {name}");
        writer.OpenBrace();
        for (var index = 0; index < elements.Count; index++)
            writer.AppendLine($"{elements[index].CSharpName} = value{index},");
        writer.CloseBrace(";");
        writer.CloseBrace();
    }

    private static void EmitTupleWrite(
        CSharpWriter writer,
        string name,
        IReadOnlyList<SynthesizedTupleElement> elements)
    {
        writer.AppendLine(
            $"public override void Write(Utf8JsonWriter writer, {name} value, " +
            "JsonSerializerOptions options)");
        writer.OpenBrace();
        writer.AppendLine("writer.WriteStartArray();");
        string? previousOptional = null;
        foreach (var element in elements)
        {
            if (element.Rest)
            {
                if (previousOptional is not null)
                {
                    writer.AppendLine(
                        $"if (value.{element.CSharpName} is {{ Count: > 0 }} " +
                        $"&& value.{previousOptional} is null)");
                    writer.AppendLine(
                        "    throw new JsonException(\"Tuple rest elements cannot follow " +
                        "an omitted optional element.\");");
                }
                writer.AppendLine($"if (value.{element.CSharpName} is not null)");
                writer.OpenBrace();
                writer.AppendLine($"foreach (var item in value.{element.CSharpName})");
                writer.AppendLine(
                    "    JsonSerializer.Serialize(writer, item, options);");
                writer.CloseBrace();
            }
            else if (element.Optional)
            {
                if (previousOptional is not null)
                {
                    writer.AppendLine(
                        $"if (value.{element.CSharpName} is not null " +
                        $"&& value.{previousOptional} is null)");
                    writer.AppendLine(
                        "    throw new JsonException(\"A later optional tuple element " +
                        "cannot be present after an omitted element.\");");
                }
                writer.AppendLine($"if (value.{element.CSharpName} is not null)");
                writer.AppendLine(
                    $"    JsonSerializer.Serialize(writer, value.{element.CSharpName}, options);");
                previousOptional = element.CSharpName;
            }
            else
            {
                writer.AppendLine(
                    $"JsonSerializer.Serialize(writer, value.{element.CSharpName}, options);");
            }
        }
        writer.AppendLine("writer.WriteEndArray();");
        writer.CloseBrace();
    }

    private CSharpWriter Header()
    {
        var writer = new CSharpWriter();
        writer.AppendLine(CSharpWriter.AutoGeneratedHeader(
            "Blazor.DOM.CSharpGenerator",
            generatorVersion));
        return writer;
    }

    private static string RenderOptional(TypeProjection projection, bool optional)
    {
        var rendered = projection.RenderedType;
        if (!optional || rendered.EndsWith("?", StringComparison.Ordinal))
            return rendered;
        return projection.Identity.Kind is ClrTypeKind.Value or ClrTypeKind.Reference
            ? $"{rendered}?"
            : rendered;
    }

    private static string EscapeString(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}

internal static class ReadOnlyListExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> items, T value)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (EqualityComparer<T>.Default.Equals(items[index], value))
                return index;
        }
        return -1;
    }
}
