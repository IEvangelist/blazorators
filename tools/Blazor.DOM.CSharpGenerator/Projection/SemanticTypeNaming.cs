using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Blazor.DOM.CSharpGenerator.Projection;

internal static partial class SemanticTypeNaming
{
    private static readonly HashSet<string> WrapperSegments =
        new(StringComparer.Ordinal)
        {
            "Array",
            "ArrayIterator",
            "AsyncIterable",
            "AsyncIterator",
            "Exclude",
            "Iterable",
            "IterableIterator",
            "Iterator",
            "Map",
            "MapIterator",
            "Promise",
            "PromiseLike",
            "ReadonlyArray",
            "Record",
            "Set",
            "SetIterator",
        };

    private static readonly HashSet<string> TechnicalSegments =
        new(StringComparer.Ordinal)
        {
            "(unnamed)",
            "constraint",
            "globalFunction",
            "intersection",
            "literal-domain",
            "overload",
            "property",
            "typeAlias",
            "typeLiteral",
        };

    public static string BuildSynthesizedTypeName(
        string provenance,
        string kind,
        string semanticDetail = "")
    {
        var context = DescribeProvenance(provenance);
        var suffix = kind switch
        {
            "Constraint" => "Constraint",
            "Intersection" => "Intersection",
            "Numeric" => "NumericValue",
            "Record" => "Record",
            "ReferenceTuple" => "ReferenceTuple",
            "String" => "String",
            "StringPattern" => "StringPattern",
            "Tuple" => "Tuple",
            "Union" => "Union",
            _ => DescribeIdentifier(kind),
        };

        var name = context;
        if (!string.IsNullOrEmpty(semanticDetail)
            && !name.EndsWith(semanticDetail, StringComparison.Ordinal))
        {
            name += semanticDetail;
        }
        if (!name.EndsWith(suffix, StringComparison.Ordinal))
            name += suffix;

        if (name.Length == 0)
            throw new ArgumentException("A semantic synthesized type name cannot be empty.");
        if (name.Length > 180)
        {
            throw new ArgumentException(
                $"Semantic synthesized type name '{name}' exceeds the 180-character limit. " +
                "Refine its semantic naming rule instead of truncating it.");
        }
        return name;
    }

    public static string DescribeProjection(TypeProjection projection)
        => DescribeRenderedType(projection.RenderedType);

    public static string DescribeContextualProjection(
        TypeProjection projection,
        string provenance)
    {
        var descriptor = DescribeProjection(projection);
        var context = DescribeProvenance(provenance);
        if (descriptor.StartsWith(context, StringComparison.Ordinal)
            && descriptor.Length > context.Length)
        {
            return SimplifyContextualDescriptor(descriptor[context.Length..]);
        }

        var owner = provenance
            .Split(['/', '['], 2, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (owner is null)
            return descriptor;

        var ownerDescriptor = DescribeIdentifier(owner);
        if (!descriptor.StartsWith(ownerDescriptor, StringComparison.Ordinal)
            || descriptor.Length == ownerDescriptor.Length)
        {
            return descriptor;
        }

        return SimplifyContextualDescriptor(descriptor[ownerDescriptor.Length..]);
    }

    public static string DescribeRenderedType(string renderedType)
    {
        var type = renderedType.Trim()
            .Replace("global::", "", StringComparison.Ordinal);
        if (type.EndsWith("[]", StringComparison.Ordinal))
            return $"{DescribeRenderedType(type[..^2])}Array";
        if (type.EndsWith("?", StringComparison.Ordinal))
            return $"Nullable{DescribeRenderedType(type[..^1])}";

        var genericStart = FindTopLevelGenericStart(type);
        if (genericStart >= 0 && type.EndsWith('>'))
        {
            var genericName = DescribeSimpleType(type[..genericStart]);
            var arguments = SplitGenericArguments(type[(genericStart + 1)..^1])
                .Select(DescribeRenderedType)
                .ToList();
            if (genericName == "Nullable" && arguments.Count == 1)
                return $"Nullable{arguments[0]}";
            if (genericName == "Memory" && arguments.Count == 1)
                return $"{arguments[0]}Memory";
            if (genericName is "ReadOnlyList" or "List"
                or "BrowserIterable" or "BrowserPromiseLike"
                && arguments.Count == 1)
            {
                return $"{arguments[0]}{genericName}";
            }
            return $"{string.Join("And", arguments)}{genericName}";
        }

        return DescribeSimpleType(type);
    }

    public static string DescribeLiteral(string literal)
    {
        var value = literal.Trim().Trim('"', '\'');
        if (value.Length == 0)
            return "Empty";
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            var finalSegment = uri.Segments
                .LastOrDefault(segment => segment != "/")?
                .Trim('/');
            value = string.IsNullOrEmpty(finalSegment)
                ? uri.Host
                : finalSegment;
        }
        if (value == "*")
            return "Wildcard";

        var descriptor = Naming.ToEnumMemberName(value)
            .TrimStart('@', '_');
        if (descriptor.Length == 0)
            return "Value";
        if (char.IsDigit(descriptor[0]))
        {
            if (descriptor.Length > 1 && char.IsLetter(descriptor[1]))
            {
                descriptor = descriptor[0] switch
                {
                    '0' => $"Zero{descriptor[1..]}",
                    '1' => $"One{descriptor[1..]}",
                    '2' => $"Two{descriptor[1..]}",
                    '3' => $"Three{descriptor[1..]}",
                    '4' => $"Four{descriptor[1..]}",
                    '5' => $"Five{descriptor[1..]}",
                    '6' => $"Six{descriptor[1..]}",
                    '7' => $"Seven{descriptor[1..]}",
                    '8' => $"Eight{descriptor[1..]}",
                    '9' => $"Nine{descriptor[1..]}",
                    _ => $"Value{descriptor}",
                };
            }
            else
            {
                descriptor = $"Value{descriptor}";
            }
        }
        return descriptor;
    }

