// Type resolver: maps TypeScript type nodes to C# type strings.
// Hard-errors on unsupported projections (unions/intersections/generics/
// conditional/mapped/template) unless they fall into well-defined safe patterns.
// `object` is allowed ONLY for TypeScript `any`, `unknown`, and `object`.

using Blazor.DOM.CSharpGenerator.IR;

namespace Blazor.DOM.CSharpGenerator.Projection;

/// <summary>
/// Describes why a type projection failed.
/// </summary>
public class TypeProjectionException(string message, string provenance)
    : Exception(message)
{
    public string Provenance { get; } = provenance;
}

/// <summary>
/// Result of projecting a TypeScript type node to C#.
/// </summary>
public enum ClrTypeKind
{
    Value,
    Reference,
    Void,
    Null,
}

public sealed record ClrTypeIdentity(
    string CanonicalName,
    ClrTypeKind Kind,
    bool IsAwaitable = false);

public sealed record TypeProjection(
    string CSharpType,
    bool IsNullable,
    bool IsCollection,
    ClrTypeIdentity Identity,
    string ProviderNote = "",
    TransportModel? Transport = null)
{
    public string RenderedType
        => IsNullable
            && Identity.Kind is ClrTypeKind.Value or ClrTypeKind.Reference
                ? CSharpType + "?"
                : CSharpType;

    public string CanonicalType
        => Identity.Kind == ClrTypeKind.Value && IsNullable
            ? Identity.CanonicalName + "?"
            : Identity.CanonicalName;
}

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
    private readonly IReadOnlyDictionary<string, EmitterOverrideEntry> _overrides;
    private readonly string _generatedNamespace;

    public TypeResolver(
        IReadOnlyList<SymbolModel> symbols,
        IReadOnlyDictionary<string, EmitterOverrideEntry>? overrides = null,
        string generatedNamespace = "Blazor.DOM")
    {
        _symbolIndex = symbols.ToDictionary(s => s.Name, StringComparer.Ordinal);
        _overrides = overrides
            ?? new Dictionary<string, EmitterOverrideEntry>(StringComparer.Ordinal);
        _generatedNamespace = generatedNamespace;
    }

    /// <summary>Returns true if the named symbol is in the TypeScript IR symbol index.</summary>
    public bool IsKnownSymbol(string name) => _symbolIndex.ContainsKey(name);

    public bool IsInterfaceOrMixin(string name)
        => _symbolIndex.TryGetValue(name, out var sym)
           && EffectiveClassificationPolicy.Classify(sym, _overrides).Name is "interface" or "mixin";

    public string GetClassification(string name)
        => _symbolIndex.TryGetValue(name, out var sym)
            ? EffectiveClassificationPolicy.Classify(sym, _overrides).Name
            : "unknown";

    /// <summary>
    /// Returns true if the named symbol is classified as a dictionary.
    /// Dictionary symbols are emitted as C# records, so record inheritance is possible.
    /// </summary>
    public bool IsDictionarySymbol(string name)
        => _symbolIndex.TryGetValue(name, out var sym)
           && EffectiveClassificationPolicy.Classify(sym, _overrides).Name == "dictionary";

    public string GetCSharpTypeReference(string symbolName)
    {
        if (!_symbolIndex.TryGetValue(symbolName, out var symbol))
            throw new TypeProjectionException(
                $"Unresolved type symbol '{symbolName}'.",
                $"{symbolName}/symbol-resolution");

        var classification = EffectiveClassificationPolicy.Classify(symbol, _overrides).Name;
        return Naming.ToCSharpTypeReference(
            _generatedNamespace,
            symbol.Name,
            classification is "interface" or "mixin");
    }

    /// <summary>
    /// Projects a TypeScript type node to C#. Throws <see cref="TypeProjectionException"/>
    /// for unsupported projections. Never returns <c>object</c> for supported types.
    /// </summary>
    public TypeProjection Project(TypeNode? typeNode, string provenance, int depth = 0)
    {
        if (typeNode is null)
            return VoidType();

        if (depth > 8)
            throw new TypeProjectionException(
                $"Type recursion depth exceeded at '{provenance}'.", provenance);

        var projection = typeNode switch
        {
            KeywordTypeNode kw => ProjectKeyword(kw, provenance),
            ReferenceTypeNode rf => ProjectReference(rf, provenance, depth),
            LiteralTypeNode lit => ProjectLiteral(lit, provenance),
            UnionTypeNode un => ProjectUnion(un, provenance, depth),
            ArrayTypeNode arr => ProjectArray(arr, provenance, depth),
            FunctionTypeNode fn => ProjectFunction(fn, provenance, depth),
            ParenthesizedTypeNode pt => Project(pt.InnerType, provenance, depth),
            HeritageReferenceTypeNode hr => ProjectHeritageReference(hr, provenance, depth),
            IntersectionTypeNode intersection => ProjectIntersection(
                intersection,
                provenance,
                depth),
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
        return projection with { Transport = typeNode.Transport ?? projection.Transport };
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
                return NullType();
            return ProjectMappedPrimitive(mapped);
        }
        // Try checkerType as a fallback
        if (kw.CheckerType is not null && KeywordMap.TryGetValue(kw.CheckerType, out var fallback))
            return ProjectMappedPrimitive(fallback);

        throw new TypeProjectionException(
            $"Unsupported keyword type '{name}' at '{provenance}'. " +
            "Add it to KeywordMap if it has a safe C# equivalent.", provenance);
    }

    private TypeProjection ProjectReference(ReferenceTypeNode rf, string provenance, int depth)
    {
        var name = rf.Name;

        // GL numeric aliases -> primitives (safe: no object degradation)
        if (GlTypeAliases.TryGetValue(name, out var glType))
            return ValueType(glType);

        // Primitives by checker type
        if (rf.CheckerType is not null && KeywordMap.TryGetValue(rf.CheckerType, out var primFromChecker))
        {
            if (primFromChecker != "null" && primFromChecker != "never")
                return ProjectMappedPrimitive(primFromChecker);
        }

        // Well-known Web API types that map to C# types
        switch (name)
        {
            case "DOMString": return ReferenceType("string");
            case "USVString": return ReferenceType("string");
            case "ByteString": return ReferenceType("string");
            case "DOMHighResTimeStamp": return ValueType("double");
            case "EpochTimeStamp": return ValueType("long");
            case "DOMTimeStamp": return ValueType("long");
        }

        // Promise<T> -> ValueTask<T>
        if (name == "Promise" && rf.TypeArguments.Count == 1)
        {
            var inner = Project(rf.TypeArguments[0], $"{provenance}/Promise<T>", depth + 1);
            var promiseType = inner.Identity.Kind == ClrTypeKind.Void
                ? "ValueTask"
                : $"ValueTask<{inner.RenderedType}>";
            var canonicalType = inner.Identity.Kind == ClrTypeKind.Void
                ? "ValueTask"
                : $"ValueTask<{inner.CanonicalType}>";
            return ValueType(
                promiseType,
                providerNote: "Promise<T>→ValueTask<T>",
                canonicalType: canonicalType,
                isAwaitable: true);
        }

        // ReadableStream/WritableStream/TransformStream are live DOM proxy objects —
        // they must remain as generated live interface proxies, not mapped to System.IO.Stream.
        // They are resolved below via the symbol index as IReadableStream / IWritableStream / ITransformStream.
        // If they are not in the symbol index they fail with provenance (see fallthrough below).

        // ArrayBuffer-like binary
        if (name is "ArrayBuffer" or "SharedArrayBuffer")
            return ReferenceType("byte[]", true, $"mapped-from-{name}");

        // Typed array views
        if (name is "Uint8Array" or "Uint8ClampedArray")
            return ReferenceType("byte[]", true, $"mapped-from-{name}");
        if (name is "Int8Array")
            return ReferenceType("sbyte[]", true, $"mapped-from-{name}");
        if (name is "Uint16Array")
            return ReferenceType("ushort[]", true, $"mapped-from-{name}");
        if (name is "Int16Array")
            return ReferenceType("short[]", true, $"mapped-from-{name}");
        if (name is "Uint32Array")
            return ReferenceType("uint[]", true, $"mapped-from-{name}");
        if (name is "Int32Array")
            return ReferenceType("int[]", true, $"mapped-from-{name}");
        if (name is "Float32Array")
            return ReferenceType("float[]", true, $"mapped-from-{name}");
        if (name is "Float64Array")
            return ReferenceType("double[]", true, $"mapped-from-{name}");
        if (name is "BigInt64Array")
            return ReferenceType("long[]", true, $"mapped-from-{name}");
        if (name is "BigUint64Array")
            return ReferenceType("ulong[]", true, $"mapped-from-{name}");
        if (name is "DataView")
            return ValueType("System.Memory<byte>", providerNote: "DataView");

        // Generic collections
        if (name is "Array" or "ReadonlyArray")
        {
            if (rf.TypeArguments.Count == 1)
            {
                var elem = Project(rf.TypeArguments[0], $"{provenance}/Array<T>", depth + 1);
                return ReferenceType(
                    $"{elem.RenderedType}[]",
                    isCollection: true,
                    canonicalType: $"{elem.CanonicalType}[]");
            }
            throw new TypeProjectionException(
                $"Unparameterized '{name}' at '{provenance}' cannot be projected to C#. " +
                "Provide an explicit type argument or add a symbol override.", provenance);
        }

        if (name is "Iterable" or "IterableIterator")
        {
            if (rf.TypeArguments.Count == 1)
            {
                var elem = Project(rf.TypeArguments[0], $"{provenance}/Iterable<T>", depth + 1);
                return ReferenceType(
                    $"IEnumerable<{elem.RenderedType}>",
                    isCollection: true,
                    canonicalType: $"IEnumerable<{elem.CanonicalType}>");
            }
            throw new TypeProjectionException(
                $"Unparameterized '{name}' at '{provenance}' cannot be projected to C#. " +
                "Provide an explicit type argument.", provenance);
        }

        if (name is "AsyncIterable" or "AsyncIterableIterator")
        {
            if (rf.TypeArguments.Count == 1)
            {
                var elem = Project(rf.TypeArguments[0], $"{provenance}/AsyncIterable<T>", depth + 1);
                return ReferenceType(
                    $"IAsyncEnumerable<{elem.RenderedType}>",
                    isCollection: true,
                    canonicalType: $"IAsyncEnumerable<{elem.CanonicalType}>");
            }
            throw new TypeProjectionException(
                $"Unparameterized '{name}' at '{provenance}' cannot be projected to C#. " +
                "Provide an explicit type argument.", provenance);
        }

        if (name == "Record" && rf.TypeArguments.Count == 2)
        {
            var key = Project(rf.TypeArguments[0], $"{provenance}/Record<K>", depth + 1);
            var val = Project(rf.TypeArguments[1], $"{provenance}/Record<V>", depth + 1);
            return ReferenceType(
                $"IReadOnlyDictionary<{key.RenderedType},{val.RenderedType}>",
                canonicalType: $"IReadOnlyDictionary<{key.CanonicalType},{val.CanonicalType}>");
        }

        // EventHandler -> not projected (deferred to events phase)
        if (name is "EventHandler" or "OnErrorEventHandler" or "OnBeforeUnloadEventHandler")
            throw new TypeProjectionException(
                $"EventHandler type '{name}' at '{provenance}' is deferred to the events phase.",
                provenance);

        // ResolvedSymbol is authoritative for namespace-local references. Falling
        // back to Name is allowed only when the extractor did not resolve a
        // symbol, or when both spellings are identical.
        SymbolModel? sym = null;
        var resolvedName = rf.ResolvedSymbol;
        if (!string.IsNullOrWhiteSpace(resolvedName))
        {
            _symbolIndex.TryGetValue(resolvedName, out sym);
            if (sym is null
                && !string.Equals(resolvedName, name, StringComparison.Ordinal))
            {
                throw new TypeProjectionException(
                    $"Resolved type reference '{resolvedName}' (written as '{name}') at " +
                    $"'{provenance}' is not in the TypeScript symbol index.",
                    provenance);
            }
        }
        if (sym is null)
            _symbolIndex.TryGetValue(name, out sym);

        if (sym is not null)
        {
            var classification = EffectiveClassificationPolicy.Classify(sym, _overrides).Name;
            var csharpName = Naming.ToCSharpTypeReference(
                _generatedNamespace,
                sym.Name,
                classification is "interface" or "mixin");
            // Warn on unsupported generics
            if (rf.TypeArguments.Count > 0)
            {
                // Only allow if the symbol has type parameters (we don't emit generics yet)
                throw new TypeProjectionException(
                    $"Generic reference '{name}<...>' at '{provenance}' uses type arguments " +
                    "but generic C# emission is deferred. Add an explicit override or emit non-generic projection.",
                    provenance);
            }
            return classification is "enum" or "typedef"
                ? ValueType(csharpName)
                : ReferenceType(csharpName);
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
            "StringLiteral" => ReferenceType("string",
                providerNote: $"literal-string:{lit.Text}"),
            "NumericLiteral" => ValueType("double",
                providerNote: $"literal-number:{lit.Text}"),
            "TrueLiteral" or "FalseLiteral" or "TrueKeyword" or "FalseKeyword"
                => ValueType("bool", providerNote: $"literal-bool:{lit.Text}"),
            // The IR emits null/undefined literals with LiteralKind="NullKeyword"/"UndefinedKeyword"
            "NullKeyword" or "NullLiteral" => NullType(),
            "UndefinedKeyword" => NullType(),
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

        // Also filter literal null/undefined (the IR uses LiteralKind="NullKeyword" in practice)
        nonNull = nonNull.Where(t =>
            t is not LiteralTypeNode lit ||
            (lit.LiteralKind != "NullLiteral" &&
             lit.LiteralKind != "NullKeyword" &&
             lit.LiteralKind != "UndefinedKeyword"))
            .ToList();

        if (nonNull.Count < types.Count && nonNull.Count == 1)
        {
            var inner = Project(nonNull[0], $"{provenance}/nullable", depth + 1);
            return inner with { IsNullable = true };
        }

        // Pattern 2: All string literals -> enum (handled at symbol level, return string here)
        if (types.All(t => t is LiteralTypeNode lt && lt.LiteralKind == "StringLiteral"))
            return ReferenceType("string", providerNote: "string-literal-union");

        // Pattern 3: T | null | undefined where T is nullable-safe -> T?
        if (nonNull.Count == 1)
        {
            var inner = Project(nonNull[0], $"{provenance}/nullable", depth + 1);
            return inner with { IsNullable = true };
        }

        // Pattern 4: EventHandler union  (EventHandler types already handled in ProjectReference)
        // Check if all non-null members are event handlers
        var nonNullTypes = types.Where(t =>
            !(t is KeywordTypeNode kw &&
              (kw.Name is "NullKeyword" or "UndefinedKeyword" ||
               kw.CheckerType is "null" or "undefined")) &&
            !(t is LiteralTypeNode lt &&
              lt.LiteralKind is "NullLiteral" or "NullKeyword" or "UndefinedKeyword"))
            .ToList();

        // Pattern 5: number | null -> double?
        if (nonNullTypes.Count == 1 && nonNullTypes[0] is KeywordTypeNode kwt &&
            (kwt.Name == "NumberKeyword" || kwt.CheckerType == "number"))
        {
            return ValueType("double") with { IsNullable = true };
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
        return ReferenceType(
            $"{elem.RenderedType}[]",
            isCollection: true,
            canonicalType: $"{elem.CanonicalType}[]");
    }

    private TypeProjection ProjectIntersection(
        IntersectionTypeNode intersection,
        string provenance,
        int depth)
    {
        if (intersection.Types.Count == 2)
        {
            var windowReference = intersection.Types
                .OfType<ReferenceTypeNode>()
                .SingleOrDefault(reference =>
                    string.Equals(
                        reference.ResolvedSymbol ?? reference.Name,
                        "Window",
                        StringComparison.Ordinal));
            var globalThisQuery = intersection.Types
                .OfType<QueryTypeNode>()
                .SingleOrDefault(query =>
                    string.Equals(
                        query.ResolvedSymbol ?? query.ExpressionName,
                        "globalThis",
                        StringComparison.Ordinal)
                    || string.Equals(
                        query.CheckerType,
                        "typeof globalThis",
                        StringComparison.Ordinal));

            if (windowReference is not null && globalThisQuery is not null)
            {
                return Project(
                    windowReference,
                    $"{provenance}/WindowProxy",
                    depth + 1) with
                {
                    ProviderNote = "Window & typeof globalThis→WindowProxy",
                };
            }
        }

        return Fail(
            intersection,
            provenance,
            "intersection types are not supported for C# projection");
    }

    private TypeProjection ProjectFunction(FunctionTypeNode fn, string provenance, int depth)
    {
        // Function types project to Action<>/Func<> delegates.
        // Skip TypeScript's synthetic `this` parameter — it has no C# equivalent.
        var ret = Project(fn.ReturnType, $"{provenance}/return", depth + 1);
        var paramTypes = fn.Parameters
            .Where(p => p.Name != "this")
            .Select((p, i) =>
                Project(p.Type, $"{provenance}/param[{i}]", depth + 1))
            .ToList();

        if (ret.Identity.Kind == ClrTypeKind.Void)
        {
            var delegateType = paramTypes.Count == 0
                ? "Action"
                : $"Action<{string.Join(", ", paramTypes.Select(p => p.RenderedType))}>";
            var canonicalType = paramTypes.Count == 0
                ? "Action"
                : $"Action<{string.Join(", ", paramTypes.Select(p => p.CanonicalType))}>";
            return ReferenceType(delegateType, canonicalType: canonicalType);
        }
        else
        {
            var delegateType = paramTypes.Count == 0
                ? $"Func<{ret.RenderedType}>"
                : $"Func<{string.Join(", ", paramTypes.Select(p => p.RenderedType))}, {ret.RenderedType}>";
            var canonicalType = paramTypes.Count == 0
                ? $"Func<{ret.CanonicalType}>"
                : $"Func<{string.Join(", ", paramTypes.Select(p => p.CanonicalType))}, {ret.CanonicalType}>";
            return ReferenceType(delegateType, canonicalType: canonicalType);
        }
    }

    private TypeProjection ProjectHeritageReference(
        HeritageReferenceTypeNode hr, string provenance, int depth)
    {
        // Heritage references used in extends/implements clauses.
        // Note: the InterfaceEmitter adds the 'I' prefix itself for heritage clauses;
        // this method is used for type references, not heritage clause building.
        var name = hr.ResolvedSymbol ?? hr.Expression;
        if (hr.TypeArguments.Count > 0)
            throw new TypeProjectionException(
                $"Generic heritage reference '{name}<...>' at '{provenance}' is not supported.",
                provenance);

        if (_symbolIndex.TryGetValue(name, out var sym))
        {
            var classification = EffectiveClassificationPolicy.Classify(sym, _overrides).Name;
            var csharpName = Naming.ToCSharpTypeReference(
                _generatedNamespace,
                sym.Name,
                classification is "interface" or "mixin");
            return classification is "enum" or "typedef"
                ? ValueType(csharpName)
                : ReferenceType(csharpName);
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

    private static TypeProjection ProjectMappedPrimitive(string csharpType)
        => csharpType switch
        {
            "void" => VoidType(),
            "null" => NullType(),
            "string" or "object" => ReferenceType(csharpType),
            _ => ValueType(csharpType),
        };

    private static TypeProjection ValueType(
        string csharpType,
        bool isCollection = false,
        string providerNote = "",
        string? canonicalType = null,
        bool isAwaitable = false)
        => new(
            csharpType,
            false,
            isCollection,
            new ClrTypeIdentity(
                canonicalType ?? csharpType,
                ClrTypeKind.Value,
                isAwaitable),
            providerNote);

    private static TypeProjection ReferenceType(
        string csharpType,
        bool isCollection = false,
        string providerNote = "",
        string? canonicalType = null)
        => new(
            csharpType,
            false,
            isCollection,
            new ClrTypeIdentity(canonicalType ?? csharpType, ClrTypeKind.Reference),
            providerNote);

    private static TypeProjection VoidType()
        => new("void", false, false, new ClrTypeIdentity("void", ClrTypeKind.Void));

    private static TypeProjection NullType()
        => new("null", true, false, new ClrTypeIdentity("null", ClrTypeKind.Null));
}
