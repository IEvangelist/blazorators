// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.CSharp;

/// <summary>
/// A record the represents various C# objects.
/// </summary>
internal record CSharpObject(
    string TypeName,
    string? ExtendsTypeName) : ICSharpDependencyGraphObject
{
    /// <summary>
    /// The collection of types that this object depends on.
    /// </summary>
    public Dictionary<string, CSharpObject> DependentTypes { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    private IImmutableSet<(string TypeName, CSharpObject Object)>? _allDependentTypes;
    private bool _isComputingAllDependentTypes;

    public IImmutableSet<(string TypeName, CSharpObject Object)> AllDependentTypes
    {
        get
        {
            if (_allDependentTypes is not null)
            {
                return _allDependentTypes;
            }

            // Re-entrant access (cycle in the dependency graph): return what we
            // have so far. The outermost call will finalize and cache the full set.
            if (_isComputingAllDependentTypes)
            {
                return ImmutableHashSet<(string, CSharpObject)>.Empty;
            }

            _isComputingAllDependentTypes = true;
            try
            {
                Dictionary<string, CSharpObject> result = new(StringComparer.OrdinalIgnoreCase);
                foreach (var prop
                    in this.GetAllDependencies()
                        .Concat(Properties.SelectMany(
                            p => p.Value.AllDependentTypes))
                        .Concat(Methods.SelectMany(
                            p => p.Value.AllDependentTypes)))
                {
                    result[prop.TypeName] = prop.Object;
                }

                return _allDependentTypes = result.Select(kvp => (kvp.Key, kvp.Value))
                    .Concat([(TypeName, this)])
                    .ToImmutableHashSet();
            }
            finally
            {
                _isComputingAllDependentTypes = false;
            }
        }
    }

    /// <summary>
    /// The <see cref="Dictionary{TKey, TValue}.Keys"/> represent the raw parsed member name, while the
    /// corresponding <see cref="Dictionary{TKey, TValue}.Values"/> are the <see cref="CSharpProperty"/> details.
    /// </summary>
    public Dictionary<string, CSharpProperty> Properties { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The <see cref="Dictionary{TKey, TValue}.Keys"/> represent the raw parsed member name, while the
    /// corresponding <see cref="Dictionary{TKey, TValue}.Values"/> are the <see cref="CSharpMethod"/> details.
    /// </summary>
    public Dictionary<string, CSharpMethod> Methods { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True when this object should be treated as a delegate/action
    /// parameter rather than a serialized DTO. Backed entirely by
    /// <see cref="IsCallback"/>, which is set via shape-based detection
    /// (see <c>TypeDeclarationParser.IsCallbackTypeDeclaration</c>).
    /// </summary>
    /// <remarks>
    /// A previous implementation also matched
    /// <c>TypeName.EndsWith("Callback")</c> as a belt-and-suspenders
    /// fallback, which misclassified any normal DTO whose name ended
    /// with "Callback" (silently dropping it from the generated
    /// output). The shape-based classifier handles every callback
    /// interface in the bundled DOM declarations.
    /// </remarks>
    public bool IsActionParameter => IsCallback;

    /// <summary>
    /// True when this object was parsed from a TypeScript callback
    /// interface (a body of one-or-more anonymous call signatures).
    /// Set by <c>TypeDeclarationParser.ToObject</c> via shape-based
    /// detection rather than relying on the <c>"Callback"</c> name
    /// suffix.
    /// </summary>
    public bool IsCallback { get; init; }

    private const char NewLine = '\n';

    internal string ToClassString()
    {
        StringBuilder builder = new();

        AppendHeader(builder);
        AppendClassOpening(builder, TypeName);

        foreach (var kvp in Properties)
        {
            // TypeScript index signatures (`[key: string]: T;`) appear in
            // the parsed property dictionary because the shared property
            // regex matches them, but they have no direct C# property
            // analogue -- the raw indexer key (`[index: number]`) is not
            // a legal identifier. The top-level emit path already skips
            // these via `IsIndexer`; mirror that filter here so dependent
            // DTOs pulled in transitively (e.g. through a property typed
            // as `CSSRuleList` or `HTMLCollection`, both of which carry
            // `[index: number]: T;`) don't break the consumer's build.
            if (kvp.Value.IsIndexer)
            {
                continue;
            }

            AppendProperty(builder, TypeName, kvp.Key, kvp.Value);
        }

        builder.Append('}').Append(NewLine);

        return builder.ToString();
    }

    private static void AppendHeader(StringBuilder builder)
    {
        builder
            .Append("#nullable enable").Append(NewLine)
            .Append("using System.Text.Json.Serialization;").Append(NewLine).Append(NewLine)
            .Append("namespace Microsoft.JSInterop;").Append(NewLine).Append(NewLine);
    }

    private static void AppendClassOpening(StringBuilder builder, string typeName)
    {
        builder
            .Append("/// <summary>").Append(NewLine)
            .Append("/// Source-generated object representing an ideally immutable <c>")
                .Append(typeName).Append("</c> value.").Append(NewLine)
            .Append("/// </summary>").Append(NewLine)
            .Append("public class ").Append(typeName).Append(NewLine)
            .Append('{').Append(NewLine);
    }

    private static void AppendProperty(
        StringBuilder builder,
        string typeName,
        string memberName,
        CSharpProperty member)
    {
        var mappedTypeName = member.MappedTypeName;
        var nullableSuffix = member.IsNullable && !mappedTypeName.EndsWith("?", StringComparison.Ordinal) ? "?" : "";
        var arraySuffix = member.IsArray ? "[]" : "";
        var isPrimitive = TypeMap.PrimitiveTypes.IsPrimitiveType(mappedTypeName);
        var initializer = member.IsNullable ||
            mappedTypeName is "string" ||
            isPrimitive is false
                ? " = default!;"
                : "";
        var csharpMemberName = memberName.CapitalizeFirstLetter();

        builder
            .Append("    /// <summary>").Append(NewLine)
            .Append("    /// Source-generated property representing the <c>")
                .Append(typeName).Append('.').Append(memberName).Append("</c> value.").Append(NewLine)
            .Append("    /// </summary>").Append(NewLine)
            .Append("    [JsonPropertyName(\"").Append(memberName).Append("\")]").Append(NewLine)
            .Append("    public ").Append(mappedTypeName).Append(arraySuffix).Append(nullableSuffix)
                .Append(' ').Append(csharpMemberName).Append(" { get; set; }").Append(initializer)
                .Append(NewLine);

        if (member.RawTypeName is "DOMTimeStamp" or "DOMTimeStamp | null"
            or "EpochTimeStamp" or "EpochTimeStamp | null")
        {
            AppendUnixToUtcDateTimeAccessor(builder, typeName, memberName, csharpMemberName, member);
        }
    }

    private static void AppendUnixToUtcDateTimeAccessor(
        StringBuilder builder,
        string typeName,
        string memberName,
        string csharpMemberName,
        CSharpProperty member)
    {
        var nullable = member.IsNullable ? "?" : "";

        builder
            .Append("    /// <summary>").Append(NewLine)
            .Append("    /// Source-generated property representing the <c>")
                .Append(typeName).Append('.').Append(memberName).Append("</c> value, ").Append(NewLine)
            .Append("    /// converted as a <see cref=\"System.DateTime\" /> in UTC.").Append(NewLine)
            .Append("    /// </summary>").Append(NewLine)
            .Append("    [JsonIgnore]").Append(NewLine)
            .Append("    public DateTime").Append(nullable).Append(' ')
                .Append(csharpMemberName).Append("AsUtcDateTime => ")
                .Append(csharpMemberName).Append(".ToDateTimeFromUnix();").Append(NewLine);
    }

    public override string ToString() => ToClassString();
}