    public static string DescribeStringDomain(IReadOnlyList<string> values)
        => values.Count <= 4
            ? string.Join("Or", values.Select(DescribeLiteral))
            : "Values";

    public static string DescribeNumericDomain(IReadOnlyList<double> values)
        => values.Count <= 4
            ? string.Join("Or", values.Select(DescribeNumber))
            : "Values";

    public static string DescribeTuple(
        IReadOnlyList<(string SourceName, string CSharpName, TypeProjection Projection)> elements)
        => string.Join(
            "And",
            elements.Select(element =>
                IsOrdinalTupleName(element.SourceName)
                    ? DescribeProjection(element.Projection)
                    : DescribeIdentifier(element.CSharpName)));

    public static string DescribeRecord(
        string provenance,
        IReadOnlyList<(string CSharpName, TypeProjection Projection)> properties)
        => properties.Count <= 4
            ? string.Join(
                "And",
                properties.Select(property =>
                    $"{DescribeIdentifier(property.CSharpName)}" +
                    $"{DescribeContextualProjection(property.Projection, provenance)}"))
            : "Properties";

    public static string DescribeUnion(IReadOnlyList<string> armNames)
        => armNames.Count <= 4
            ? string.Join("Or", armNames.Select(DescribeIdentifier))
            : "";

    public static string DescribeSourceShape(string sourceShape)
        => DescribeIdentifier(sourceShape);

