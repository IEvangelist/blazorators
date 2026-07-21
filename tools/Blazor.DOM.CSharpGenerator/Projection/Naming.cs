// Deterministic naming conventions for C# identifiers generated from TypeScript/WebIDL names.
// All decisions are centralised here so every emitter stays consistent.

using System.Text;
using System.Text.RegularExpressions;

namespace Blazor.DOM.CSharpGenerator.Projection;

/// <summary>
/// Centralises deterministic naming, keyword escaping, collision handling,
/// and idiomatic mapping from JS/WebIDL identifiers to C# identifiers.
/// </summary>
public static class Naming
{
    // C# reserved keywords that must be escaped with @.
    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "abstract","as","base","bool","break","byte","case","catch","char","checked",
        "class","const","continue","decimal","default","delegate","do","double","else",
        "enum","event","explicit","extern","false","finally","fixed","float","for",
        "foreach","goto","if","implicit","in","int","interface","internal","is","lock",
        "long","namespace","new","null","object","operator","out","override","params",
        "private","protected","public","readonly","ref","return","sbyte","sealed",
        "short","sizeof","stackalloc","static","string","struct","switch","this","throw",
        "true","try","typeof","uint","ulong","unchecked","unsafe","ushort","using",
        "virtual","void","volatile","while",
    };

    // Contextual keywords that should be escaped when used as identifiers.
    private static readonly HashSet<string> CSharpContextualKeywords = new(StringComparer.Ordinal)
    {
        "add","alias","ascending","async","await","by","descending","dynamic","equals",
        "from","get","global","group","init","into","join","let","managed","nameof",
        "nint","not","notnull","nuint","on","orderby","partial","record","remove",
        "required","scoped","select","set","unmanaged","value","var","when","where","with",
        "yield",
    };

    /// <summary>
    /// Converts a TypeScript/WebIDL name to a PascalCase C# identifier,
    /// escaping reserved keywords. Namespace-qualified names (A.B) become A_B.
    /// </summary>
    public static string ToCSharpTypeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "_Empty";

        // Handle namespace-qualified names (e.g. CSS.Hz -> CSS_Hz)
        var sanitized = name.Replace('.', '_').Replace('-', '_');

        // Already PascalCase? Just escape if needed.
        var result = SanitizeIdentifier(sanitized);
        return EscapeKeyword(result);
    }

    /// <summary>
    /// Returns the unqualified C# type name for a TypeScript symbol. TypeScript
    /// namespace segments are represented by generated C# namespaces instead of
    /// being flattened into the public type name.
    /// </summary>
    public static string ToCSharpSimpleTypeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "_Empty";
        var separator = name.LastIndexOf('.');
        return ToCSharpTypeName(separator >= 0 ? name[(separator + 1)..] : name);
    }

    /// <summary>
    /// Maps a qualified TypeScript symbol to a collision-safe generated namespace.
    /// For example, WebAssembly.Module becomes
    /// Blazor.DOM.Namespaces.WebAssembly.
    /// </summary>
    public static string ToGeneratedNamespace(string rootNamespace, string symbolName)
    {
        var separator = symbolName.LastIndexOf('.');
        if (separator < 0)
            return rootNamespace;

        var namespaceSegments = symbolName[..separator]
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(ToNamespaceSegment);
        return $"{rootNamespace}.Namespaces.{string.Join(".", namespaceSegments)}";
    }

    /// <summary>
    /// Returns a fully-qualified C# reference for a TypeScript symbol. Qualified
    /// TypeScript symbols always use global:: so System and generated namespace
    /// names cannot be resolved accidentally through the current C# scope.
    /// </summary>
    public static string ToCSharpTypeReference(
        string rootNamespace,
        string symbolName,
        bool interfaceType)
    {
        var simpleName = ToCSharpSimpleTypeName(symbolName);
        var typeName = interfaceType ? $"I{simpleName}" : simpleName;
        return symbolName.Contains('.', StringComparison.Ordinal)
            ? $"global::{ToGeneratedNamespace(rootNamespace, symbolName)}.{typeName}"
            : typeName;
    }

    /// <summary>
    /// Returns the deterministic output subdirectory for a generated symbol.
    /// </summary>
    public static string ToOutputSubdirectory(string category, string symbolName)
    {
        var separator = symbolName.LastIndexOf('.');
        if (separator < 0)
            return category;

        var namespacePath = string.Join(
            Path.DirectorySeparatorChar,
            symbolName[..separator]
                .Split('.', StringSplitOptions.RemoveEmptyEntries)
                .Select(ToNamespaceSegment));
        return Path.Combine(category, "Namespaces", namespacePath);
    }

    public static string? GetTypeScriptNamespace(string symbolName)
    {
        var separator = symbolName.LastIndexOf('.');
        return separator < 0 ? null : symbolName[..separator];
    }

    /// <summary>
    /// Converts a JS member name to a PascalCase C# property/method name.
    /// </summary>
    public static string ToCSharpMemberName(string jsName)
    {
        if (string.IsNullOrEmpty(jsName)) return "_Empty";
        var pascal = ToPascalCase(jsName);
        return EscapeKeyword(pascal);
    }

    /// <summary>
    /// Converts a JS parameter name to a camelCase C# parameter name.
    /// </summary>
    public static string ToCSharpParameterName(string jsName)
    {
        if (string.IsNullOrEmpty(jsName)) return "_empty";
        var camel = ToCamelCase(jsName);
        return EscapeKeyword(camel);
    }

    public static string ToCSharpTypeParameterName(string typeScriptName)
    {
        if (string.IsNullOrWhiteSpace(typeScriptName))
            return "_";
        return ToCSharpTypeName(typeScriptName);
    }

    /// <summary>
    /// Converts a string-literal enum value to a valid C# enum member name.
    /// E.g. "audio/mpeg" -> "AudioMpeg", "end-of-line" -> "EndOfLine", "2d" -> "_2D".
    /// </summary>
    public static string ToEnumMemberName(string literalValue)
    {
        if (string.IsNullOrEmpty(literalValue)) return "_Empty";

        // Strip surrounding quotes if present
        var raw = literalValue.Trim('"', '\'');
        if (raw.Length == 0) return "_Empty";

        // Split on non-alphanumeric chars AND digit/alpha boundaries
        var parts = SplitOnBoundaries(raw)
            .Where(p => p.Length > 0)
            .Select(p =>
            {
                // Uppercase first letter of each segment
                if (char.IsLetter(p[0]))
                    return char.ToUpperInvariant(p[0]) + p[1..];
                return p; // keep digit segments as-is
            })
            .ToArray();

        var name = string.Concat(parts);
        if (name.Length == 0) return "_";
        if (char.IsDigit(name[0])) name = "_" + name;
        return EscapeKeyword(name);
    }

    private static IEnumerable<string> SplitOnBoundaries(string input)
    {
        // Split on separator chars first
        var separatorSplit = Regex.Split(input, @"[^a-zA-Z0-9]+");
        foreach (var segment in separatorSplit)
        {
            if (segment.Length == 0) continue;
            // Further split on digit/alpha boundaries within each segment
            var sub = Regex.Split(segment, @"(?<=\d)(?=[a-zA-Z])|(?<=[a-zA-Z])(?=\d)");
            foreach (var s in sub)
                if (s.Length > 0) yield return s;
        }
    }

    /// <summary>
    /// Escapes a C# identifier that is a reserved or contextual keyword.
    /// </summary>
    public static string EscapeKeyword(string name)
    {
        if (CSharpKeywords.Contains(name) || CSharpContextualKeywords.Contains(name))
            return "@" + name;
        return name;
    }

    private static string ToPascalCase(string name)
    {
        if (name.Length == 0) return name;

        // Already starts uppercase and no separators -> likely already PascalCase
        if (char.IsUpper(name[0])
            && !name.Contains('_')
            && !name.Contains('-')
            && !name.Contains('.'))
            return name;

        var sb = new StringBuilder(name.Length);
        bool nextUpper = true;
        foreach (char c in name)
        {
            if (c == '_' || c == '-' || c == '.')
            {
                nextUpper = true;
            }
            else if (nextUpper)
            {
                sb.Append(char.ToUpperInvariant(c));
                nextUpper = false;
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static string ToCamelCase(string name)
    {
        var pascal = ToPascalCase(name);
        if (pascal.Length == 0) return pascal;
        return char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    private static string SanitizeIdentifier(string name)
    {
        if (name.Length == 0) return "_";
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                sb.Append(c);
            else
                sb.Append('_');
        }
        var result = sb.ToString();
        if (char.IsDigit(result[0])) result = "_" + result;
        return result;
    }

    internal static string ToNamespaceSegment(string segment)
    {
        var sanitized = SanitizeIdentifier(segment.Replace('-', '_'));
        return sanitized switch
        {
            "System" => "TypeScriptSystem",
            "Microsoft" => "TypeScriptMicrosoft",
            _ => EscapeKeyword(sanitized),
        };
    }

    /// <summary>
    /// Resolves collisions in a set of names by appending numeric suffixes.
    /// Returns a new list in the same order with collisions disambiguated.
    /// </summary>
    public static IReadOnlyList<string> ResolveCollisions(IReadOnlyList<string> names)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var n in names)
            counts[n] = counts.TryGetValue(n, out var c) ? c + 1 : 1;

        var result = new List<string>(names.Count);
        foreach (var n in names)
        {
            if (counts[n] == 1)
            {
                result.Add(n);
            }
            else
            {
                var idx = seen.TryGetValue(n, out var s) ? s : 0;
                result.Add(idx == 0 ? n : $"{n}_{idx}");
                seen[n] = idx + 1;
            }
        }
        return result;
    }
}
