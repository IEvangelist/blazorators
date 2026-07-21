// Type resolver: maps TypeScript type nodes to C# type strings.
// Hard-errors on unsupported projections (unions/intersections/advanced generics/
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
    bool IsAwaitable = false,
    int GenericArity = 0,
    IReadOnlyList<ClrTypeIdentity>? TypeArguments = null,
    bool IsTypeParameter = false);

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

    public int GetGenericArity(string symbolName)
    {
        if (!_symbolIndex.TryGetValue(symbolName, out var symbol))
            throw new TypeProjectionException(
                $"Unresolved type symbol '{symbolName}'.",
                $"{symbolName}/symbol-resolution");
        return GetSymbolTypeParameters(symbol, $"{symbolName}/typeParameters").Count;
    }

    public GenericDeclaration CreateGenericDeclaration(
        IReadOnlyList<TypeParameterModel> parameters,
        string provenance,
        GenericScope? parent = null,
        string canonicalPrefix = "!")
    {
        var scope = GenericScope.Create(
            parameters,
            provenance,
            parent,
            canonicalPrefix);
        var clauses = new List<string>();
        var canonical = new List<string>();
        var defaults = new List<string>();
        foreach (var binding in scope.Parameters)
        {
            var constraint = ProjectConstraint(binding, scope, provenance);
            if (constraint is not null)
            {
                clauses.Add($"where {binding.CSharpName} : {constraint.Value.Rendered}");
                canonical.Add(
                    $"{binding.CanonicalIdentity}:{constraint.Value.Canonical}");
            }
            else
            {
                canonical.Add($"{binding.CanonicalIdentity}:*");
            }

            if (binding.Model.Default is not null)
            {
                TypeProjection projectedDefault;
                try
                {
                    projectedDefault = Project(
                        binding.Model.Default,
                        $"{provenance}/typeParameter[{binding.Model.Ordinal}]/default",
                        scope);
                }
                catch (GenericDeferralException)
                {
                    throw;
                }
                catch (TypeProjectionException exception)
                {
                    throw new GenericDeferralException(
                        $"Generic default for '{binding.SourceName}' at '{provenance}' " +
                        $"cannot be represented faithfully in C#: {exception.Message}",
                        exception.Provenance,
                        "generic-defaults");
                }
                defaults.Add(
                    $"{binding.SourceName} = {projectedDefault.RenderedType}");
            }
        }

        var list = scope.Parameters.Count == 0
            ? ""
            : $"<{string.Join(", ", scope.Parameters.Select(
                parameter => parameter.CSharpName))}>";
        return new GenericDeclaration(
            scope,
            list,
            clauses,
            string.Join(";", canonical),
            defaults);
    }

    public GenericDeclaration CreateGenericDeclaration(
        SymbolModel symbol,
        string provenance,
        GenericScope? parent = null,
        string canonicalPrefix = "!")
        => CreateGenericDeclaration(
            GetSymbolTypeParameters(symbol, provenance),
            provenance,
            parent,
            canonicalPrefix);

    public IReadOnlyList<GenericDeclaration> CreateDefaultExpandedDeclarations(
        IReadOnlyList<TypeParameterModel> parameters,
        string provenance,
        GenericScope? parent = null,
        string canonicalPrefix = "!")
    {
        if (parameters.Count == 0 || parameters.All(parameter => parameter.Default is null))
            return [];
        var firstDefault = parameters
            .Select((parameter, index) => (parameter, index))
            .First(item => item.parameter.Default is not null)
            .index;
        if (parameters.Skip(firstDefault).Any(parameter => parameter.Default is null))
        {
            throw new GenericDeferralException(
                $"Generic defaults at '{provenance}' are not trailing and cannot be " +
                "expanded into deterministic CLR overloads.",
                $"{provenance}/typeParameters",
                "generic-method-defaults");
        }

        var fullScope = GenericScope.Create(
            parameters,
            provenance,
            parent,
            canonicalPrefix: "^");
        var expansions = new List<GenericDeclaration>();
        for (var retained = firstDefault; retained < parameters.Count; retained++)
        {
            var retainedDeclaration = CreateGenericDeclaration(
                parameters.Take(retained).ToList(),
                provenance,
                parent,
                canonicalPrefix);
            var substitutions = fullScope.Parameters
                .Select((parameter, index) => index < retained
                    ? TypeParameter(retainedDeclaration.Scope.Parameters[index])
                    : TypeParameter(parameter))
                .ToList();
            for (var index = retained; index < parameters.Count; index++)
            {
                var parameter = parameters[index];
                var defaultScope = fullScope.WithSubstitutions(substitutions);
                TypeProjection projectedDefault;
                try
                {
                    projectedDefault = Project(
                        parameter.Default,
                        $"{provenance}/defaultExpansion[{retained}]/" +
                        $"typeParameter[{parameter.Ordinal}]",
                        defaultScope);
                }
                catch (GenericDeferralException)
                {
                    throw;
                }
                catch (TypeProjectionException exception)
                {
                    throw new GenericDeferralException(
                        $"Generic method default for '{parameter.Name}' at " +
                        $"'{provenance}' cannot be expanded faithfully: " +
                        exception.Message,
                        exception.Provenance,
                        "generic-method-defaults");
                }
                if (ContainsUnsubstitutedTargetParameter(projectedDefault.Identity))
                {
                    var defaultProvenance =
                        $"{provenance}/defaultExpansion[{retained}]/" +
                        $"typeParameter[{parameter.Ordinal}]";
                    throw new GenericDeferralException(
                        $"Generic method default for '{parameter.Name}' at " +
                        $"'{defaultProvenance}' is cyclic or depends on an omitted target " +
                        "parameter.",
                        defaultProvenance,
                        "generic-method-defaults");
                }
                substitutions[index] = projectedDefault;
            }
            expansions.Add(retainedDeclaration with
            {
                Scope = fullScope.WithSubstitutions(substitutions),
                DefaultNotes = [],
            });
        }
        return expansions;
    }

    /// <summary>
    /// Projects a TypeScript type node to C#. Throws <see cref="TypeProjectionException"/>
    /// for unsupported projections. Never returns <c>object</c> for supported types.
    /// </summary>
    public TypeProjection Project(
        TypeNode? typeNode,
        string provenance,
        GenericScope? scope = null,
        int depth = 0)
    {
        if (typeNode is null)
            return VoidType();

        if (depth > 8)
            throw new TypeProjectionException(
                $"Type recursion depth exceeded at '{provenance}'.", provenance);

        var projection = typeNode switch
        {
            KeywordTypeNode kw => ProjectKeyword(kw, provenance),
            ReferenceTypeNode rf => ProjectReference(rf, provenance, scope, depth),
            LiteralTypeNode lit => ProjectLiteral(lit, provenance),
            UnionTypeNode un => ProjectUnion(un, provenance, scope, depth),
            ArrayTypeNode arr => ProjectArray(arr, provenance, scope, depth),
            FunctionTypeNode fn => ProjectFunction(fn, provenance, scope, depth),
            ParenthesizedTypeNode pt => Project(pt.InnerType, provenance, scope, depth),
            HeritageReferenceTypeNode hr => ProjectHeritageReference(
                hr,
                provenance,
                scope,
                depth),
            IntersectionTypeNode intersection => ProjectIntersection(
                intersection,
                provenance,
                scope,
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
        ValidateJsonGenericTransport(typeNode, provenance);
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

    private TypeProjection ProjectReference(
        ReferenceTypeNode rf,
        string provenance,
        GenericScope? scope,
        int depth)
    {
        var name = rf.Name;
        var isGlobalBuiltIn = IsGlobalBuiltInReference(rf);

        if (scope?.TryResolve(name, rf.ResolvedSymbol, out var parameter) == true)
        {
            if (rf.TypeArguments.Count != 0)
            {
                throw new TypeProjectionException(
                    $"Type parameter '{name}' at '{provenance}' cannot receive type arguments.",
                    provenance);
            }
            return parameter.Substitution ?? TypeParameter(parameter);
        }

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
        if (isGlobalBuiltIn && name == "Promise")
        {
            EnsureSupportedStandardContainerTransport(rf, provenance, "promise-transport");
            if (rf.TypeArguments.Count != 1)
                throw ArityError(name, 1, rf.TypeArguments.Count, provenance);
            var inner = Project(
                rf.TypeArguments[0],
                $"{provenance}/Promise<T>",
                scope,
                depth + 1);
            if (inner.Identity.Kind == ClrTypeKind.Null)
                throw IllegalGenericArgument(name, inner, provenance, 0);
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
                isAwaitable: true,
                typeArguments: inner.Identity.Kind == ClrTypeKind.Void
                    ? []
                    : [inner.Identity]);
        }

        // ReadableStream/WritableStream/TransformStream are live DOM proxy objects —
        // they must remain as generated live interface proxies, not mapped to System.IO.Stream.
        // They are resolved below via the symbol index as IReadableStream / IWritableStream / ITransformStream.
        // If they are not in the symbol index they fail with provenance (see fallthrough below).

        // ArrayBuffer-like binary
        if (name is "ArrayBuffer" or "SharedArrayBuffer")
        {
            if (rf.TypeArguments.Count != 0)
                throw ArityError(name, 0, rf.TypeArguments.Count, provenance);
            return ReferenceType("byte[]", true, $"mapped-from-{name}");
        }

        // Typed array views
        if (name is "Uint8Array" or "Uint8ClampedArray")
        {
            ValidateOptionalBufferArgument(rf, provenance);
            return ReferenceType("byte[]", true, $"mapped-from-{name}");
        }
        if (name is "Int8Array")
        {
            ValidateOptionalBufferArgument(rf, provenance);
            return ReferenceType("sbyte[]", true, $"mapped-from-{name}");
        }
        if (name is "Uint16Array")
        {
            ValidateOptionalBufferArgument(rf, provenance);
            return ReferenceType("ushort[]", true, $"mapped-from-{name}");
        }
        if (name is "Int16Array")
        {
            ValidateOptionalBufferArgument(rf, provenance);
            return ReferenceType("short[]", true, $"mapped-from-{name}");
        }
        if (name is "Uint32Array")
        {
            ValidateOptionalBufferArgument(rf, provenance);
            return ReferenceType("uint[]", true, $"mapped-from-{name}");
        }
        if (name is "Int32Array")
        {
            ValidateOptionalBufferArgument(rf, provenance);
            return ReferenceType("int[]", true, $"mapped-from-{name}");
        }
        if (name is "Float32Array")
        {
            ValidateOptionalBufferArgument(rf, provenance);
            return ReferenceType("float[]", true, $"mapped-from-{name}");
        }
        if (name is "Float64Array")
        {
            ValidateOptionalBufferArgument(rf, provenance);
            return ReferenceType("double[]", true, $"mapped-from-{name}");
        }
        if (name is "BigInt64Array")
        {
            ValidateOptionalBufferArgument(rf, provenance);
            return ReferenceType("long[]", true, $"mapped-from-{name}");
        }
        if (name is "BigUint64Array")
        {
            ValidateOptionalBufferArgument(rf, provenance);
            return ReferenceType("ulong[]", true, $"mapped-from-{name}");
        }
        if (name is "DataView")
            return ValueType("System.Memory<byte>", providerNote: "DataView");

        // Generic collections
        if (isGlobalBuiltIn && name is "Array" or "ReadonlyArray")
        {
            EnsureSupportedStandardContainerTransport(
                rf,
                provenance,
                "standard-container-transport");
            if (rf.TypeArguments.Count == 1)
            {
                var elem = Project(
                    rf.TypeArguments[0],
                    $"{provenance}/Array<T>",
                    scope,
                    depth + 1);
                ValidateGenericArgument(name, elem, provenance, 0);
                return ReferenceType(
                    $"{elem.RenderedType}[]",
                    isCollection: true,
                    canonicalType: $"{elem.CanonicalType}[]",
                    typeArguments: [elem.Identity]);
            }
            throw new TypeProjectionException(
                $"Unparameterized '{name}' at '{provenance}' cannot be projected to C#. " +
                "Provide an explicit type argument or add a symbol override.", provenance);
        }

        if (isGlobalBuiltIn && name is
            "IteratorObject" or
            "AsyncIteratorObject" or
            "Iterator" or
            "AsyncIterator")
        {
            EnsureSupportedStandardContainerTransport(
                rf,
                provenance,
                "iterator-transport");
            if (rf.TypeArguments.Count is not (1 or 3))
            {
                throw new TypeProjectionException(
                    $"{name} at '{provenance}' requires one element argument or " +
                    "the complete three-argument iterator form.",
                    provenance);
            }
            if (rf.TypeArguments.Count == 3
                && (!IsDefaultIteratorReturn(rf.TypeArguments[1])
                    || !IsUnknownLike(rf.TypeArguments[2])))
            {
                throw new GenericDeferralException(
                    $"{name} at '{provenance}' has non-standard return/next " +
                    "arguments that cannot be represented by the CLR enumerable contract.",
                    provenance,
                    "advanced-iterator-contracts");
            }
            var item = Project(
                rf.TypeArguments[0],
                $"{provenance}/{name}<T>",
                scope,
                depth + 1);
            ValidateGenericArgument(name, item, provenance, 0);
            var clrName = name is "AsyncIteratorObject" or "AsyncIterator"
                ? "IAsyncEnumerable"
                : "IEnumerable";
            return ReferenceType(
                $"{clrName}<{item.RenderedType}>",
                isCollection: true,
                canonicalType: $"{clrName}<{item.CanonicalType}>",
                typeArguments: [item.Identity]);
        }

        if (isGlobalBuiltIn && name is
            "Iterable" or
            "IterableIterator" or
            "ArrayIterator" or
            "MapIterator" or
            "SetIterator")
        {
            EnsureSupportedStandardContainerTransport(
                rf,
                provenance,
                "iterator-transport");
            if (rf.TypeArguments.Count == 1)
            {
                var elem = Project(
                    rf.TypeArguments[0],
                    $"{provenance}/{name}<T>",
                    scope,
                    depth + 1);
                ValidateGenericArgument(name, elem, provenance, 0);
                return ReferenceType(
                    $"IEnumerable<{elem.RenderedType}>",
                    isCollection: true,
                    canonicalType: $"IEnumerable<{elem.CanonicalType}>",
                    typeArguments: [elem.Identity]);
            }
            throw new TypeProjectionException(
                $"Unparameterized '{name}' at '{provenance}' cannot be projected to C#. " +
                "Provide an explicit type argument.", provenance);
        }

        if (isGlobalBuiltIn && name is "AsyncIterable" or "AsyncIterableIterator")
        {
            EnsureSupportedStandardContainerTransport(
                rf,
                provenance,
                "iterator-transport");
            if (rf.TypeArguments.Count == 1)
            {
                var elem = Project(
                    rf.TypeArguments[0],
                    $"{provenance}/AsyncIterable<T>",
                    scope,
                    depth + 1);
                ValidateGenericArgument(name, elem, provenance, 0);
                return ReferenceType(
                    $"IAsyncEnumerable<{elem.RenderedType}>",
                    isCollection: true,
                    canonicalType: $"IAsyncEnumerable<{elem.CanonicalType}>",
                    typeArguments: [elem.Identity]);
            }
            throw new TypeProjectionException(
                $"Unparameterized '{name}' at '{provenance}' cannot be projected to C#. " +
                "Provide an explicit type argument.", provenance);
        }

        if (isGlobalBuiltIn && name == "Record")
        {
            EnsureSupportedStandardContainerTransport(
                rf,
                provenance,
                "standard-container-transport");
            if (rf.TypeArguments.Count != 2)
                throw ArityError(name, 2, rf.TypeArguments.Count, provenance);
            var key = Project(
                rf.TypeArguments[0],
                $"{provenance}/Record<K>",
                scope,
                depth + 1);
            ValidateGenericArgument(name, key, provenance, 0);
            var val = Project(
                rf.TypeArguments[1],
                $"{provenance}/Record<V>",
                scope,
                depth + 1);
            ValidateGenericArgument(name, val, provenance, 1);
            return ReferenceType(
                $"IReadOnlyDictionary<{key.RenderedType},{val.RenderedType}>",
                canonicalType: $"IReadOnlyDictionary<{key.CanonicalType},{val.CanonicalType}>",
                typeArguments: [key.Identity, val.Identity]);
        }

        if (isGlobalBuiltIn && name is "Map" or "ReadonlyMap")
        {
            EnsureSupportedStandardContainerTransport(
                rf,
                provenance,
                "standard-container-transport");
            return ProjectDictionaryContainer(rf, provenance, scope, depth, name);
        }

        if (isGlobalBuiltIn && name is "Set" or "ReadonlySet")
        {
            EnsureSupportedStandardContainerTransport(
                rf,
                provenance,
                "standard-container-transport");
            return ProjectSetContainer(rf, provenance, scope, depth, name);
        }

        if (isGlobalBuiltIn && name is "WeakMap" or "WeakSet")
        {
            throw new GenericDeferralException(
                $"{name} at '{provenance}' has weak-key lifetime semantics with no " +
                "faithful existing CLR projection.",
                provenance,
                "standard-library-weak-collections");
        }

        if (isGlobalBuiltIn && name == "PromiseLike")
        {
            EnsureSupportedStandardContainerTransport(rf, provenance, "promise-transport");
            if (rf.TypeArguments.Count != 1)
                throw ArityError(name, 1, rf.TypeArguments.Count, provenance);
            var inner = Project(
                rf.TypeArguments[0],
                $"{provenance}/PromiseLike<T>",
                scope,
                depth + 1);
            if (inner.Identity.Kind == ClrTypeKind.Null)
                throw IllegalGenericArgument(name, inner, provenance, 0);
            var promiseLikeType = inner.Identity.Kind == ClrTypeKind.Void
                ? "ValueTask"
                : $"ValueTask<{inner.RenderedType}>";
            var promiseLikeCanonical = inner.Identity.Kind == ClrTypeKind.Void
                ? "ValueTask"
                : $"ValueTask<{inner.CanonicalType}>";
            return ValueType(
                promiseLikeType,
                providerNote: "PromiseLike<T>→ValueTask<T>",
                canonicalType: promiseLikeCanonical,
                isAwaitable: true,
                typeArguments: inner.Identity.Kind == ClrTypeKind.Void
                    ? []
                    : [inner.Identity]);
        }

        if (isGlobalBuiltIn && name == "Readonly")
        {
            EnsureSupportedStandardContainerTransport(
                rf,
                provenance,
                "standard-container-transport");
            if (rf.TypeArguments.Count != 1)
                throw ArityError(name, 1, rf.TypeArguments.Count, provenance);
            var target = Project(
                rf.TypeArguments[0],
                $"{provenance}/Readonly<T>",
                scope,
                depth + 1);
            ValidateGenericArgument(name, target, provenance, 0);
            if (!IsProvablyImmutable(target))
            {
                throw new GenericDeferralException(
                    $"Readonly<T> at '{provenance}' targets mutable or unproven CLR " +
                    $"type '{target.RenderedType}' and cannot be weakened to that type.",
                    provenance,
                    "readonly-mapped-types");
            }
            return target with
            {
                ProviderNote = "Readonly<T> preserves a proven immutable CLR type",
            };
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
                if (resolvedName.EndsWith(
                        $".{name}",
                        StringComparison.Ordinal)
                    && name.Length > 0
                    && char.IsUpper(name[0])
                    && scope?.ContainsSourceName(name) == true)
                {
                    throw new TypeProjectionException(
                        $"Type-parameter reference '{resolvedName}' at '{provenance}' " +
                        "is outside the active lexical generic scope.",
                        provenance);
                }
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
            var typeParameters = GetSymbolTypeParameters(
                sym,
                $"{sym.Name}/typeParameters");
            var arguments = ProjectTypeArguments(
                rf.TypeArguments,
                typeParameters,
                sym.Name,
                provenance,
                scope,
                depth);
            for (var index = 0; index < arguments.Count; index++)
                ValidateGenericArgument(sym.Name, arguments[index], provenance, index);
            if (arguments.Count > 0)
                csharpName += $"<{string.Join(", ", arguments.Select(
                    argument => argument.RenderedType))}>";
            var canonicalName = arguments.Count == 0
                ? csharpName
                : $"{Naming.ToCSharpTypeReference(
                    _generatedNamespace,
                    sym.Name,
                    classification is "interface" or "mixin")}<" +
                  $"{string.Join(",", arguments.Select(
                      argument => argument.CanonicalType))}>";
            return classification is "enum" or "typedef"
                ? ValueType(
                    csharpName,
                    canonicalType: canonicalName,
                    typeArguments: arguments.Select(argument => argument.Identity).ToList())
                : ReferenceType(
                    csharpName,
                    canonicalType: canonicalName,
                    typeArguments: arguments.Select(argument => argument.Identity).ToList());
        }

        if (!string.IsNullOrWhiteSpace(rf.ResolvedSymbol)
            && rf.ResolvedSymbol.Contains('.', StringComparison.Ordinal)
            && char.IsUpper(rf.Name.FirstOrDefault()))
        {
            throw new TypeProjectionException(
                $"Type-parameter reference '{rf.ResolvedSymbol}' at '{provenance}' " +
                "is outside the active lexical generic scope.",
                provenance);
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

    private TypeProjection ProjectUnion(
        UnionTypeNode un,
        string provenance,
        GenericScope? scope,
        int depth)
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
            var inner = Project(
                nonNull[0],
                $"{provenance}/nullable",
                scope,
                depth + 1);
            return inner with { IsNullable = true };
        }

        // Pattern 2: All string literals -> enum (handled at symbol level, return string here)
        if (types.All(t => t is LiteralTypeNode lt && lt.LiteralKind == "StringLiteral"))
            return ReferenceType("string", providerNote: "string-literal-union");

        // Pattern 3: T | null | undefined where T is nullable-safe -> T?
        if (nonNull.Count == 1)
        {
            var inner = Project(
                nonNull[0],
                $"{provenance}/nullable",
                scope,
                depth + 1);
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

    private TypeProjection ProjectArray(
        ArrayTypeNode arr,
        string provenance,
        GenericScope? scope,
        int depth)
    {
        if (arr.Transport?.Kind == "unsupported")
        {
            throw new GenericDeferralException(
                $"Array at '{provenance}' has authoritative unsupported transport " +
                $"metadata: {arr.Transport.Reason ?? "no reviewed transport"}",
                $"{provenance}/transport",
                "standard-container-transport");
        }
        var elem = Project(
            arr.ElementType,
            $"{provenance}[]",
            scope,
            depth + 1);
        ValidateGenericArgument("array", elem, provenance, 0);
        return ReferenceType(
            $"{elem.RenderedType}[]",
            isCollection: true,
            canonicalType: $"{elem.CanonicalType}[]",
            typeArguments: [elem.Identity]);
    }

    private TypeProjection ProjectIntersection(
        IntersectionTypeNode intersection,
        string provenance,
        GenericScope? scope,
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
                    scope,
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

    private TypeProjection ProjectFunction(
        FunctionTypeNode fn,
        string provenance,
        GenericScope? scope,
        int depth)
    {
        if (fn.TypeParameters.Count > 0)
        {
            throw new GenericDeferralException(
                $"Generic function type at '{provenance}' requires a named delegate " +
                "because System.Func/System.Action cannot preserve generic Invoke arity.",
                provenance,
                "generic-callback-signature");
        }
        // Function types project to Action<>/Func<> delegates.
        // Skip TypeScript's synthetic `this` parameter — it has no C# equivalent.
        var ret = Project(
            fn.ReturnType,
            $"{provenance}/return",
            scope,
            depth + 1);
        var paramTypes = fn.Parameters
            .Where(p => p.Name != "this")
            .Select((p, i) =>
                Project(
                    p.Type,
                    $"{provenance}/param[{i}]",
                    scope,
                    depth + 1))
            .ToList();
        for (var index = 0; index < paramTypes.Count; index++)
            ValidateGenericArgument("delegate", paramTypes[index], provenance, index);

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
            ValidateGenericArgument("delegate", ret, $"{provenance}/return", paramTypes.Count);
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
        HeritageReferenceTypeNode hr,
        string provenance,
        GenericScope? scope,
        int depth)
    {
        var name = hr.ResolvedSymbol ?? hr.Expression;
        return ProjectReference(
            new ReferenceTypeNode(
                hr.Expression,
                name,
                hr.TypeArguments)
            {
                CheckerType = hr.CheckerType,
                Transport = hr.Transport,
            },
            provenance,
            scope,
            depth);
    }

    private (string Rendered, string Canonical)? ProjectConstraint(
        GenericParameterBinding binding,
        GenericScope scope,
        string provenance)
    {
        if (binding.Model.Constraint is null)
            return null;
        var constraintProvenance =
            $"{provenance}/typeParameter[{binding.Model.Ordinal}]/constraint";
        if (binding.Model.Constraint is OperatorTypeNode
            or IndexedAccessTypeNode
            or IntersectionTypeNode
            or UnionTypeNode
            or TypeLiteralTypeNode
            or TemplateLiteralTypeNode
            or QueryTypeNode)
        {
            throw new GenericDeferralException(
                $"Generic constraint for '{binding.SourceName}' at '{provenance}' " +
                $"uses unsupported TypeScript shape " +
                $"'{binding.Model.Constraint.Kind}' and cannot be weakened.",
                constraintProvenance,
                "advanced-generic-constraints");
        }

        var constraintNode = binding.Model.Constraint is ParenthesizedTypeNode parenthesized
            ? parenthesized.InnerType
            : binding.Model.Constraint;
        if (constraintNode is KeywordTypeNode)
        {
            throw new GenericDeferralException(
                $"Generic constraint for '{binding.SourceName}' at '{provenance}' " +
                "uses a TypeScript primitive/keyword constraint that has no faithful " +
                "C# where-clause equivalent.",
                constraintProvenance,
                "advanced-generic-constraints");
        }
        if (constraintNode is not ReferenceTypeNode reference)
        {
            throw new GenericDeferralException(
                $"Generic constraint for '{binding.SourceName}' at '{provenance}' " +
                $"uses non-nominal TypeScript shape '{constraintNode.Kind}'.",
                constraintProvenance,
                "advanced-generic-constraints");
        }
        if (scope.TryResolve(
                reference.Name,
                reference.ResolvedSymbol,
                out _) == false)
        {
            var target = reference.ResolvedSymbol ?? reference.Name;
            if (!IsInterfaceOrMixin(target))
            {
                throw new GenericDeferralException(
                    $"Generic constraint for '{binding.SourceName}' at '{provenance}' " +
                    $"targets '{target}', which is not a generated reference/base " +
                    "contract and cannot be used as a faithful C# constraint.",
                    constraintProvenance,
                    "advanced-generic-constraints");
            }
        }

        TypeProjection projection;
        try
        {
            projection = Project(
                binding.Model.Constraint,
                constraintProvenance,
                scope);
        }
        catch (GenericDeferralException)
        {
            throw;
        }
        catch (TypeProjectionException exception)
        {
            throw new GenericDeferralException(
                $"Generic constraint for '{binding.SourceName}' at '{provenance}' " +
                $"cannot be represented faithfully in C#: {exception.Message}",
                exception.Provenance,
                "advanced-generic-constraints");
        }

        if (projection.Identity.IsTypeParameter)
            return (projection.RenderedType, projection.CanonicalType);
        if (projection.Identity.Kind != ClrTypeKind.Reference)
        {
            throw new GenericDeferralException(
                $"Generic constraint for '{binding.SourceName}' at '{provenance}' " +
                $"projects to non-reference type '{projection.RenderedType}', which " +
                "is not a faithful C# base/interface constraint.",
                constraintProvenance,
                "advanced-generic-constraints");
        }
        return (projection.RenderedType, projection.CanonicalType);
    }

    private IReadOnlyList<TypeProjection> ProjectTypeArguments(
        IReadOnlyList<TypeNode> supplied,
        IReadOnlyList<TypeParameterModel> parameters,
        string targetIdentity,
        string provenance,
        GenericScope? callerScope,
        int depth)
    {
        var required = parameters.Count(parameter => parameter.Default is null);
        if (supplied.Count < required || supplied.Count > parameters.Count)
        {
            throw new TypeProjectionException(
                $"Generic reference at '{provenance}' supplies {supplied.Count} type " +
                $"argument(s), but target arity is {parameters.Count} with {required} " +
                "required argument(s).",
                provenance);
        }
        if (parameters.Count == 0)
        {
            if (supplied.Count != 0)
                throw ArityError("target", 0, supplied.Count, provenance);
            return [];
        }

        var projected = supplied.Select((argument, index) => Project(
            argument,
            $"{provenance}/typeArgument[{index}]",
            callerScope,
            depth + 1)).ToList();
        var targetScope = GenericScope.Create(
            parameters,
            targetIdentity,
            callerScope,
            canonicalPrefix: "^");
        while (projected.Count < parameters.Count)
        {
            var parameter = parameters[projected.Count];
            if (parameter.Default is null)
            {
                throw new TypeProjectionException(
                    $"Missing required type argument {projected.Count} at '{provenance}'.",
                    provenance);
            }
            var substitutions = projected
                .Concat(Enumerable.Repeat<TypeProjection>(
                    TypeParameter(targetScope.Parameters[projected.Count]),
                    parameters.Count - projected.Count))
                .Take(parameters.Count)
                .ToList();
            var defaultScope = targetScope.WithSubstitutions(substitutions);
            try
            {
                var projectedDefault = Project(
                    parameter.Default,
                    $"{provenance}/defaultTypeArgument[{projected.Count}]",
                    defaultScope,
                    depth + 1);
                if (ContainsUnsubstitutedTargetParameter(projectedDefault.Identity))
                {
                    throw new GenericDeferralException(
                        $"Omitted default type argument '{parameter.Name}' at " +
                        $"'{provenance}' depends on an unresolved or cyclic target " +
                        "type parameter.",
                        $"{provenance}/defaultTypeArgument[{projected.Count}]",
                        "generic-defaults");
                }
                projected.Add(projectedDefault);
            }
            catch (TypeProjectionException exception)
            {
                throw new GenericDeferralException(
                    $"Omitted default type argument '{parameter.Name}' at " +
                    $"'{provenance}' cannot be represented faithfully: " +
                    exception.Message,
                    exception.Provenance,
                    "generic-defaults");
            }
        }
        return projected;
    }

    private static bool ContainsUnsubstitutedTargetParameter(ClrTypeIdentity identity)
        => identity.IsTypeParameter
            ? identity.CanonicalName.StartsWith('^')
            : identity.TypeArguments?.Any(ContainsUnsubstitutedTargetParameter) == true;

    private static IReadOnlyList<TypeParameterModel> GetSymbolTypeParameters(
        SymbolModel symbol,
        string provenance)
    {
        var declarations = symbol.Declarations
            .Where(declaration => declaration.Kind is "interface" or "typeAlias")
            .ToList();
        var parameterLists = declarations
            .Where(declaration => declaration.TypeParameters.Count > 0)
            .Select(declaration => declaration.TypeParameters
                .OrderBy(parameter => parameter.Ordinal)
                .ToList())
            .ToList();
        if (parameterLists.Count == 0)
            return [];

        var canonical = parameterLists[0];
        foreach (var list in parameterLists.Skip(1))
        {
            if (list.Count != canonical.Count
                || list.Where((parameter, index) =>
                        parameter.Name != canonical[index].Name
                        || TypeFingerprint(parameter.Constraint)
                            != TypeFingerprint(canonical[index].Constraint)
                        || TypeFingerprint(parameter.Default)
                            != TypeFingerprint(canonical[index].Default))
                    .Any())
            {
                throw new TypeProjectionException(
                    $"Merged declarations for '{symbol.Name}' have incompatible " +
                    "generic parameter order or arity.",
                    provenance);
            }
        }
        if (declarations.Any(declaration =>
                declaration.TypeParameters.Count == 0))
        {
            throw new TypeProjectionException(
                $"Merged declarations for '{symbol.Name}' mix generic and " +
                "non-generic declaration shapes.",
                provenance);
        }
        return canonical;
    }

    private static string TypeFingerprint(TypeNode? type)
        => type switch
        {
            null => "-",
            KeywordTypeNode keyword =>
                $"keyword:{keyword.Name}:{keyword.CheckerType}",
            ReferenceTypeNode reference =>
                $"reference:{reference.ResolvedSymbol ?? reference.Name}<" +
                $"{string.Join(",", reference.TypeArguments.Select(TypeFingerprint))}>",
            HeritageReferenceTypeNode heritage =>
                $"heritage:{heritage.ResolvedSymbol ?? heritage.Expression}<" +
                $"{string.Join(",", heritage.TypeArguments.Select(TypeFingerprint))}>",
            UnionTypeNode union =>
                $"union({string.Join("|", union.Types.Select(TypeFingerprint))})",
            IntersectionTypeNode intersection =>
                $"intersection({string.Join("&", intersection.Types.Select(TypeFingerprint))})",
            ArrayTypeNode array => $"array({TypeFingerprint(array.ElementType)})",
            TupleTypeNode tuple =>
                $"tuple({string.Join(",", tuple.Elements.Select(TypeFingerprint))})",
            LiteralTypeNode literal =>
                $"literal:{literal.LiteralKind}:{literal.Text}",
            ParenthesizedTypeNode parenthesized =>
                $"parenthesized({TypeFingerprint(parenthesized.InnerType)})",
            OperatorTypeNode operation =>
                $"operator:{operation.Operator}({TypeFingerprint(operation.OperandType)})",
            IndexedAccessTypeNode indexed =>
                $"indexed({TypeFingerprint(indexed.ObjectType)}," +
                $"{TypeFingerprint(indexed.IndexType)})",
            _ => $"{type.Kind}:{type.CheckerType}",
        };

    private TypeProjection ProjectDictionaryContainer(
        ReferenceTypeNode reference,
        string provenance,
        GenericScope? scope,
        int depth,
        string name)
    {
        if (reference.TypeArguments.Count != 2)
            throw ArityError(name, 2, reference.TypeArguments.Count, provenance);
        var key = Project(
            reference.TypeArguments[0],
            $"{provenance}/{name}<K>",
            scope,
            depth + 1);
        var value = Project(
            reference.TypeArguments[1],
            $"{provenance}/{name}<V>",
            scope,
            depth + 1);
        ValidateGenericArgument(name, key, provenance, 0);
        ValidateGenericArgument(name, value, provenance, 1);
        return ReferenceType(
            $"IReadOnlyDictionary<{key.RenderedType}, {value.RenderedType}>",
            isCollection: true,
            canonicalType:
                $"IReadOnlyDictionary<{key.CanonicalType},{value.CanonicalType}>",
            typeArguments: [key.Identity, value.Identity]);
    }

    private TypeProjection ProjectSetContainer(
        ReferenceTypeNode reference,
        string provenance,
        GenericScope? scope,
        int depth,
        string name)
    {
        if (reference.TypeArguments.Count != 1)
            throw ArityError(name, 1, reference.TypeArguments.Count, provenance);
        var item = Project(
            reference.TypeArguments[0],
            $"{provenance}/{name}<T>",
            scope,
            depth + 1);
        ValidateGenericArgument(name, item, provenance, 0);
        return ReferenceType(
            $"IReadOnlySet<{item.RenderedType}>",
            isCollection: true,
            canonicalType: $"IReadOnlySet<{item.CanonicalType}>",
            typeArguments: [item.Identity]);
    }

    private static TypeProjectionException ArityError(
        string name,
        int expected,
        int actual,
        string provenance)
        => new(
            $"Generic type '{name}' at '{provenance}' requires exactly {expected} " +
            $"type argument(s), but received {actual}.",
            provenance);

    private static void ValidateGenericArgument(
        string owner,
        TypeProjection argument,
        string provenance,
        int index)
    {
        if (argument.Identity.Kind is ClrTypeKind.Null or ClrTypeKind.Void)
            throw IllegalGenericArgument(owner, argument, provenance, index);
    }

    private static GenericDeferralException IllegalGenericArgument(
        string owner,
        TypeProjection argument,
        string provenance,
        int index)
        => new(
            $"'{owner}' at '{provenance}' projects type argument {index} to illegal " +
            $"CLR generic argument '{argument.RenderedType}'.",
            $"{provenance}/typeArgument[{index}]",
            "illegal-clr-generic-arguments");

    private static bool IsGlobalBuiltInReference(ReferenceTypeNode reference)
    {
        if (string.IsNullOrWhiteSpace(reference.ResolvedSymbol)
            || string.Equals(
                reference.ResolvedSymbol,
                reference.Name,
                StringComparison.Ordinal))
        {
            return true;
        }
        return reference.ResolvedSymbol.StartsWith(
                $"{reference.Name}<",
                StringComparison.Ordinal)
            && reference.ResolvedSymbol.EndsWith('>');
    }

    private static void EnsureSupportedStandardContainerTransport(
        ReferenceTypeNode reference,
        string provenance,
        string phase)
    {
        if (reference.Transport?.Kind != "unsupported")
            return;
        throw new GenericDeferralException(
            $"Standard generic '{reference.Name}' at '{provenance}' has authoritative " +
            $"unsupported transport metadata: {reference.Transport.Reason ?? "no reviewed transport"}",
            $"{provenance}/transport",
            phase);
    }

    private static bool IsProvablyImmutable(TypeProjection projection)
        => projection.Identity.Kind == ClrTypeKind.Value
            || string.Equals(
                projection.Identity.CanonicalName,
                "string",
                StringComparison.Ordinal);

    private static void ValidateOptionalBufferArgument(
        ReferenceTypeNode reference,
        string provenance)
    {
        if (reference.TypeArguments.Count > 1)
            throw ArityError(
                reference.Name,
                1,
                reference.TypeArguments.Count,
                provenance);
        if (reference.TypeArguments.Count == 1
            && reference.TypeArguments[0] is not ReferenceTypeNode
            {
                Name: "ArrayBuffer" or "ArrayBufferLike" or "SharedArrayBuffer",
            })
        {
            throw new TypeProjectionException(
                $"Typed array '{reference.Name}' at '{provenance}' has unsupported " +
                "backing-buffer type argument.",
                provenance);
        }
    }

    private static bool IsDefaultIteratorReturn(TypeNode type)
        => type is ReferenceTypeNode
            {
                Name: "BuiltinIteratorReturn",
            }
            || IsUnknownLike(type);

    private static bool IsUnknownLike(TypeNode type)
        => type is KeywordTypeNode
            {
                Name: "UnknownKeyword" or "AnyKeyword" or "unknown" or "any",
            };

    private static void ValidateJsonGenericTransport(
        TypeNode typeNode,
        string provenance)
    {
        if (typeNode.Transport?.Kind != "json-value"
            || typeNode is not ReferenceTypeNode reference
            || reference.TypeArguments.Count == 0)
        {
            return;
        }

        var invalid = reference.TypeArguments
            .Select((argument, index) => (argument, index))
            .FirstOrDefault(item =>
                item.argument.Transport?.Kind != "json-value");
        if (invalid.argument is not null)
        {
            throw new TypeProjectionException(
                $"Generic JSON projection '{reference.Name}' at '{provenance}' " +
                $"cannot prove JSON compatibility recursively: type argument " +
                $"{invalid.index} has transport " +
                $"'{invalid.argument.Transport?.Kind ?? "(missing)"}'.",
                $"{provenance}/typeArgument[{invalid.index}]/transport");
        }
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
        bool isAwaitable = false,
        IReadOnlyList<ClrTypeIdentity>? typeArguments = null)
        => new(
            csharpType,
            false,
            isCollection,
            new ClrTypeIdentity(
                canonicalType ?? csharpType,
                ClrTypeKind.Value,
                isAwaitable,
                typeArguments?.Count ?? 0,
                typeArguments),
            providerNote);

    private static TypeProjection ReferenceType(
        string csharpType,
        bool isCollection = false,
        string providerNote = "",
        string? canonicalType = null,
        IReadOnlyList<ClrTypeIdentity>? typeArguments = null)
        => new(
            csharpType,
            false,
            isCollection,
            new ClrTypeIdentity(
                canonicalType ?? csharpType,
                ClrTypeKind.Reference,
                GenericArity: typeArguments?.Count ?? 0,
                TypeArguments: typeArguments),
            providerNote);

    private static TypeProjection TypeParameter(GenericParameterBinding parameter)
        => new(
            parameter.CSharpName,
            false,
            false,
            new ClrTypeIdentity(
                parameter.CanonicalIdentity,
                ClrTypeKind.Reference,
                IsTypeParameter: true),
            $"type-parameter:{parameter.SourceName}");

    private static TypeProjection VoidType()
        => new("void", false, false, new ClrTypeIdentity("void", ClrTypeKind.Void));

    private static TypeProjection NullType()
        => new("null", true, false, new ClrTypeIdentity("null", ClrTypeKind.Null));
}