    private static string DescribeProvenance(string provenance)
    {
        var builder = new StringBuilder();
        var segments = provenance.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            if (segment == "[]")
            {
                Append(builder, "Items");
                continue;
            }
            var ordinal = OrdinalSegmentRegex().Match(segment);
            if (ordinal.Success)
            {
                if (ordinal.Groups["array"].Success)
                    Append(builder, "Items");
                continue;
            }
            if (IndexedSegmentRegex().IsMatch(segment))
            {
                if (segment.StartsWith("typeArgument", StringComparison.Ordinal))
                    Append(builder, "Element");
                continue;
            }
            if (TechnicalSegments.Contains(segment))
                continue;

            var generic = GenericSegmentRegex().Match(segment);
            if (generic.Success
                && WrapperSegments.Contains(generic.Groups["name"].Value))
            {
                continue;
            }

            switch (segment)
            {
                case "constructSignature":
                    Append(builder, "Constructor");
                    break;
                case "callSignature":
                    Append(builder, "Call");
                    break;
                case "indexSignature":
                    Append(builder, "Index");
                    break;
                case "return":
                    Append(builder, "Result");
                    break;
                default:
                    var array = segment.EndsWith("[]", StringComparison.Ordinal);
                    var value = array ? segment[..^2] : segment;
                    Append(builder, DescribeIdentifier(value));
                    if (array)
                        Append(builder, "Items");
                    break;
            }
        }
        return builder.Length == 0 ? "Anonymous" : builder.ToString();
    }

    private static string DescribeNumber(double value)
    {
        if (value == 0)
            return "Zero";
        if (value == 1)
            return "One";
        if (value == 2)
            return "Two";
        if (value == -1)
            return "NegativeOne";

        var invariant = value.ToString("R", CultureInfo.InvariantCulture);
        var descriptor = invariant
            .Replace("-", "Negative", StringComparison.Ordinal)
            .Replace(".", "Point", StringComparison.Ordinal)
            .Replace("+", "Positive", StringComparison.Ordinal);
        return $"Value{descriptor}";
    }

    private static string SimplifyContextualDescriptor(string descriptor)
    {
        var contextual = descriptor.StartsWith("Items", StringComparison.Ordinal)
            && descriptor.Length > "Items".Length
                ? descriptor["Items".Length..]
                : descriptor;
        foreach (var container in new[]
        {
            "Array",
            "BrowserIterable",
            "BrowserPromiseLike",
            "List",
            "ReadOnlyList",
        })
        {
            var marker = $"Union{container}";
            if (contextual.EndsWith(marker, StringComparison.Ordinal))
            {
                return $"{contextual[..^marker.Length]}{container}";
            }
        }
        return contextual;
    }

    private static string DescribeSimpleType(string type)
    {
        var simple = type[(type.LastIndexOf('.') + 1)..];
        var known = simple switch
        {
            "bool" or "Boolean" => "Boolean",
            "byte" or "Byte" => "Byte",
            "double" or "Double" or "float" or "Single" => "Number",
            "int" or "Int32" or "long" or "Int64" => "Integer",
            "object" or "Object" => "Object",
            "string" or "String" => "String",
            _ => "",
        };
        if (known.Length > 0)
            return known;

        if (simple.Length > 1
            && simple[0] == 'I'
            && char.IsUpper(simple[1]))
        {
            simple = simple[1..];
        }
        return DescribeIdentifier(simple);
    }

    private static string DescribeIdentifier(string value)
    {
        var descriptor = Naming.ToEnumMemberName(value)
            .Replace("@", "", StringComparison.Ordinal)
            .TrimStart('_');
        if (descriptor.Length == 0)
            return "Value";
        return char.IsDigit(descriptor[0]) ? $"Value{descriptor}" : descriptor;
    }

    private static bool IsOrdinalTupleName(string name)
        => TupleOrdinalRegex().IsMatch(name);

    private static int FindTopLevelGenericStart(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '<')
                return index;
        }
        return -1;
    }

    private static IReadOnlyList<string> SplitGenericArguments(string value)
    {
        var arguments = new List<string>();
        var start = 0;
        var depth = 0;
        for (var index = 0; index < value.Length; index++)
        {
            switch (value[index])
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    depth--;
                    break;
                case ',' when depth == 0:
                    arguments.Add(value[start..index].Trim());
                    start = index + 1;
                    break;
            }
        }
        arguments.Add(value[start..].Trim());
        return arguments;
    }

    private static void Append(StringBuilder builder, string value)
    {
        if (value.Length > 0 && !builder.ToString().EndsWith(value, StringComparison.Ordinal))
            builder.Append(value);
    }

    [GeneratedRegex(@"^(?:decl|member|parameter|arm)\[\d+\](?<array>\[\])?$")]
    private static partial Regex OrdinalSegmentRegex();

    [GeneratedRegex(@"^(?:typeArgument|typeParameter)\[\d+\]$")]
    private static partial Regex IndexedSegmentRegex();

    [GeneratedRegex(@"^(?<name>[A-Za-z_][A-Za-z0-9_.]*)<.*>$")]
    private static partial Regex GenericSegmentRegex();

    [GeneratedRegex(@"^item\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex TupleOrdinalRegex();
}
