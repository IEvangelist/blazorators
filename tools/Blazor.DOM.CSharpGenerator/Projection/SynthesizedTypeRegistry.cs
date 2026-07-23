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
    private readonly Dictionary<string, SynthesizedTypeDefinition> _definitionsByName =
        new(StringComparer.Ordinal);

    public IReadOnlyList<SynthesizedTypeDefinition> Definitions
        => _definitionsByName.Values
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
            name => EmitTuple(name, elements),
            semanticDetail: SemanticTypeNaming.DescribeTuple(
                elements.Select(element => (
                    element.SourceName,
                    element.CSharpName,
                    element.Projection)).ToList()));
    }

    public string RegisterReferenceTuple(
        string provenance,
        IReadOnlyList<SynthesizedTupleElement> elements,
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
            $"reference-tuple({string.Join(",", elements.Select(element =>
                $"{element.SourceName}:{element.Projection.CanonicalType}:" +
                $"{element.Optional}:{element.Rest}"))})" +
            $"<params:{string.Join(",", parameters.Select(parameter => parameter.CanonicalIdentity))}>";
        return Register(
            "ReferenceTuple",
            provenance,
            fingerprint,
            name => EmitReferenceTuple($"{name}{genericList}", elements),
            genericList,
            semanticDetail: SemanticTypeNaming.DescribeTuple(
                elements.Select(element => (
                    element.SourceName,
                    element.CSharpName,
                    element.Projection)).ToList()));
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
            name => EmitRecord(name, properties),
            semanticDetail: SemanticTypeNaming.DescribeRecord(
                provenance,
                properties.Select(property => (
                    property.CSharpName,
                    property.Projection)).ToList()));
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
            name => EmitStringDomain(name, ordered),
            semanticDetail: SemanticTypeNaming.DescribeStringDomain(ordered));
    }

    public string RegisterNumericDomain(
        string provenance,
        IReadOnlyList<double> values)
    {
        var ordered = values.Distinct().Order().ToList();
        var fingerprint =
            $"numeric-domain({string.Join("|", ordered.Select(value =>
                value.ToString(System.Globalization.CultureInfo.InvariantCulture)))})";
        return Register(
            "Numeric",
            provenance,
            fingerprint,
            name => EmitNumericDomain(name, ordered),
            semanticDetail: SemanticTypeNaming.DescribeNumericDomain(ordered));
    }

    public string RegisterConstraint(string provenance, string sourceShape)
    {
        var fingerprint = $"constraint({sourceShape})";
        return Register(
            "Constraint",
            provenance,
            fingerprint,
            name => EmitConstraint(name, sourceShape),
            semanticDetail: SemanticTypeNaming.DescribeSourceShape(sourceShape));
    }

    public string RegisterIntersection(string provenance, string sourceShape)
    {
        var fingerprint = $"intersection({sourceShape})";
        return Register(
            "Intersection",
            provenance,
            fingerprint,
            name => EmitIntersection(name, sourceShape),
            semanticDetail: SemanticTypeNaming.DescribeSourceShape(sourceShape));
    }

    public string RegisterStringPattern(
        string provenance,
        string pattern,
        string sourceShape)
    {
        var fingerprint = $"string-pattern({pattern}:{sourceShape})";
        return Register(
            "StringPattern",
            provenance,
            fingerprint,
            name => EmitStringPattern(name, pattern, sourceShape));
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
        _definitionsByName.Add(name, _byIdentity[identity]);
        return QualifiedStandard(name);
    }

    public string RegisterTypeScriptNever()
    {
        const string identity = "Standard:TypeScript.never";
        if (_byIdentity.TryGetValue(identity, out var existing))
            return QualifiedStandard(existing.Name);

        const string name = "TypeScriptNever";
        const string fingerprint = "typescript:never:uninhabited";
        _byIdentity.Add(
            identity,
            new SynthesizedTypeDefinition(
                name,
                "Never",
                "typescript/never",
                fingerprint,
                Path.Combine("StandardTypes", $"{name}.g.cs"),
                EmitTypeScriptNever(name)));
        _definitionsByName.Add(name, _byIdentity[identity]);
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
            semanticDetail: SemanticTypeNaming.DescribeUnion(
                arms.Select(arm => arm.Name).ToList()));
    }

    private string Register(
        string kind,
        string provenance,
        string fingerprint,
        Func<string, string> emit,
        string typeArguments = "",
        string semanticDetail = "")
    {
        var identity = $"{kind}:{provenance}:{fingerprint}";
        if (_byIdentity.TryGetValue(identity, out var existing))
            return Qualified(existing.Name) + typeArguments;

        var name = SemanticTypeNaming.BuildSynthesizedTypeName(
            provenance,
            kind,
            semanticDetail);
        if (_definitionsByName.TryGetValue(name, out var existingByName))
        {
            if (!string.Equals(
                existingByName.Fingerprint,
                fingerprint,
                StringComparison.Ordinal))
            {
                throw new GenericDeferralException(
                    $"Semantic synthesized CLR name '{name}' at '{provenance}' collides with " +
                    $"the different shape registered at '{existingByName.Provenance}'. " +
                    "Add a semantic naming rule for this distinction; hashes and ordinals " +
                    "are not permitted in public identifiers.",
                    provenance,
                    "synthesized-semantic-name-collision");
            }
            _byIdentity.Add(identity, existingByName);
            return Qualified(existingByName.Name) + typeArguments;
        }

        var relativePath = Path.Combine("AdvancedTypes", $"{name}.g.cs");
        var definition = new SynthesizedTypeDefinition(
            name,
            kind,
            provenance,
            fingerprint,
            relativePath,
            emit(name));
        _byIdentity.Add(identity, definition);
        _definitionsByName.Add(name, definition);
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

    private string EmitTypeScriptNever(string name)
    {
        var writer = Header();
        writer.AppendLine();
        writer.AppendLine($"namespace {generatedNamespace}.StandardTypes;");
        writer.AppendLine();
        writer.AppendLine(
            "/// <summary>Represents TypeScript's uninhabited <c>never</c> type.</summary>");
        writer.Block($"public sealed class {name}", () =>
        {
            writer.AppendLine($"private {name}() {{ }}");
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

    private string EmitReferenceTuple(
        string declaredName,
        IReadOnlyList<SynthesizedTupleElement> elements)
    {
        var writer = Header();
        writer.AppendLine();
        writer.AppendLine($"namespace {generatedNamespace}.AdvancedTypes;");
        writer.AppendLine();
        writer.Block($"public partial interface {declaredName} : global::Microsoft.JSInterop.IDomProxy", () =>
        {
            for (var index = 0; index < elements.Count; index++)
            {
                var element = elements[index];
                var transport = element.Projection.Transport;
                var transportKind = transport?.Kind switch
                {
                    "js-reference" => "JsReference",
                    "js-stream" => "JsStream",
                    "binary" => "Binary",
                    "transferable" => "Transferable",
                    _ => "JsonValue",
                };
                writer.AppendLine(
                    "[global::Microsoft.JSInterop.DomIndexAccessor(" +
                    "global::Microsoft.JSInterop.DomAccessorOperation.Get, " +
                    "global::Microsoft.JSInterop.DomIndexKeyKind.Number, \"number\", " +
                    $"global::Microsoft.JSInterop.DomTransportKind.{transportKind}, " +
                    $"\"{EscapeString(transport?.SourceType ?? element.Projection.CSharpType)}\", " +
                    $"Nullable = {(element.Optional || element.Projection.IsNullable).ToString().ToLowerInvariant()}, " +
                    $"Streamable = {(transport?.Streamable == true).ToString().ToLowerInvariant()}, " +
                    $"StructuredClone = {(transport?.StructuredClone == true).ToString().ToLowerInvariant()})]");
                writer.AppendLine(
                    $"{element.Projection.RenderedType} GetItem{index + 1}();");
                if (index < elements.Count - 1)
                    writer.AppendLine();
            }
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

    private string EmitNumericDomain(string name, IReadOnlyList<double> values)
    {
        var writer = Header();
        writer.AppendLine("using System.Text.Json;");
        writer.AppendLine("using System.Text.Json.Serialization;");
        writer.AppendLine();
        writer.AppendLine($"namespace {generatedNamespace}.AdvancedTypes;");
        writer.AppendLine();
        writer.AppendLine($"[JsonConverter(typeof({name}JsonConverter))]");
        writer.Block($"public readonly record struct {name}", () =>
        {
            writer.AppendLine("public double Value { get; }");
            writer.AppendLine();
            writer.Block($"public {name}(double value)", () =>
            {
                writer.AppendLine(
                    $"if (value is not ({string.Join(" or ", values.Select(value =>
                        value.ToString(System.Globalization.CultureInfo.InvariantCulture))) }))");
                writer.AppendLine(
                    "    throw new ArgumentOutOfRangeException(nameof(value));");
                writer.AppendLine("Value = value;");
            });
            writer.AppendLine(
                $"public static implicit operator double({name} value) => value.Value;");
        });
        writer.AppendLine();
        writer.Block(
            $"internal sealed class {name}JsonConverter : JsonConverter<{name}>",
            () =>
            {
                writer.AppendLine(
                    $"public override {name} Read(ref Utf8JsonReader reader, Type typeToConvert, " +
                    $"JsonSerializerOptions options) => new(reader.GetDouble());");
                writer.AppendLine(
                    $"public override void Write(Utf8JsonWriter writer, {name} value, " +
                    "JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);");
            });
        return writer.ToString();
    }

    private string EmitConstraint(string name, string sourceShape)
    {
        var writer = Header();
        writer.AppendLine();
        writer.AppendLine($"namespace {generatedNamespace}.AdvancedTypes;");
        writer.AppendLine();
        writer.AppendLine(
            $"/// <summary>Constraint abstraction for <c>{EscapeXml(sourceShape)}</c>.</summary>");
        writer.AppendLine($"public interface {name};");
        return writer.ToString();
    }

    private string EmitIntersection(string name, string sourceShape)
    {
        var writer = Header();
        writer.AppendLine();
        writer.AppendLine($"namespace {generatedNamespace}.AdvancedTypes;");
        writer.AppendLine();
        writer.AppendLine(
            $"/// <summary>Composite live reference preserving every arm of " +
            $"<c>{EscapeXml(sourceShape)}</c>.</summary>");
        writer.AppendLine(
            $"public interface {name} : global::Microsoft.JSInterop.IDomProxy;");
        return writer.ToString();
    }

    private string EmitStringPattern(
        string name,
        string pattern,
        string sourceShape)
    {
        var writer = Header();
        writer.AppendLine("using System.Text.Json;");
        writer.AppendLine("using System.Text.Json.Serialization;");
        writer.AppendLine("using System.Text.RegularExpressions;");
        writer.AppendLine();
        writer.AppendLine($"namespace {generatedNamespace}.AdvancedTypes;");
        writer.AppendLine();
        writer.AppendLine($"[JsonConverter(typeof({name}JsonConverter))]");
        writer.Block(
            $"public readonly record struct {name} : global::Microsoft.JSInterop.ITypeScriptStringValue",
            () =>
            {
                writer.AppendLine(
                    $"private static readonly Regex Pattern = new(\"{EscapeString(pattern)}\", " +
                    "RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);");
                writer.AppendLine("public string Value { get; }");
                writer.AppendLine();
                writer.Block($"public {name}(string value)", () =>
                {
                    writer.AppendLine("ArgumentNullException.ThrowIfNull(value);");
                    writer.AppendLine(
                        "if (!Pattern.IsMatch(value)) throw new ArgumentException(" +
                        $"\"Value does not match TypeScript pattern {EscapeString(sourceShape)}.\", " +
                        "nameof(value));");
                    writer.AppendLine("Value = value;");
                });
                writer.AppendLine();
                writer.AppendLine(
                    $"public static {name} Parse(string value) => new(value);");
                writer.Block(
                    $"public static bool TryParse(string? value, out {name} result)",
                    () =>
                    {
                        writer.AppendLine("if (value is not null && Pattern.IsMatch(value))");
                        writer.OpenBrace();
                        writer.AppendLine($"result = new {name}(value);");
                        writer.AppendLine("return true;");
                        writer.CloseBrace();
                        writer.AppendLine("result = default;");
                        writer.AppendLine("return false;");
                    });
                writer.AppendLine("public override string ToString() => Value;");
            });
        writer.AppendLine();
        writer.Block(
            $"internal sealed class {name}JsonConverter : JsonConverter<{name}>",
            () =>
            {
                writer.AppendLine(
                    $"public override {name} Read(ref Utf8JsonReader reader, Type typeToConvert, " +
                    "JsonSerializerOptions options) => new(reader.GetString() ?? throw new " +
                    "JsonException(\"Pattern value cannot be null.\"));");
                writer.AppendLine(
                    $"public override void Write(Utf8JsonWriter writer, {name} value, " +
                    "JsonSerializerOptions options) => writer.WriteStringValue(value.Value);");
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

    private static string EscapeXml(string value)
        => System.Security.SecurityElement.Escape(value) ?? "";
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
