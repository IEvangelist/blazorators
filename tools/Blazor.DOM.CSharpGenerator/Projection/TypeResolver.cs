// Type resolver: maps TypeScript type nodes to C# type strings.
// Hard-errors on unsupported projections (unions/intersections/generics/
// conditional/mapped/template) unless they fall into well-defined safe patterns.
// `object` is allowed ONLY for TypeScript `any`, `unknown`, and `object`.

using Blazor.DOM.CSharpGenerator.IR;

namespace Blazor.DOM.CSharpGenerator.Projection;

/// <summary>
/// Describes why a type projection failed.
/// </summary>
public sealed class TypeProjectionException(string message, string provenance)
    : Exception(message)
{
    public string Provenance { get; } = provenance;
}

/// <summary>
/// Result of projecting a TypeScript type node to C#.
/// </summary>
public sealed record TypeProjection(
    string CSharpType,
    bool IsNullable,
    bool IsCollection,
    string ProviderNote = "");

/// <summary>
/// Resolves TypeScript type nodes to deterministic C# type strings.
/// Consults the symbol index to resolve named references.
/// </summary>
public sealed class TypeResolver
{
    // Map of JS primitive checkerType/syntaxKind -> C# type
    private static readonly Dictionary<string, string> KeywordMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // The only cases where `object` is allowed
            ["AnyKeyword"]         = "object",
            ["UnknownKeyword"]     = "object",
            ["ObjectKeyword"]      = "object",
            ["any"]                = "object",
            ["unknown"]            = "object",

            // Primitives
            ["VoidKeyword"]        = "void",
            ["BooleanKeyword"]     = "bool",
            ["NumberKeyword"]      = "double",
            ["StringKeyword"]      = "string",
            ["BigIntKeyword"]      = "long",
            ["NullKeyword"]        = "null",
            ["UndefinedKeyword"]   = "null",
            ["NeverKeyword"]       = "never",

            // Checker-type aliases
            ["void"]               = "void",
            ["boolean"]            = "bool",
            ["number"]             = "double",
            ["string"]             = "string",
            ["bigint"]             = "long",
            ["null"]               = "null",
            ["undefined"]          = "null",
        };

    // GL numeric type aliases -> map to C# types
    private static readonly Dictionary<string, string> GlTypeAliases =
        new(StringComparer.Ordinal)
        {
            ["GLenum"]     = "uint",
            ["GLboolean"]  = "bool",
            ["GLbitfield"] = "uint",
            ["GLbyte"]     = "sbyte",
            ["GLshort"]    = "short",
            ["GLint"]      = "int",
            ["GLsizei"]    = "int",
            ["GLintptr"]   = "long",
            ["GLsizeiptr"] = "long",
            ["GLubyte"]    = "byte",
            ["GLushort"]   = "ushort",
            ["GLuint"]     = "uint",
            ["GLfloat"]    = "float",
            ["GLclampf"]   = "float",
        };

    private readonly IReadOnlyDictionary<string, SymbolModel> _symbolIndex;

    public TypeResolver(IReadOnlyList<SymbolModel> symbols)
    {
        _symbolIndex = symbols.ToDictionary(s => s.Name, StringComparer.Ordinal);
    }

    /// <summary>Returns true if the named symbol is in the TypeScript IR symbol index.</summary>
    public bool IsKnownSymbol(string name) => _symbolIndex.ContainsKey(name);

    /// <summary>
    /// Projects a TypeScript type node to C#. Throws <see cref="TypeProjectionException"/>
    /// for unsupported projections. Never returns <c>object</c> for supported types.
    /// </summary>
    public TypeProjection Project(TypeNode? typeNode, string provenance, int depth = 0)
    {
        if (typeNode is null)
            return new TypeProjection("void", false, false);

        if (depth > 8)
            throw new TypeProjectionException(
                $"Type recursion depth exceeded at '{provenance}'.", provenance);

        return typeNode switch
        {
            KeywordTypeNode kw => ProjectKeyword(kw, provenance),
            ReferenceTypeNode rf => ProjectReference(rf, provenance, depth),
            LiteralTypeNode lit => ProjectLiteral(lit, provenance),
            UnionTypeNode un => ProjectUnion(un, provenance, depth),
            ArrayTypeNode arr => ProjectArray(arr, provenance, depth),
            FunctionTypeNode fn => ProjectFunction(fn, provenance, depth),
            HeritageReferenceTypeNode hr => ProjectHeritageReference(hr, provenance, depth),
            IntersectionTypeNode => Fail(typeNode, provenance,
                "intersection types are not supported for C# projection"),
            TypeLiteralTypeNode => Fail(typeNode, provenance,
                "type literal (anonymous object type) is not supported for C# projection"),
            TemplateLiteralTypeNode => Fail(typeNode, provenance,
                "template literal types are not supported for C# projection"),
            QueryTypeNode => Fail(typeNode, provenance,
                "typeof query types are not supported for C# projection"),
            IndexedAccessTypeNode => Fail(typeNode, provenance,
                "indexed access types are not supported for C# projection"),
            OperatorTypeNode => Fail(typeNode, provenance,
                "type operator (keyof/readonly) is not supported for C# projection"),
            TupleTypeNode => Fail(typeNode, provenance,
                "tuple types are not supported for C# projection"),
            UnknownTypeNode u => Fail(typeNode, provenance,
                $"unknown type node kind '{u.RawKind}' cannot be projected"),
            _ => Fail(typeNode, provenance,
                $"unhandled TypeNode subtype '{typeNode.GetType().Name}'"),
        };
    }

    private TypeProjection ProjectKeyword(KeywordTypeNode kw, string provenance)
    {
        var name = kw.Name ?? kw.CheckerType ?? "";
        if (KeywordMap.TryGetValue(name, out var mapped))
        {
            if (mapped == "never")
                throw new TypeProjectionException(
                    $"'never' type at '{provenance}' cannot be projected to C#.", provenance);
            if (mapped == "null")
                return new TypeProjection("null", true, false);
            return new TypeProjection(mapped, false, false);
        }
        // Try checkerType as a fallback
        if (kw.CheckerType is not null && KeywordMap.TryGetValue(kw.CheckerType, out var fallback))
            return new TypeProjection(fallback, false, false);

        throw new TypeProjectionException(
            $"Unsupported keyword type '{name}' at '{provenance}'. " +
            "Add it to KeywordMap if it has a safe C# equivalent.", provenance);
    }

    private TypeProjection ProjectReference(ReferenceTypeNode rf, string provenance, int depth)
    {
        var name = rf.Name;

        // GL numeric aliases -> primitives (safe: no object degradation)
        if (GlTypeAliases.TryGetValue(name, out var glType))
            return new TypeProjection(glType, false, false);

        // Primitives by checker type
        if (rf.CheckerType is not null && KeywordMap.TryGetValue(rf.CheckerType, out var primFromChecker))
        {
            if (primFromChecker != "null" && primFromChecker != "never")
                return new TypeProjection(primFromChecker, false, false);
        }

        // Well-known Web API types that map to C# types
        switch (name)
        {
            case "DOMString": return new TypeProjection("string", false, false);
            case "USVString": return new TypeProjection("string", false, false);
            case "ByteString": return new TypeProjection("string", false, false);
            case "DOMHighResTimeStamp": return new TypeProjection("double", false, false);
            case "EpochTimeStamp": return new TypeProjection("long", false, false);
            case "DOMTimeStamp": return new TypeProjection("long", false, false);
        }

        // Promise<T> -> ValueTask<T>
        if (name == "Promise" && rf.TypeArguments.Count == 1)
        {
            var inner = Project(rf.TypeArguments[0], $"{provenance}/Promise<T>", depth + 1);
            var promiseType = inner.CSharpType == "void"
                ? "ValueTask"
                : $"ValueTask<{inner.CSharpType}>";
            return new TypeProjection(promiseType, false, false, "Promise<T>→ValueTask<T>");
        }

        // IAsyncEnumerable-like (ReadableStream, AsyncIterable, etc.) - defer with provenance
        if (name is "ReadableStream" or "WritableStream" or "TransformStream")
            return new TypeProjection("System.IO.Stream", false, false, $"mapped-from-{name}");

        // ArrayBuffer-like binary
        if (name is "ArrayBuffer" or "SharedArrayBuffer")
            return new TypeProjection("byte[]", false, true, $"mapped-from-{name}");

        // Typed array views
        if (name is "Uint8Array" or "Uint8ClampedArray")
            return new TypeProjection("byte[]", false, true, $"mapped-from-{name}");
        if (name is "Int8Array")
            return new TypeProjection("sbyte[]", false, true, $"mapped-from-{name}");
        if (name is "Uint16Array")
            return new TypeProjection("ushort[]", false, true, $"mapped-from-{name}");
        if (name is "Int16Array")
            return new TypeProjection("short[]", false, true, $"mapped-from-{name}");
        if (name is "Uint32Array")
            return new TypeProjection("uint[]", false, true, $"mapped-from-{name}");
        if (name is "Int32Array")
            return new TypeProjection("int[]", false, true, $"mapped-from-{name}");
        if (name is "Float32Array")
            return new TypeProjection("float[]", false, true, $"mapped-from-{name}");
        if (name is "Float64Array")
            return new TypeProjection("double[]", false, true, $"mapped-from-{name}");
        if (name is "BigInt64Array")
            return new TypeProjection("long[]", false, true, $"mapped-from-{name}");
        if (name is "BigUint64Array")
            return new TypeProjection("ulong[]", false, true, $"mapped-from-{name}");
        if (name is "DataView")
            return new TypeProjection("System.Memory<byte>", false, false, "DataView");

        // Generic collections
        if (name is "Array" or "ReadonlyArray")
        {
            if (rf.TypeArguments.Count == 1)
            {
                var elem = Project(rf.TypeArguments[0], $"{provenance}/Array<T>", depth + 1);
                return new TypeProjection($"{elem.CSharpType}[]", false, true);
            }
            return new TypeProjection("object[]", false, true, "Array<unknown>");
        }

        if (name is "Iterable" or "IterableIterator")
        {
            if (rf.TypeArguments.Count == 1)
            {
                var elem = Project(rf.TypeArguments[0], $"{provenance}/Iterable<T>", depth + 1);
                return new TypeProjection($"IEnumerable<{elem.CSharpType}>", false, true);
            }
        }

        if (name is "AsyncIterable" or "AsyncIterableIterator")
        {
            if (rf.TypeArguments.Count == 1)
            {
                var elem = Project(rf.TypeArguments[0], $"{provenance}/AsyncIterable<T>", depth + 1);
                return new TypeProjection($"IAsyncEnumerable<{elem.CSharpType}>", false, true);
            }
        }

        if (name == "Record" && rf.TypeArguments.Count == 2)
        {
            var key = Project(rf.TypeArguments[0], $"{provenance}/Record<K>", depth + 1);
            var val = Project(rf.TypeArguments[1], $"{provenance}/Record<V>", depth + 1);
            return new TypeProjection(
                $"IReadOnlyDictionary<{key.CSharpType},{val.CSharpType}>", false, false);
        }

        // EventHandler -> not projected (deferred to events phase)
        if (name is "EventHandler" or "OnErrorEventHandler" or "OnBeforeUnloadEventHandler")
            throw new TypeProjectionException(
                $"EventHandler type '{name}' at '{provenance}' is deferred to the events phase.",
                provenance);

        // If the referenced symbol exists in our symbol index use its C# name
        if (_symbolIndex.TryGetValue(name, out var sym))
        {
            var csharpName = Naming.ToCSharpTypeName(sym.Name);
            // Warn on unsupported generics
            if (rf.TypeArguments.Count > 0)
            {
                // Only allow if the symbol has type parameters (we don't emit generics yet)
                throw new TypeProjectionException(
                    $"Generic reference '{name}<...>' at '{provenance}' uses type arguments " +
                    "but generic C# emission is deferred. Add an explicit override or emit non-generic projection.",
                    provenance);
            }
            // Interface and mixin symbols are emitted as I-prefixed partial interfaces.
            var classification = sym.Semantic.Classifications.FirstOrDefault() ?? "";
            var firstDeclKind = sym.Declarations.FirstOrDefault()?.Kind ?? "";
            if (classification is "interface" or "mixin"
                || (firstDeclKind == "interface" && classification is "" or "unmatched"))
            {
                csharpName = $"I{csharpName}";
            }
            return new TypeProjection(csharpName, false, false);
        }

        throw new TypeProjectionException(
            $"Unresolved type reference '{name}' at '{provenance}'. " +
            "The symbol is not in the TypeScript symbol index and has no built-in mapping.",
            provenance);
    }

    private static TypeProjection ProjectLiteral(LiteralTypeNode lit, string provenance)
    {
        return lit.LiteralKind switch
        {
            "StringLiteral" => new TypeProjection("string", false, false,
                $"literal-string:{lit.Text}"),
            "NumericLiteral" => new TypeProjection("double", false, false,
                $"literal-number:{lit.Text}"),
            "TrueLiteral" or "FalseLiteral" => new TypeProjection("bool", false, false,
                $"literal-bool:{lit.Text}"),
            _ => throw new TypeProjectionException(
                $"Unsupported literal kind '{lit.LiteralKind}' at '{provenance}'.", provenance),
        };
    }

    private TypeProjection ProjectUnion(UnionTypeNode un, string provenance, int depth)
    {
        var types = un.Types;

        // Pattern 1: T | null | undefined  ->  T?
        var nonNull = types.Where(t =>
            t is not KeywordTypeNode kw ||
            (kw.Name != "NullKeyword" && kw.Name != "UndefinedKeyword" &&
             kw.CheckerType != "null" && kw.CheckerType != "undefined"))
            .ToList();

        // Also filter literal null
        nonNull = nonNull.Where(t =>
            t is not LiteralTypeNode lit || lit.LiteralKind != "NullLiteral")
            .ToList();

        if (nonNull.Count < types.Count && nonNull.Count == 1)
        {
            var inner = Project(nonNull[0], $"{provenance}/nullable", depth + 1);
            return inner with { IsNullable = true };
        }

        // Pattern 2: All string literals -> enum (handled at symbol level, return string here)
        if (types.All(t => t is LiteralTypeNode lt && lt.LiteralKind == "StringLiteral"))
            return new TypeProjection("string", false, false, "string-literal-union");

        // Pattern 3: T | null | undefined where T is nullable-safe -> T?
        if (nonNull.Count == 1)
        {
            var inner = Project(nonNull[0], $"{provenance}/nullable", depth + 1);
            return inner with { IsNullable = true };
        }

        // Pattern 4: EventHandler union  (EventHandler types already handled in ProjectReference)
        // Check if all non-null members are event handlers
        var nonNullTypes = types.Where(t =>
            t is not KeywordTypeNode kw ||
            (kw.Name != "NullKeyword" && kw.Name != "UndefinedKeyword"))
            .ToList();

        // Pattern 5: number | null -> double?
        if (nonNullTypes.Count == 1 && nonNullTypes[0] is KeywordTypeNode kwt &&
            (kwt.Name == "NumberKeyword" || kwt.CheckerType == "number"))
        {
            return new TypeProjection("double", true, false);
        }

        // Mixed union: fail hard with source provenance
        var typeDescriptions = string.Join(" | ", types.Select(DescribeType));
        throw new TypeProjectionException(
            $"Unsupported union type '{typeDescriptions}' at '{provenance}'. " +
            "Mixed unions (non-nullable, non-all-string-literal) cannot be projected to C# without an explicit override.",
            provenance);
    }

    private TypeProjection ProjectArray(ArrayTypeNode arr, string provenance, int depth)
    {
        var elem = Project(arr.ElementType, $"{provenance}[]", depth + 1);
        return new TypeProjection($"{elem.CSharpType}[]", false, true);
    }

    private TypeProjection ProjectFunction(FunctionTypeNode fn, string provenance, int depth)
    {
        // Function types project to Action<>/Func<> delegates
        var ret = Project(fn.ReturnType, $"{provenance}/return", depth + 1);
        var paramTypes = fn.Parameters.Select((p, i) =>
            Project(p.Type, $"{provenance}/param[{i}]", depth + 1).CSharpType).ToList();

        if (ret.CSharpType == "void")
        {
            var delegateType = paramTypes.Count == 0
                ? "Action"
                : $"Action<{string.Join(", ", paramTypes)}>";
            return new TypeProjection(delegateType, false, false);
        }
        else
        {
            var delegateType = paramTypes.Count == 0
                ? $"Func<{ret.CSharpType}>"
                : $"Func<{string.Join(", ", paramTypes)}, {ret.CSharpType}>";
            return new TypeProjection(delegateType, false, false);
        }
    }

    private TypeProjection ProjectHeritageReference(
        HeritageReferenceTypeNode hr, string provenance, int depth)
    {
        // Heritage references used in extends/implements clauses.
        // Note: the InterfaceEmitter adds the 'I' prefix itself for heritage clauses;
        // this method is used for type references, not heritage clause building.
        var name = hr.Expression;
        if (hr.TypeArguments.Count > 0)
            throw new TypeProjectionException(
                $"Generic heritage reference '{name}<...>' at '{provenance}' is not supported.",
                provenance);

        if (_symbolIndex.TryGetValue(name, out var sym))
        {
            var csharpName = Naming.ToCSharpTypeName(sym.Name);
            var classification = sym.Semantic.Classifications.FirstOrDefault() ?? "";
            var firstDeclKind = sym.Declarations.FirstOrDefault()?.Kind ?? "";
            if (classification is "interface" or "mixin"
                || (firstDeclKind == "interface" && classification is "" or "unmatched"))
            {
                csharpName = $"I{csharpName}";
            }
            return new TypeProjection(csharpName, false, false);
        }

        throw new TypeProjectionException(
            $"Unresolved heritage reference '{name}' at '{provenance}'.", provenance);
    }

    private static TypeProjection Fail(TypeNode node, string provenance, string reason)
        => throw new TypeProjectionException(
            $"Type projection failed at '{provenance}': {reason}. " +
            $"(kind={node.Kind}, checkerType={node.CheckerType ?? "(none)"})", provenance);

    private static string DescribeType(TypeNode t) => t switch
    {
        KeywordTypeNode kw => kw.Name ?? kw.CheckerType ?? "keyword",
        ReferenceTypeNode rf => rf.Name,
        LiteralTypeNode lit => lit.Text,
        _ => t.Kind,
    };
}
