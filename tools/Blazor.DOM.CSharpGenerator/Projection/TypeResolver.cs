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
    private readonly SynthesizedTypeRegistry _synthesizedTypes;

    public IReadOnlyList<SynthesizedTypeDefinition> SynthesizedTypes
        => _synthesizedTypes.Definitions;

    public TypeResolver(
        IReadOnlyList<SymbolModel> symbols,
        IReadOnlyDictionary<string, EmitterOverrideEntry>? overrides = null,
        string generatedNamespace = "Blazor.DOM")
    {
        _symbolIndex = symbols.ToDictionary(s => s.Name, StringComparer.Ordinal);
        _overrides = overrides
            ?? new Dictionary<string, EmitterOverrideEntry>(StringComparer.Ordinal);
        _generatedNamespace = generatedNamespace;
        _synthesizedTypes = new SynthesizedTypeRegistry(generatedNamespace);
    }

    /// <summary>Returns true if the named symbol is in the TypeScript IR symbol index.</summary>
    public bool IsKnownSymbol(string name) => _symbolIndex.ContainsKey(name);

    /// <summary>Gets a symbol from the TypeScript IR index without rendering its CLR name.</summary>
    public bool TryGetSymbol(string name, out SymbolModel symbol)
        => _symbolIndex.TryGetValue(name, out symbol!);

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

    public bool IsStandardStructuralHeritage(HeritageReferenceTypeNode reference)
        => reference.TypeArguments.Count == 0
            && string.Equals(reference.Expression, "Error", StringComparison.Ordinal)
            && string.Equals(
                reference.ResolvedSymbol ?? reference.Expression,
                "Error",
                StringComparison.Ordinal)
            && !_symbolIndex.ContainsKey("Error");

    public bool RequiresDictionaryContract(string symbolName)
        => RequiresDictionaryContract(
            symbolName,
            new HashSet<string>(StringComparer.Ordinal));

    private bool RequiresDictionaryContract(
        string symbolName,
        ISet<string> visiting)
    {
        if (!visiting.Add(symbolName))
            return false;
        try
        {
            foreach (var symbol in _symbolIndex.Values)
            {
                var classification =
                    EffectiveClassificationPolicy.Classify(symbol, _overrides).Name;
                var extendsTarget = symbol.Declarations
                    .SelectMany(declaration => declaration.Heritage)
                    .Where(heritage => heritage.Token == "extends")
                    .SelectMany(heritage => heritage.Types)
                    .OfType<HeritageReferenceTypeNode>()
                    .Any(reference => string.Equals(
                        reference.ResolvedSymbol ?? reference.Expression,
                        symbolName,
                        StringComparison.Ordinal));
                if (!extendsTarget)
                    continue;
                if (classification is "interface" or "mixin"
                    || classification == "dictionary"
                    && RequiresDictionaryContract(symbol.Name, visiting))
                {
                    return true;
                }
            }
            return false;
        }
        finally
        {
            visiting.Remove(symbolName);
        }
    }

    public TypeProjection ProjectDictionaryContract(
        HeritageReferenceTypeNode reference,
        string provenance,
        GenericScope? scope)
    {
        var symbolName = reference.ResolvedSymbol ?? reference.Expression;
        if (!_symbolIndex.TryGetValue(symbolName, out var symbol)
            || !IsDictionarySymbol(symbolName))
        {
            throw new TypeProjectionException(
                $"Structural heritage '{symbolName}' at '{provenance}' is not a " +
                "known TypeScript/Web IDL dictionary.",
                provenance);
        }

        var parameters = GetSymbolTypeParameters(
            symbol,
            $"{symbolName}/typeParameters");
        var arguments = ProjectTypeArguments(
            reference.TypeArguments,
            parameters,
            symbolName,
            provenance,
            scope,
            0);
        for (var index = 0; index < arguments.Count; index++)
            ValidateGenericArgument(symbolName, arguments[index], provenance, index);

        var simpleName = $"I{Naming.ToCSharpSimpleTypeName(symbolName)}Contract";
        var contractName = symbolName.Contains('.', StringComparison.Ordinal)
            ? $"global::{Naming.ToGeneratedNamespace(_generatedNamespace, symbolName)}.{simpleName}"
            : simpleName;
        if (arguments.Count > 0)
        {
            contractName += $"<{string.Join(", ", arguments.Select(
                argument => argument.RenderedType))}>";
        }
        var canonical = arguments.Count == 0
            ? contractName
            : $"{contractName[..contractName.IndexOf('<')]}<" +
              $"{string.Join(",", arguments.Select(argument => argument.CanonicalType))}>";
        return ReferenceType(
            contractName,
            providerNote: "structural dictionary heritage contract",
            canonicalType: canonical,
            typeArguments: arguments.Select(argument => argument.Identity).ToList());
    }

    public TypeProjection ProjectLiteralStringParameter(
        LiteralTypeNode literal,
        string provenance)
    {
        if (literal.LiteralKind != "StringLiteral")
        {
            throw new TypeProjectionException(
                $"Literal specialization at '{provenance}' requires a string literal.",
                provenance);
        }
        var value = Unquote(literal.Text);
        var domain = _synthesizedTypes.RegisterStringDomain(
            $"{provenance}/literal-domain",
            [value]);
        return ValueType(
            domain,
            providerNote: $"literal-string-domain:{value}",
            canonicalType: domain,
            transport: literal.Transport ?? new TransportModel(
                "json-value",
                false,
                literal.CheckerType ?? literal.Text,
                false,
                true,
                null));
    }

    public TypeProjection ProjectOverloadReturnUnion(
        IReadOnlyList<TypeNode?> returnTypes,
        string provenance,
        GenericScope? scope)
    {
        if (returnTypes.Any(type => type is null))
        {
            throw new TypeProjectionException(
                $"Return-only overload set at '{provenance}' includes an implicit " +
                "void return and cannot form a value union.",
                provenance);
        }
        return Project(
            new UnionTypeNode(returnTypes.Cast<TypeNode>().ToList()),
            $"{provenance}/return-union",
            scope);
    }

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
            TypeLiteralTypeNode literal => ProjectTypeLiteral(
                literal,
                provenance,
                scope,
                depth),
            TemplateLiteralTypeNode template => ProjectTemplateLiteral(
                template,
                provenance),
            QueryTypeNode query => ProjectQuery(
                query,
                provenance,
                scope,
                depth),
            IndexedAccessTypeNode indexed => ProjectIndexedAccess(
                indexed,
                provenance,
                scope,
                depth),
            OperatorTypeNode operation => ProjectOperator(
                operation,
                provenance,
                scope,
                depth),
            TupleTypeNode tuple => ProjectTuple(
                tuple,
                provenance,
                scope,
                depth),
            NamedTupleMemberTypeNode named => Project(
                named.ElementType,
                $"{provenance}/namedTupleMember",
                scope,
                depth + 1),
            OptionalTypeNode optional => Project(
                optional.InnerType,
                $"{provenance}/optional",
                scope,
                depth + 1) with { IsNullable = true },
            RestTypeNode rest => Project(
                rest.InnerType,
                $"{provenance}/rest",
                scope,
                depth + 1),
            ImportTypeNode import => ProjectImport(
                import,
                provenance,
                scope,
                depth),
            UnknownTypeNode u => Fail(typeNode, provenance,
                $"unknown type node kind '{u.RawKind}' cannot be projected"),
            _ => Fail(typeNode, provenance,
                $"unhandled TypeNode subtype '{typeNode.GetType().Name}'"),
        };
        ValidateJsonGenericTransport(typeNode, provenance);
        var reducedTransport = projection.ProviderNote is
            "statically-reduced-indexed-access" or
            "readonly-operator" or
            "readonly-array" or
            "resolved-import-type" or
            "Promise<T>→ValueTask<T>"
            || projection.ProviderNote.StartsWith(
                "type-parameter:",
                StringComparison.Ordinal)
            || projection.ProviderNote.StartsWith(
                "browser-",
                StringComparison.Ordinal)
            || projection.ProviderNote.StartsWith(
                "Window & typeof globalThis",
                StringComparison.Ordinal);
        return projection with
        {
            Transport = typeNode.Transport?.Kind == "runtime-inferred"
                ? typeNode.Transport
                : reducedTransport
                    || projection.ProviderNote == "declaration-shape-dictionary"
                    ? projection.Transport
                    : typeNode.Transport ?? projection.Transport,
        };
    }

    private TypeProjection ProjectKeyword(KeywordTypeNode kw, string provenance)
    {
        var name = kw.Name ?? kw.CheckerType ?? "";
        if (KeywordMap.TryGetValue(name, out var mapped))
        {
            if (mapped == "never")
            {
                var neverType = _synthesizedTypes.RegisterTypeScriptNever();
                return ReferenceType(
                    neverType,
                    providerNote: "TypeScript never (uninhabited)");
            }
            if (mapped == "null")
                return name is "UndefinedKeyword" or "undefined"
                    ? BrowserUndefinedType()
                    : BrowserNullType();
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
            case "Date": return ValueType(
                "DateTimeOffset",
                providerNote: "JavaScript Date");
            case "Function": return ReferenceType(
                "Delegate",
                providerNote: "JavaScript Function");
            case "ArrayBufferLike": return ReferenceType(
                "byte[]",
                true,
                "mapped-from-ArrayBufferLike");
            case "ArrayBufferView": return ReferenceType(
                "byte[]",
                true,
                "mapped-from-ArrayBufferView");
        }

        // TypeScript 5.9.3 lib.es5.d.ts defines Error structurally as
        // { name: string; message: string; stack?: string }. It is a live
        // JavaScript object, not a JSON/object fallback. A qualified user type
        // named Error is deliberately excluded by IsGlobalBuiltInReference.
        if (isGlobalBuiltIn
            && name == "Error"
            && !_symbolIndex.ContainsKey(rf.ResolvedSymbol ?? rf.Name))
        {
            if (rf.TypeArguments.Count != 0)
                throw ArityError(name, 0, rf.TypeArguments.Count, provenance);
            var errorType = _synthesizedTypes.RegisterTypeScriptError();
            return ReferenceType(
                errorType,
                providerNote: "TypeScript lib.es5 Error",
                transport: new TransportModel(
                    "js-reference",
                    false,
                    "Error",
                    false,
                    false,
                    "Standard Error is transported as its live JavaScript object identity."));
        }

        // TypeScript 5.9.3 lib.es5.d.ts:
        // type Exclude<T, U> = T extends U ? never : T.
        // Evaluate the utility only for finite string domains, preserving the
        // exact set difference rather than widening to string.
        if (isGlobalBuiltIn
            && name == "Exclude"
            && !_symbolIndex.ContainsKey(rf.ResolvedSymbol ?? rf.Name))
        {
            if (rf.TypeArguments.Count != 2)
                throw ArityError(name, 2, rf.TypeArguments.Count, provenance);
            if (!TryResolveFiniteStringDomain(
                    rf.TypeArguments[0],
                    $"{provenance}/Exclude<T>",
                    out var candidates)
                || !TryResolveFiniteStringDomain(
                    rf.TypeArguments[1],
                    $"{provenance}/Exclude<U>",
                    out var exclusions))
            {
                throw new GenericDeferralException(
                    $"Standard Exclude<T, U> at '{provenance}' requires finite " +
                    "string domains for an exact CLR projection.",
                    provenance,
                    "standard-utility-types");
            }
            var excluded = exclusions.ToHashSet(StringComparer.Ordinal);
            var remaining = candidates
                .Where(value => !excluded.Contains(value))
                .Order(StringComparer.Ordinal)
                .ToList();
            if (remaining.Count == 0)
            {
                throw new GenericDeferralException(
                    $"Standard Exclude<T, U> at '{provenance}' evaluates to never.",
                    provenance,
                    "standard-utility-types");
            }
            var domain = _synthesizedTypes.RegisterStringDomain(
                $"{provenance}/Exclude",
                remaining);
            return ValueType(
                domain,
                providerNote: "TypeScript lib.es5 Exclude finite string domain",
                canonicalType: domain,
                transport: new TransportModel(
                    "json-value",
                    false,
                    rf.CheckerType ?? "Exclude",
                    false,
                    true,
                    null));
        }

        // Method-returned promises are awaited by generated proxy methods. Promise
        // values elsewhere retain their live JavaScript identity.
        if (isGlobalBuiltIn && name == "Promise")
        {
            if (rf.TypeArguments.Count != 1)
                throw ArityError(name, 1, rf.TypeArguments.Count, provenance);
            var inner = Project(
                rf.TypeArguments[0],
                $"{provenance}/Promise<T>",
                scope,
                depth + 1);
            if (inner.Identity.Kind == ClrTypeKind.Null)
                throw IllegalGenericArgument(name, inner, provenance, 0);
            if (!IsCallableReturn(provenance))
            {
                var browserPromise = inner.Identity.Kind == ClrTypeKind.Void
                    ? "global::Microsoft.JSInterop.IBrowserPromise"
                    : $"global::Microsoft.JSInterop.IBrowserPromise<{inner.RenderedType}>";
                var canonicalPromise = inner.Identity.Kind == ClrTypeKind.Void
                    ? "global::Microsoft.JSInterop.IBrowserPromise"
                    : $"global::Microsoft.JSInterop.IBrowserPromise<{inner.CanonicalType}>";
                return ReferenceType(
                    browserPromise,
                    providerNote: "browser-promise-reference",
                    canonicalType: canonicalPromise,
                    typeArguments: inner.Identity.Kind == ClrTypeKind.Void
                        ? []
                        : [inner.Identity],
                    transport: BrowserReferenceTransport(rf));
            }
            var promiseType = inner.Identity.Kind == ClrTypeKind.Void
                ? "ValueTask"
                : $"ValueTask<{inner.RenderedType}>";
            var canonicalType = inner.Identity.Kind == ClrTypeKind.Void
                ? "ValueTask"
                : $"ValueTask<{inner.CanonicalType}>";
            return ValueType(
                promiseType,
                providerNote: "browser-awaited-promise",
                canonicalType: canonicalType,
                isAwaitable: true,
                typeArguments: inner.Identity.Kind == ClrTypeKind.Void
                    ? []
                    : [inner.Identity],
                transport: AwaitedTransport(rf, inner));
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
            if (rf.TypeArguments.Count == 1)
            {
                var elem = Project(
                    rf.TypeArguments[0],
                    $"{provenance}/Array<T>",
                    scope,
                    depth + 1);
                ValidateGenericArgument(name, elem, provenance, 0);
                if (RequiresLiveContainer(rf))
                {
                    var contract = name == "ReadonlyArray"
                        ? "IReadOnlyBrowserArray"
                        : "IBrowserArray";
                    return ReferenceType(
                        $"global::Microsoft.JSInterop.{contract}<{elem.RenderedType}>",
                        isCollection: true,
                        providerNote: "browser-array-reference",
                        canonicalType:
                            $"global::Microsoft.JSInterop.{contract}<{elem.CanonicalType}>",
                        typeArguments: [elem.Identity],
                        transport: BrowserReferenceTransport(rf));
                }
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
            if (rf.TypeArguments.Count is not (1 or 3))
            {
                throw new TypeProjectionException(
                    $"{name} at '{provenance}' requires one element argument or " +
                    "the complete three-argument iterator form.",
                    provenance);
            }
            var item = Project(
                rf.TypeArguments[0],
                $"{provenance}/{name}<T>",
                scope,
                depth + 1);
            ValidateGenericArgument(name, item, provenance, 0);
            var returnType = rf.TypeArguments.Count == 3
                ? ProjectIteratorGenericArgument(
                    rf.TypeArguments[1],
                    $"{provenance}/{name}<TReturn>",
                    scope,
                    depth)
                : BrowserUndefinedType();
            var nextType = rf.TypeArguments.Count == 3
                ? ProjectIteratorGenericArgument(
                    rf.TypeArguments[2],
                    $"{provenance}/{name}<TNext>",
                    scope,
                    depth)
                : ReferenceType("object");
            var clrName = name is "AsyncIteratorObject" or "AsyncIterator"
                ? "IBrowserAsyncIterator"
                : "IBrowserIterator";
            return ReferenceType(
                $"global::Microsoft.JSInterop.{clrName}<" +
                $"{item.RenderedType}, {returnType.RenderedType}, {nextType.RenderedType}>",
                isCollection: true,
                providerNote: "browser-iterator-reference",
                canonicalType:
                    $"global::Microsoft.JSInterop.{clrName}<" +
                    $"{item.CanonicalType},{returnType.CanonicalType},{nextType.CanonicalType}>",
                typeArguments: [item.Identity, returnType.Identity, nextType.Identity],
                transport: BrowserReferenceTransport(rf));
        }

        if (isGlobalBuiltIn && name is
            "Iterable" or
            "IterableIterator" or
            "ArrayIterator" or
            "MapIterator" or
            "SetIterator")
        {
            if (rf.TypeArguments.Count == 1)
            {
                var elem = Project(
                    rf.TypeArguments[0],
                    $"{provenance}/{name}<T>",
                    scope,
                    depth + 1);
                ValidateGenericArgument(name, elem, provenance, 0);
                var contract = name == "Iterable"
                    ? "IBrowserIterable"
                    : "IBrowserIterableIterator";
                return ReferenceType(
                    $"global::Microsoft.JSInterop.{contract}<{elem.RenderedType}>",
                    isCollection: true,
                    providerNote: "browser-iterable-reference",
                    canonicalType:
                        $"global::Microsoft.JSInterop.{contract}<{elem.CanonicalType}>",
                    typeArguments: [elem.Identity],
                    transport: BrowserReferenceTransport(rf));
            }
            throw new TypeProjectionException(
                $"Unparameterized '{name}' at '{provenance}' cannot be projected to C#. " +
                "Provide an explicit type argument.", provenance);
        }

        if (isGlobalBuiltIn && name is "AsyncIterable" or "AsyncIterableIterator")
        {
            if (rf.TypeArguments.Count == 1)
            {
                var elem = Project(
                    rf.TypeArguments[0],
                    $"{provenance}/AsyncIterable<T>",
                    scope,
                    depth + 1);
                ValidateGenericArgument(name, elem, provenance, 0);
                var contract = name == "AsyncIterable"
                    ? "IBrowserAsyncIterable"
                    : "IBrowserAsyncIterableIterator";
                return ReferenceType(
                    $"global::Microsoft.JSInterop.{contract}<{elem.RenderedType}>",
                    isCollection: true,
                    providerNote: "browser-async-iterable-reference",
                    canonicalType:
                        $"global::Microsoft.JSInterop.{contract}<{elem.CanonicalType}>",
                    typeArguments: [elem.Identity],
                    transport: BrowserReferenceTransport(rf));
            }
            throw new TypeProjectionException(
                $"Unparameterized '{name}' at '{provenance}' cannot be projected to C#. " +
                "Provide an explicit type argument.", provenance);
        }

        if (isGlobalBuiltIn && name == "Record")
        {
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
            if (RequiresLiveContainer(rf))
            {
                return ReferenceType(
                    $"global::Microsoft.JSInterop.IBrowserRecord<{key.RenderedType}, {val.RenderedType}>",
                    isCollection: true,
                    providerNote: "browser-record-reference",
                    canonicalType:
                        $"global::Microsoft.JSInterop.IBrowserRecord<{key.CanonicalType},{val.CanonicalType}>",
                    typeArguments: [key.Identity, val.Identity],
                    transport: BrowserReferenceTransport(rf));
            }
            return ReferenceType(
                $"IReadOnlyDictionary<{key.RenderedType},{val.RenderedType}>",
                canonicalType: $"IReadOnlyDictionary<{key.CanonicalType},{val.CanonicalType}>",
                typeArguments: [key.Identity, val.Identity]);
        }

        if (isGlobalBuiltIn && name is "Map" or "ReadonlyMap")
        {
            return ProjectDictionaryContainer(rf, provenance, scope, depth, name);
        }

        if (isGlobalBuiltIn && name is "Set" or "ReadonlySet")
        {
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
            if (rf.TypeArguments.Count != 1)
                throw ArityError(name, 1, rf.TypeArguments.Count, provenance);
            var inner = Project(
                rf.TypeArguments[0],
                $"{provenance}/PromiseLike<T>",
                scope,
                depth + 1);
            if (inner.Identity.Kind == ClrTypeKind.Null)
                throw IllegalGenericArgument(name, inner, provenance, 0);
            if (!IsCallableReturn(provenance))
            {
                var browserPromise = inner.Identity.Kind == ClrTypeKind.Void
                    ? "global::Microsoft.JSInterop.IBrowserPromise"
                    : $"global::Microsoft.JSInterop.IBrowserPromiseLike<{inner.RenderedType}>";
                var canonicalPromise = inner.Identity.Kind == ClrTypeKind.Void
                    ? "global::Microsoft.JSInterop.IBrowserPromise"
                    : $"global::Microsoft.JSInterop.IBrowserPromiseLike<{inner.CanonicalType}>";
                return ReferenceType(
                    browserPromise,
                    providerNote: "browser-promise-like-reference",
                    canonicalType: canonicalPromise,
                    typeArguments: inner.Identity.Kind == ClrTypeKind.Void
                        ? []
                        : [inner.Identity],
                    transport: BrowserReferenceTransport(rf));
            }
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
            if (!IsSemanticallyImmutable(
                    rf.TypeArguments[0],
                    scope,
                    new HashSet<string>(StringComparer.Ordinal),
                    new Dictionary<string, TypeNode>(StringComparer.Ordinal)))
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
            var effective = EffectiveClassificationPolicy.Classify(sym, _overrides);
            var classification = effective.Name;
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
                    providerNote: classification == "dictionary"
                        && effective.Source == EffectiveClassificationSource.DeclarationShape
                            ? "declaration-shape-dictionary"
                            : "",
                    canonicalType: canonicalName,
                    typeArguments: arguments.Select(argument => argument.Identity).ToList(),
                    transport: classification == "dictionary"
                        && effective.Source == EffectiveClassificationSource.DeclarationShape
                            ? new TransportModel(
                                "json-value",
                                rf.Transport?.Nullable ?? false,
                                rf.CheckerType ?? sym.Name,
                                false,
                                true,
                                null)
                            : null);
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

    private TypeProjection ProjectLiteral(LiteralTypeNode lit, string provenance)
    {
        return lit.LiteralKind switch
        {
            "StringLiteral" => ValueType(
                _synthesizedTypes.RegisterStringDomain(
                    provenance,
                    [lit.Text.Trim('"')]),
                providerNote: $"literal-string-domain:{lit.Text}"),
            "NumericLiteral" => ValueType("double",
                providerNote: $"literal-number:{lit.Text}"),
            "PrefixUnaryExpression"
                when double.TryParse(
                    lit.Text,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out _)
                => ValueType(
                    "double",
                    providerNote: $"literal-number:{lit.Text}"),
            "BigIntLiteral"
                when lit.Text.EndsWith("n", StringComparison.Ordinal)
                    && long.TryParse(
                        lit.Text[..^1],
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out _)
                => ValueType(
                    "long",
                    providerNote: $"literal-bigint:{lit.Text}"),
            "TrueLiteral" or "FalseLiteral" or "TrueKeyword" or "FalseKeyword"
                => ValueType("bool", providerNote: $"literal-bool:{lit.Text}"),
            // The IR emits null/undefined literals with LiteralKind="NullKeyword"/"UndefinedKeyword"
            "NullKeyword" or "NullLiteral" => BrowserNullType(),
            "UndefinedKeyword" => BrowserUndefinedType(),
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
        var normalized = UnionNormalization.Normalize(un, provenance);
        if ((normalized.HasNull || normalized.HasUndefined)
            && normalized.ValueArms.Count == 1)
        {
            var inner = Project(
                normalized.ValueArms[0].Type,
                $"{provenance}/nullable",
                scope,
                depth + 1);
            return inner with { IsNullable = true };
        }

        if (normalized.Arms.Count == 2
            && normalized.Arms.All(arm => arm.Type is LiteralTypeNode literal
                && literal.LiteralKind is "TrueLiteral" or "FalseLiteral"
                    or "TrueKeyword" or "FalseKeyword")
            && normalized.Arms.Select(arm => ((LiteralTypeNode)arm.Type).LiteralKind
                    is "TrueLiteral" or "TrueKeyword")
                .Distinct()
                .Count() == 2)
        {
            return ValueType("bool", providerNote: "complete-boolean-literal-union");
        }

        if (normalized.Arms.Count > 0
            && normalized.Arms.All(arm => arm.Type is LiteralTypeNode
            {
                LiteralKind: "StringLiteral",
            }))
        {
            var values = normalized.Arms
                .Select(arm => ((LiteralTypeNode)arm.Type).Text.Trim('"'))
                .ToList();
            var enumType = _synthesizedTypes.RegisterStringDomain(provenance, values);
            return ValueType(
                enumType,
                providerNote: "finite-string-literal-union",
                canonicalType: enumType);
        }

        if (normalized.Arms.Count > 0
            && normalized.Arms.All(arm => arm.Type is LiteralTypeNode
            {
                LiteralKind: "NumericLiteral" or "PrefixUnaryExpression",
            })
            && normalized.Arms
                .Select(arm => (LiteralTypeNode)arm.Type)
                .All(literal => double.TryParse(
                    literal.Text,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out _)))
        {
            var values = normalized.Arms
                .Select(arm => double.Parse(
                    ((LiteralTypeNode)arm.Type).Text,
                    System.Globalization.CultureInfo.InvariantCulture))
                .ToList();
            var numericType = _synthesizedTypes.RegisterNumericDomain(
                provenance,
                values);
            return ValueType(
                numericType,
                providerNote: "finite-numeric-literal-union",
                canonicalType: numericType);
        }

        if (normalized.Arms.Count > 0
            && normalized.Arms.All(arm =>
                arm.Type is QueryTypeNode
                && double.TryParse(
                    arm.Type.CheckerType,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out _)))
        {
            var values = normalized.Arms
                .Select(arm => double.Parse(
                    arm.Type.CheckerType!,
                    System.Globalization.CultureInfo.InvariantCulture))
                .ToList();
            var numericType = _synthesizedTypes.RegisterNumericDomain(
                provenance,
                values);
            return ValueType(
                numericType,
                providerNote: "finite-numeric-query-union",
                canonicalType: numericType);
        }

        var arms = Emitters.UnionWrapperEmitter.ProjectArms(
            normalized,
            arm => Project(
                arm.Type,
                arm.Provenances[0],
                scope,
                depth + 1));
        Emitters.UnionWrapperEmitter.ValidateRuntimeArms(provenance, arms);
        var unionType = _synthesizedTypes.RegisterUnion(
            provenance,
            normalized,
            arms,
            scope);
        return ValueType(
            unionType,
            providerNote: "typed-union",
            canonicalType: unionType,
            typeArguments: arms
                .Where(arm => arm.Projection is not null)
                .Select(arm => arm.Projection!.Identity)
                .ToList());
    }

    private TypeProjection ProjectArray(
        ArrayTypeNode arr,
        string provenance,
        GenericScope? scope,
        int depth)
    {
        var elem = Project(
            arr.ElementType,
            $"{provenance}[]",
            scope,
            depth + 1);
        ValidateGenericArgument("array", elem, provenance, 0);
        if (arr.Transport?.Kind is "unsupported" or "js-reference")
        {
            return ReferenceType(
                $"global::Microsoft.JSInterop.IBrowserArray<{elem.RenderedType}>",
                isCollection: true,
                providerNote: "browser-array-reference",
                canonicalType:
                    $"global::Microsoft.JSInterop.IBrowserArray<{elem.CanonicalType}>",
                typeArguments: [elem.Identity],
                transport: BrowserReferenceTransport(arr));
        }
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
                        StringComparison.Ordinal)
                    || string.Equals(
                        reference.ResolvedSymbol ?? reference.Name,
                        "WindowProxy",
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

        if (intersection.Types.Any(ContainsUnion))
        {
            throw new GenericDeferralException(
                $"Intersection at '{provenance}' contains a union arm and cannot be " +
                "composed without the dedicated typed-union phase.",
                provenance,
                "intersection-union-arms");
        }

        if (intersection.Types.All(type =>
                type is ReferenceTypeNode or QueryTypeNode or ParenthesizedTypeNode))
        {
            var sourceShape = intersection.CheckerType
                ?? string.Join(
                    " & ",
                    intersection.Types.Select(TypeFingerprint));
            var composite = _synthesizedTypes.RegisterIntersection(
                $"{provenance}/intersection",
                sourceShape);
            return ReferenceType(
                composite,
                providerNote: "browser-intersection-composite",
                canonicalType: composite,
                transport: BrowserReferenceTransport(intersection));
        }

        if (intersection.Types.All(type => type is TypeLiteralTypeNode)
            && intersection.Transport?.Kind is null or "json-value")
        {
            var merged = new List<MemberModel>();
            foreach (var member in intersection.Types
                .Cast<TypeLiteralTypeNode>()
                .SelectMany(type => type.Members))
            {
                var sourceName = member.Name?.Text;
                var existing = merged.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Name?.Text,
                        sourceName,
                        StringComparison.Ordinal));
                if (existing is null)
                {
                    merged.Add(member);
                    continue;
                }

                if (existing.Type is null
                    || member.Type is null
                    || TypeFingerprint(existing.Type) != TypeFingerprint(member.Type))
                {
                    throw new GenericDeferralException(
                        $"Intersection at '{provenance}' has incompatible duplicate " +
                        $"member '{sourceName ?? "(computed)"}'.",
                        provenance,
                        "intersection-member-collision");
                }
                var index = merged.IndexOf(existing);
                merged[index] = existing with
                {
                    Optional = existing.Optional && member.Optional,
                    Readonly = existing.Readonly || member.Readonly,
                };
            }
            return ProjectTypeLiteral(
                new TypeLiteralTypeNode(merged)
                {
                    CheckerType = intersection.CheckerType,
                    Transport = intersection.Transport,
                },
                $"{provenance}/intersection",
                scope,
                depth + 1);
        }

        if (intersection.Types.Any(type => type is KeywordTypeNode or LiteralTypeNode)
            && intersection.Types.Any(type =>
                type is TypeLiteralTypeNode or ReferenceTypeNode))
        {
            throw new GenericDeferralException(
                $"Intersection at '{provenance}' is a branded primitive and cannot be " +
                "represented as its underlying CLR primitive without losing the brand.",
                provenance,
                "branded-intersection");
        }

        var transports = intersection.Types
            .Select(type => type.Transport?.Kind)
            .Where(kind => kind is not null)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (transports.Count > 1
            || transports.Contains("unsupported", StringComparer.Ordinal))
        {
            throw new GenericDeferralException(
                $"Intersection at '{provenance}' has incompatible or unsupported arm " +
                $"transports [{string.Join(", ", transports)}].",
                provenance,
                "intersection-transport");
        }

        throw new GenericDeferralException(
            $"Intersection at '{provenance}' requires a named collision-free CLR " +
            "composition that has not been proven.",
            provenance,
            "intersection-composition");
    }

    private TypeProjection ProjectTuple(
        TupleTypeNode tuple,
        string provenance,
        GenericScope? scope,
        int depth)
    {
        var liveReference = tuple.Transport?.Kind is "unsupported" or "js-reference";
        if (tuple.Elements.Count == 0)
        {
            throw new GenericDeferralException(
                $"Empty tuple at '{provenance}' requires an explicit unit-array contract.",
                provenance,
                "tuple-json-contract");
        }

        var elements = new List<SynthesizedTupleElement>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        var optionalSeen = false;
        for (var index = 0; index < tuple.Elements.Count; index++)
        {
            var source = tuple.Elements[index];
            var sourceName = $"item{index + 1}";
            var optional = false;
            var rest = false;
            TypeNode elementType;
            switch (source)
            {
                case NamedTupleMemberTypeNode named:
                    sourceName = named.Name;
                    optional = named.Optional;
                    rest = named.Rest;
                    elementType = named.ElementType;
                    break;
                case OptionalTypeNode optionalType:
                    optional = true;
                    elementType = optionalType.InnerType;
                    break;
                case RestTypeNode restType:
                    rest = true;
                    elementType = restType.InnerType;
                    break;
                default:
                    elementType = source;
                    break;
            }

            if (rest && index != tuple.Elements.Count - 1)
                throw TupleShapeDeferral(
                    provenance,
                    "a rest element is not last");
            if (rest)
            {
                elementType = elementType switch
                {
                    ArrayTypeNode array => array.ElementType,
                    ReferenceTypeNode
                    {
                        Name: "Array" or "ReadonlyArray",
                        TypeArguments.Count: 1,
                    } reference => reference.TypeArguments[0],
                    _ => throw TupleShapeDeferral(
                        provenance,
                        "the rest element is not a homogeneous array type"),
                };
            }
            if (!optional && !rest && optionalSeen)
                throw TupleShapeDeferral(
                    provenance,
                    "a required element follows an optional element");
            optionalSeen |= optional;
            if (!liveReference)
            {
                EnsureRecursiveJsonTransport(
                    elementType,
                    $"{provenance}/element[{index}]",
                    "tuple-transport");
            }
            var projection = Project(
                elementType,
                $"{provenance}/element[{index}]",
                scope,
                depth + 1);
            ValidateGenericArgument("tuple", projection, provenance, index);
            var csharpName = Naming.ToCSharpMemberName(sourceName);
            if (!seenNames.Add(csharpName))
            {
                throw new GenericDeferralException(
                    $"Tuple labels at '{provenance}' collide on CLR member " +
                    $"'{csharpName}'.",
                    $"{provenance}/element[{index}]",
                    "synthesized-identity-collision");
            }
            elements.Add(new SynthesizedTupleElement(
                sourceName,
                csharpName,
                projection,
                optional,
                rest));
        }

        var typeName = liveReference
            ? _synthesizedTypes.RegisterReferenceTuple(provenance, elements, scope)
            : _synthesizedTypes.RegisterTuple(provenance, elements);
        return ReferenceType(
            typeName,
            isCollection: true,
            providerNote: liveReference
                ? "browser-reference-tuple"
                : "json-array-tuple",
            canonicalType: typeName,
            typeArguments: elements
                .Select(element => element.Projection.Identity)
                .ToList(),
            transport: liveReference
                ? BrowserReferenceTransport(tuple)
                : null);
    }

    private TypeProjection ProjectTypeLiteral(
        TypeLiteralTypeNode literal,
        string provenance,
        GenericScope? scope,
        int depth)
    {
        if (literal.Transport?.Kind is not (null or "json-value"))
        {
            var phase = literal.Members.Any(member =>
                member.Kind is "callSignature" or "constructSignature" or "indexSignature")
                ? "anonymous-structural-members"
                : "anonymous-js-reference";
            throw new GenericDeferralException(
                $"Anonymous structural type at '{provenance}' has authoritative " +
                $"transport '{literal.Transport.Kind}' and cannot be emitted as a JSON record.",
                $"{provenance}/transport",
                phase);
        }
        if (literal.Members.Any(member => member.Kind != "property"))
        {
            var facets = string.Join(
                ", ",
                literal.Members
                    .Where(member => member.Kind != "property")
                    .Select(member => member.Kind)
                    .Distinct(StringComparer.Ordinal));
            throw new GenericDeferralException(
                $"Anonymous structural type at '{provenance}' contains advanced member " +
                $"facets [{facets}].",
                provenance,
                "anonymous-structural-members");
        }

        var properties = new List<SynthesizedProperty>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in literal.Members.OrderBy(member => member.Ordinal))
        {
            if (member.Name is null
                || member.Name.Kind is "computed" or "private"
                || string.IsNullOrWhiteSpace(member.Name.Text)
                || member.Type is null)
            {
                throw new GenericDeferralException(
                    $"Anonymous structural member at '{provenance}/member[{member.Ordinal}]' " +
                    "does not have a stable string property identity and value type.",
                    $"{provenance}/member[{member.Ordinal}]",
                    "anonymous-structural-members");
            }
            EnsureRecursiveJsonTransport(
                member.Type,
                $"{provenance}/member[{member.Ordinal}]",
                "anonymous-json-transport");
            var projection = Project(
                member.Type,
                $"{provenance}/member[{member.Ordinal}]",
                scope,
                depth + 1);
            ValidateGenericArgument(
                "anonymous JSON record",
                projection,
                provenance,
                member.Ordinal);
            var csharpName = Naming.ToCSharpMemberName(member.Name.Text);
            if (!seenNames.Add(csharpName))
            {
                throw new GenericDeferralException(
                    $"Anonymous structural members at '{provenance}' collide on CLR " +
                    $"property '{csharpName}'.",
                    $"{provenance}/member[{member.Ordinal}]",
                    "synthesized-identity-collision");
            }
            properties.Add(new SynthesizedProperty(
                member.Name.Text,
                csharpName,
                projection,
                member.Optional,
                member.Documentation.Text,
                member.Documentation.Deprecated));
        }
        if (properties.Count == 0)
        {
            throw new GenericDeferralException(
                $"Empty anonymous structural type at '{provenance}' has no deterministic " +
                "JSON value contract.",
                provenance,
                "anonymous-structural-members");
        }

        var typeName = _synthesizedTypes.RegisterJsonRecord(provenance, properties);
        return ReferenceType(
            typeName,
            providerNote: "anonymous-json-record",
            canonicalType: typeName,
            typeArguments: properties
                .Select(property => property.Projection.Identity)
                .ToList());
    }

    private static GenericDeferralException TupleShapeDeferral(
        string provenance,
        string reason)
        => new(
            $"Tuple at '{provenance}' cannot preserve positional arity because {reason}.",
            provenance,
            "tuple-json-contract");

    private static void EnsureRecursiveJsonTransport(
        TypeNode type,
        string provenance,
        string phase)
    {
        if (type.Transport?.Kind is null or "json-value")
            return;
        throw new GenericDeferralException(
            $"JSON value at '{provenance}' cannot prove recursive compatibility: " +
            $"child transport is '{type.Transport.Kind}' " +
            $"({type.Transport.Reason ?? "no reviewed transport"}).",
            $"{provenance}/transport",
            phase);
    }

    private static bool ContainsUnion(TypeNode type)
        => type switch
        {
            UnionTypeNode => true,
            ParenthesizedTypeNode parenthesized => ContainsUnion(parenthesized.InnerType),
            _ => false,
        };

    private static bool ContainsConstrainedLiteral(TypeNode type)
        => type switch
        {
            LiteralTypeNode literal when literal.LiteralKind is
                "StringLiteral" or "TrueLiteral" or "FalseLiteral"
                => true,
            UnionTypeNode union => union.Types.Any(ContainsConstrainedLiteral),
            ParenthesizedTypeNode parenthesized
                => ContainsConstrainedLiteral(parenthesized.InnerType),
            _ => false,
        };

    public bool TryResolveFiniteStringDomain(
        TypeNode type,
        string provenance,
        out IReadOnlyList<string> values)
    {
        if (type is ParenthesizedTypeNode parenthesized)
        {
            return TryResolveFiniteStringDomain(
                parenthesized.InnerType,
                $"{provenance}/parenthesized",
                out values);
        }

        return TryResolveFiniteStringDomain(
            type,
            provenance,
            new HashSet<string>(StringComparer.Ordinal),
            new Dictionary<string, TypeNode>(StringComparer.Ordinal),
            out values);
    }

    private bool TryResolveFiniteStringDomain(
        TypeNode type,
        string provenance,
        ISet<string> visiting,
        IReadOnlyDictionary<string, TypeNode> substitutions,
        out IReadOnlyList<string> values)
    {
        if (type is ParenthesizedTypeNode parenthesized)
        {
            return TryResolveFiniteStringDomain(
                parenthesized.InnerType,
                $"{provenance}/parenthesized",
                visiting,
                substitutions,
                out values);
        }

        var result = new SortedSet<string>(StringComparer.Ordinal);
        var success = false;
        switch (type)
        {
            case OperatorTypeNode { Operator: "KeyOfKeyword" } operation:
                success = TryCollectFiniteKeys(
                    operation.OperandType,
                    provenance,
                    result,
                    new HashSet<string>(StringComparer.Ordinal));
                break;
            case LiteralTypeNode { LiteralKind: "StringLiteral" } literal:
                success = result.Add(Unquote(literal.Text));
                break;
            case UnionTypeNode union:
                success = union.Types
                    .Select((item, index) => (item, index))
                    .All(item =>
                        TryResolveFiniteStringDomain(
                            item.item,
                            $"{provenance}/union[{item.index}]",
                            visiting,
                            substitutions,
                            out var itemValues)
                        && AddAll(result, itemValues));
                break;
            case ReferenceTypeNode reference:
                if (substitutions.TryGetValue(reference.Name, out var substitution))
                {
                    success = TryResolveFiniteStringDomain(
                        substitution,
                        $"{provenance}/substitution[{reference.Name}]",
                        visiting,
                        substitutions,
                        out var substitutionValues)
                        && AddAll(result, substitutionValues);
                    break;
                }
                var symbolName = reference.ResolvedSymbol ?? reference.Name;
                if (_symbolIndex.TryGetValue(symbolName, out var symbol)
                    && visiting.Add(symbolName))
                {
                    try
                    {
                        var aliases = symbol.Declarations
                            .Where(declaration =>
                                declaration.Kind == "typeAlias"
                                && declaration.Type is not null)
                            .Select(declaration => declaration.Type!)
                            .ToList();
                        var parameters = symbol.Declarations
                            .Where(declaration => declaration.Kind == "typeAlias")
                            .Select(declaration => declaration.TypeParameters)
                            .FirstOrDefault(parameters => parameters.Count > 0)
                            ?? [];
                        var aliasSubstitutions =
                            substitutions.ToDictionary(
                                pair => pair.Key,
                                pair => pair.Value,
                                StringComparer.Ordinal);
                        if (reference.TypeArguments.Count > parameters.Count)
                        {
                            success = false;
                            break;
                        }
                        var validArguments = true;
                        for (var index = 0; index < parameters.Count; index++)
                        {
                            var argument = index < reference.TypeArguments.Count
                                ? reference.TypeArguments[index]
                                : parameters[index].Default;
                            if (argument is null)
                            {
                                validArguments = false;
                                break;
                            }
                            aliasSubstitutions[parameters[index].Name] = argument;
                        }
                        if (!validArguments)
                        {
                            break;
                        }
                        success = aliases.Count > 0
                            && aliases.All(alias =>
                                TryResolveFiniteStringDomain(
                                    alias,
                                    $"{provenance}/{symbolName}",
                                    visiting,
                                    aliasSubstitutions,
                                    out var aliasValues)
                                && AddAll(result, aliasValues));
                    }
                    finally
                    {
                        visiting.Remove(symbolName);
                    }
                }
                break;
            case TemplateLiteralTypeNode template:
                success = TryExpandTemplateLiteral(
                    template,
                    provenance,
                    visiting,
                    substitutions,
                    result);
                break;
        }
        values = result.ToList();
        return success && values.Count > 0;
    }

    private bool TryExpandTemplateLiteral(
        TemplateLiteralTypeNode template,
        string provenance,
        ISet<string> visiting,
        IReadOnlyDictionary<string, TypeNode> substitutions,
        ISet<string> values)
    {
        const int expansionLimit = 4096;
        var prefixes = new List<string> { template.Head };
        for (var index = 0; index < template.Spans.Count; index++)
        {
            var span = template.Spans[index];
            if (!TryResolveFiniteStringDomain(
                    span.Type,
                    $"{provenance}/span[{index}]",
                    visiting,
                    substitutions,
                    out var spanValues))
            {
                return false;
            }
            if ((long)prefixes.Count * spanValues.Count > expansionLimit)
                return false;
            prefixes = prefixes
                .SelectMany(prefix => spanValues.Select(value =>
                    $"{prefix}{value}{span.Literal}"))
                .ToList();
        }
        return AddAll(values, prefixes);
    }

    private TypeProjection ProjectTemplateLiteral(
        TemplateLiteralTypeNode template,
        string provenance)
    {
        if (template.Transport?.Kind is not (null or "json-value"))
        {
            throw new GenericDeferralException(
                $"Template literal at '{provenance}' has unsupported transport " +
                $"'{template.Transport.Kind}'.",
                $"{provenance}/transport",
                "template-literal-transport");
        }
        if (IsUnrestrictedStringTemplate(template))
        {
            return ReferenceType(
                "string",
                providerNote: "unrestricted-template-string");
        }
        if (TryResolveFiniteStringDomain(template, provenance, out var values))
        {
            var typeName = _synthesizedTypes.RegisterStringDomain(
                provenance,
                values);
            return ValueType(
                typeName,
                providerNote: "finite-template-string",
                canonicalType: typeName);
        }
        var checkerDomain = ExtractQuotedStringDomain(template.CheckerType);
        if (checkerDomain.Count > 1)
        {
            var typeName = _synthesizedTypes.RegisterStringDomain(
                provenance,
                checkerDomain);
            return ValueType(
                typeName,
                providerNote: "checker-finite-template-string",
                canonicalType: typeName);
        }
        var pattern = BuildTemplatePattern(template);
        var patternType = _synthesizedTypes.RegisterStringPattern(
            provenance,
            pattern,
            template.CheckerType ?? TypeFingerprint(template));
        return ValueType(
            patternType,
            providerNote: "validated-template-string-pattern",
            canonicalType: patternType);
    }

    private static bool IsUnrestrictedStringTemplate(
        TemplateLiteralTypeNode template)
        => string.Equals(template.CheckerType, "string", StringComparison.Ordinal)
            || string.IsNullOrEmpty(template.Head)
                && template.Spans.Count == 1
                && string.IsNullOrEmpty(template.Spans[0].Literal)
                && template.Spans[0].Type is KeywordTypeNode
                {
                    Name: "StringKeyword" or "string",
                };

    private static IReadOnlyList<string> ExtractQuotedStringDomain(string? checkerType)
    {
        if (string.IsNullOrWhiteSpace(checkerType))
            return [];
        return System.Text.RegularExpressions.Regex.Matches(
                checkerType,
                "\"(?<value>(?:\\\\.|[^\"\\\\])*)\"",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            .Select(match => System.Text.Json.JsonSerializer.Deserialize<string>(
                $"\"{match.Groups["value"].Value}\"")!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static string BuildTemplatePattern(TemplateLiteralTypeNode template)
    {
        var builder = new System.Text.StringBuilder("^");
        builder.Append(System.Text.RegularExpressions.Regex.Escape(template.Head ?? ""));
        foreach (var span in template.Spans)
        {
            builder.Append("[\\s\\S]*");
            builder.Append(System.Text.RegularExpressions.Regex.Escape(span.Literal ?? ""));
        }
        builder.Append('$');
        return builder.ToString();
    }

    private TypeProjection ProjectQuery(
        QueryTypeNode query,
        string provenance,
        GenericScope? scope,
        int depth)
    {
        if (query.ExprType is not null)
        {
            return Project(
                query.ExprType,
                $"{provenance}/query",
                scope,
                depth + 1);
        }
        if (query.CheckerType is { } checker
            && double.TryParse(
                checker,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out _))
        {
            return ValueType(
                "double",
                providerNote: $"literal-number:{checker}");
        }
        throw new GenericDeferralException(
            $"typeof query '{query.ExpressionName ?? query.CheckerType ?? "(unknown)"}' " +
            $"at '{provenance}' requires a logical factory or constant-domain projection.",
            provenance,
            "type-query-factory");
    }

    private TypeProjection ProjectImport(
        ImportTypeNode import,
        string provenance,
        GenericScope? scope,
        int depth)
    {
        if (import.IsTypeOf)
        {
            throw new GenericDeferralException(
                $"typeof import at '{provenance}' denotes a module value/factory rather " +
                "than an instance type.",
                provenance,
                "import-type-factory");
        }
        if (string.IsNullOrWhiteSpace(import.Qualifier))
        {
            throw new GenericDeferralException(
                $"Import type at '{provenance}' has no resolved qualified symbol.",
                provenance,
                "import-type-resolution");
        }
        var qualifier = import.Qualifier;
        if (!_symbolIndex.ContainsKey(qualifier))
        {
            throw new GenericDeferralException(
                $"Import type qualifier '{qualifier}' at '{provenance}' is not present " +
                "in the pinned symbol index.",
                provenance,
                "import-type-resolution");
        }
        var simpleName = qualifier[
            (qualifier.LastIndexOf('.') + 1)..];
        return ProjectReference(
            new ReferenceTypeNode(
                simpleName,
                qualifier,
                import.TypeArguments),
            $"{provenance}/import",
            scope,
            depth + 1) with
        {
            ProviderNote = "resolved-import-type",
        };
    }

            private TypeProjection ProjectOperator(
                OperatorTypeNode operation,
                string provenance,
                GenericScope? scope,
                int depth)
            {
                switch (operation.Operator)
                {
                    case "KeyOfKeyword":
                        if (!TryResolveFiniteStringDomain(
                                operation,
                                provenance,
                                out var keys))
                        {
                            throw new GenericDeferralException(
                                $"keyof at '{provenance}' has a dynamic or unresolvable property " +
                                "domain and cannot be widened to string.",
                                provenance,
                                "dynamic-key-domain");
                        }
                        throw new GenericDeferralException(
                            $"keyof at '{provenance}' resolves to the finite domain " +
                            $"[{string.Join(", ", keys.Select(key => $"'{key}'"))}], which requires " +
                            "a named generated value type rather than string.",
                            provenance,
                            "finite-key-domain");

                    case "ReadonlyKeyword":
                        if (operation.OperandType is ArrayTypeNode array)
                        {
                            var element = Project(
                                array.ElementType,
                                $"{provenance}/readonly/element",
                                scope,
                                depth + 1);
                            ValidateGenericArgument("readonly array", element, provenance, 0);
                            return ReferenceType(
                                $"IReadOnlyList<{element.RenderedType}>",
                                isCollection: true,
                                providerNote: "readonly-array",
                                canonicalType: $"IReadOnlyList<{element.CanonicalType}>",
                                typeArguments: [element.Identity]);
                        }
                        var operand = Project(
                            operation.OperandType,
                            $"{provenance}/readonly",
                            scope,
                            depth + 1);
                        if (!IsProvablyImmutable(operand))
                        {
                            throw new GenericDeferralException(
                                $"readonly operator at '{provenance}' targets mutable or unproven " +
                                $"CLR type '{operand.RenderedType}'.",
                                provenance,
                                "readonly-type-operator");
                        }
                        return operand with { ProviderNote = "readonly-operator" };

                    case "UniqueKeyword":
                        throw new GenericDeferralException(
                            $"unique symbol type at '{provenance}' has no supported CLR identity " +
                            "or JavaScript symbol-key transport.",
                            provenance,
                            "unique-symbol-types");

                    default:
                        throw new GenericDeferralException(
                            $"Type operator '{operation.Operator}' at '{provenance}' has no " +
                            "reviewed exact CLR reduction.",
                            provenance,
                            "advanced-type-operators");
                }
            }

            private TypeProjection ProjectIndexedAccess(
                IndexedAccessTypeNode indexed,
                string provenance,
                GenericScope? scope,
                int depth)
            {
                if (!TryResolveIndexedMembers(
                        indexed.ObjectType,
                        indexed.IndexType,
                        provenance,
                        scope,
                        out var selected,
                        out var dynamicReason))
                {
                    throw new GenericDeferralException(
                        $"Indexed access at '{provenance}' cannot be reduced exactly: " +
                        dynamicReason,
                        provenance,
                        "dependent-indexed-access");
                }

                var projections = selected.Select((member, index) =>
                {
                    var projection = Project(
                        member.Type,
                        $"{provenance}/selected[{index}]",
                        scope,
                        depth + 1);
                    return member.Optional
                        ? projection with { IsNullable = true }
                        : projection;
                }).ToList();
                if (projections.Count == 0)
                {
                    throw new GenericDeferralException(
                        $"Indexed access at '{provenance}' selected no members.",
                        provenance,
                        "dependent-indexed-access");
                }

                var canonical = projections[0].CanonicalType;
                if (projections.Any(projection =>
                        !string.Equals(
                            projection.CanonicalType,
                            canonical,
                            StringComparison.Ordinal)))
                {
                    var map = Project(
                        indexed.ObjectType,
                        $"{provenance}/map",
                        scope,
                        depth + 1);
                    var key = Project(
                        indexed.IndexType,
                        $"{provenance}/key",
                        scope,
                        depth + 1);
                    ValidateGenericArgument(
                        "indexed-access map",
                        map,
                        provenance,
                        0);
                    ValidateGenericArgument(
                        "indexed-access key",
                        key,
                        provenance,
                        1);
                    return ReferenceType(
                        "global::Microsoft.JSInterop.ITypeScriptIndexedAccess<" +
                        $"{map.RenderedType}, {key.RenderedType}>",
                        providerNote: "statically-reduced-indexed-access",
                        canonicalType:
                            "global::Microsoft.JSInterop.ITypeScriptIndexedAccess<" +
                            $"{map.CanonicalType},{key.CanonicalType}>",
                        typeArguments: [map.Identity, key.Identity],
                        transport: projections.All(projection =>
                                projection.Identity.Kind == ClrTypeKind.Reference)
                            ? new TransportModel(
                                "js-reference",
                                indexed.Transport?.Nullable == true,
                                indexed.Transport?.SourceType
                                    ?? indexed.CheckerType
                                    ?? "indexed access",
                                false,
                                false,
                                null)
                            : new TransportModel(
                                "json-value",
                                indexed.Transport?.Nullable == true,
                                indexed.Transport?.SourceType
                                    ?? indexed.CheckerType
                                    ?? "indexed access",
                                false,
                                true,
                                null));
                }
                return projections[0] with
                {
                    IsNullable = projections.Any(projection => projection.IsNullable),
                    ProviderNote = "statically-reduced-indexed-access",
                };
            }

            private bool TryResolveIndexedMembers(
                TypeNode objectType,
                TypeNode indexType,
                string provenance,
                GenericScope? scope,
                out IReadOnlyList<IndexedMember> selected,
                out string reason)
            {
                selected = [];
                reason = "the target is not a finite structural property domain.";
                if (!TryGetStructuralMembers(
                        objectType,
                        provenance,
                        out var members,
                        out var hasDynamicIndex,
                        new HashSet<string>(StringComparer.Ordinal)))
                {
                    return false;
                }

                var keys = new SortedSet<string>(StringComparer.Ordinal);
                if (!TryCollectIndexKeys(indexType, scope, provenance, keys))
                {
                    if (hasDynamicIndex)
                        reason = "the index is dynamic and the target has an index signature.";
                    else
                        reason = "the index domain is dynamic or not provably finite.";
                    return false;
                }

                var byName = members
                    .GroupBy(member => member.Name, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
                var result = new List<IndexedMember>();
                foreach (var key in keys)
                {
                    if (!byName.TryGetValue(key, out var matches))
                    {
                        if (!byName.TryGetValue("*", out matches))
                        {
                            reason = $"property '{key}' is not present across the resolved target.";
                            return false;
                        }
                    }
                    var fingerprints = matches
                        .Select(member => TypeFingerprint(member.Type))
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
                    if (fingerprints.Count != 1)
                    {
                        reason = $"merged property '{key}' has incompatible declarations.";
                        return false;
                    }
                    result.Add(new IndexedMember(
                        key,
                        matches[0].Type,
                        matches.Any(member => member.Optional)));
                }
                selected = result;
                reason = "";
                return true;
            }

            private bool TryCollectIndexKeys(
                TypeNode indexType,
                GenericScope? scope,
                string provenance,
                ISet<string> keys)
            {
                switch (indexType)
                {
                    case LiteralTypeNode { LiteralKind: "StringLiteral" } literal:
                        keys.Add(Unquote(literal.Text));
                        return true;
                    case LiteralTypeNode { LiteralKind: "NumericLiteral" } literal:
                        keys.Add(literal.Text);
                        return true;
                    case UnionTypeNode union:
                        return union.Types
                            .Select((item, index) => (item, index))
                            .All(item => TryCollectIndexKeys(
                                item.item,
                                scope,
                                $"{provenance}/index[{item.index}]",
                                keys));
                    case ParenthesizedTypeNode parenthesized:
                        return TryCollectIndexKeys(
                            parenthesized.InnerType,
                            scope,
                            $"{provenance}/index/parenthesized",
                            keys);
                    case ReferenceTypeNode reference
                        when scope?.TryResolve(
                            reference.Name,
                            reference.ResolvedSymbol,
                            out var binding) == true
                            && binding.Model.Constraint is not null:
                        return TryResolveFiniteStringDomain(
                                binding.Model.Constraint,
                                $"{provenance}/index/constraint",
                                out var values)
                            && AddAll(keys, values);
                    default:
                        return false;
                }
            }

            private bool TryCollectFiniteKeys(
                TypeNode operand,
                string provenance,
                ISet<string> keys,
                ISet<string> visiting)
            {
                if (!TryGetStructuralMembers(
                        operand,
                        provenance,
                        out var members,
                        out var hasDynamicIndex,
                        visiting)
                    || hasDynamicIndex)
                {
                    return false;
                }
                foreach (var member in members)
                {
                    if (member.Name != "*")
                        keys.Add(member.Name);
                }
                return keys.Count > 0;
            }

            private bool TryGetStructuralMembers(
                TypeNode type,
                string provenance,
                out IReadOnlyList<IndexedMember> members,
                out bool hasDynamicIndex,
                ISet<string> visiting)
            {
                switch (type)
                {
                    case ParenthesizedTypeNode parenthesized:
                        return TryGetStructuralMembers(
                            parenthesized.InnerType,
                            provenance,
                            out members,
                            out hasDynamicIndex,
                            visiting);
            case HeritageReferenceTypeNode heritage:
                return TryGetStructuralMembers(
                            new ReferenceTypeNode(
                                heritage.Expression,
                                heritage.ResolvedSymbol,
                                heritage.TypeArguments),
                            provenance,
                            out members,
                            out hasDynamicIndex,
                            visiting);
                    case TypeLiteralTypeNode literal:
                        return TryGetMembersFromDeclarations(
                            [new DeclarationModel(
                                0,
                                "typeAlias",
                                "anonymous",
                                [],
                                [],
                                [],
                                literal.Members,
                                literal,
                                [],
                                null,
                                new DocumentationModel("", [], false),
                                new LocationModel(
                                    provenance,
                                    new PositionModel(1, 1, 0),
                                    new PositionModel(1, 1, 0)),
                                null,
                                false,
                                new EventMapModel(false, []),
                                [])],
                            provenance,
                            out members,
                            out hasDynamicIndex,
                            visiting);
                    case ReferenceTypeNode reference:
                        var symbolName = reference.ResolvedSymbol ?? reference.Name;
                        if (!_symbolIndex.TryGetValue(symbolName, out var symbol)
                            || !visiting.Add(symbolName))
                        {
                            members = [];
                            hasDynamicIndex = false;
                            return false;
                        }
                        try
                        {
                            if (symbol.Declarations
                                .Where(declaration => declaration.Kind == "typeAlias")
                                .Select(declaration => declaration.Type)
                                .FirstOrDefault(node => node is not null) is { } aliasType)
                            {
                                return TryGetStructuralMembers(
                                    aliasType,
                                    $"{provenance}/{symbolName}",
                                    out members,
                                    out hasDynamicIndex,
                                    visiting);
                            }
                            return TryGetMembersFromDeclarations(
                                symbol.Declarations,
                                provenance,
                                out members,
                                out hasDynamicIndex,
                                visiting);
                        }
                        finally
                        {
                            visiting.Remove(symbolName);
                        }
                    default:
                        members = [];
                        hasDynamicIndex = false;
                        return false;
                }
            }

            private bool TryGetMembersFromDeclarations(
                IReadOnlyList<DeclarationModel> declarations,
                string provenance,
                out IReadOnlyList<IndexedMember> members,
                out bool hasDynamicIndex,
                ISet<string> visiting)
            {
                var result = new List<IndexedMember>();
                hasDynamicIndex = false;
                foreach (var declaration in declarations)
                {
                    foreach (var member in declaration.Members)
                    {
                        if (member.Kind == "indexSignature")
                        {
                            hasDynamicIndex = true;
                            if (member.Type is not null)
                            {
                                result.Add(new IndexedMember(
                                    "*",
                                    member.Type,
                                    member.Optional));
                            }
                            continue;
                        }
                        if (member.Kind is not (
                                "property" or "getter" or "setter" or "method")
                            || member.Name is null
                            || member.Name.Kind is "computed" or "private"
                            || GetMemberValueType(member) is not { } memberType)
                        {
                            continue;
                        }
                        result.Add(new IndexedMember(
                            Unquote(member.Name.Text),
                            memberType,
                            member.Optional));
                    }
                    foreach (var heritage in declaration.Heritage
                        .Where(clause => clause.Token == "extends")
                        .SelectMany(clause => clause.Types))
                    {
                        if (!TryGetStructuralMembers(
                                heritage,
                                $"{provenance}/heritage",
                                out var inherited,
                                out var inheritedDynamic,
                                visiting))
                        {
                            members = [];
                            return false;
                        }
                        result.AddRange(inherited);
                        hasDynamicIndex |= inheritedDynamic;
                    }
                }
                members = result;
                return result.Count > 0 || hasDynamicIndex;
            }

            private static TypeNode? GetMemberValueType(MemberModel member)
            {
                if (member.Type is not null)
                    return member.Type;
                if (member.Kind == "getter")
                    return member.ReturnType;
                if (member.Kind == "setter")
                    return member.Parameters.SingleOrDefault()?.Type;
                if (member.Kind == "method" && member.ReturnType is not null)
                {
                    return new FunctionTypeNode(
                        member.TypeParameters,
                        member.Parameters,
                        member.ReturnType)
                    {
                        Transport = member.ReturnType.Transport,
                    };
                }
                return null;
            }

            private static bool AddAll(ISet<string> target, IEnumerable<string> values)
            {
                foreach (var value in values)
                    target.Add(value);
                return true;
            }

            private static string Unquote(string value)
                => value.Length >= 2
                    && ((value[0] == '"' && value[^1] == '"')
                        || (value[0] == '\'' && value[^1] == '\''))
                    ? value[1..^1]
                    : value;

    private sealed record IndexedMember(string Name, TypeNode Type, bool Optional);

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
        var constraintNode = binding.Model.Constraint is ParenthesizedTypeNode parenthesized
            ? parenthesized.InnerType
            : binding.Model.Constraint;
        if (constraintNode is OperatorTypeNode
            {
                Operator: "keyof" or "KeyOfKeyword",
            } keyOf)
        {
            var map = Project(
                keyOf.OperandType,
                $"{constraintProvenance}/keyof",
                scope);
            ValidateGenericArgument("keyof constraint", map, constraintProvenance, 0);
            var rendered =
                $"global::Microsoft.JSInterop.ITypeScriptKeyOf<{map.RenderedType}>";
            var canonical =
                $"global::Microsoft.JSInterop.ITypeScriptKeyOf<{map.CanonicalType}>";
            return (rendered, canonical);
        }

        if (constraintNode is KeywordTypeNode
            {
                Name: "StringKeyword" or "string",
            })
        {
            const string marker =
                "global::Microsoft.JSInterop.ITypeScriptStringValue";
            return (marker, marker);
        }

        if (constraintNode.Transport?.Kind == "binary")
        {
            const string binaryCollection = "global::System.Collections.IList";
            return (binaryCollection, binaryCollection);
        }

        if (constraintNode is OperatorTypeNode
            or IndexedAccessTypeNode
            or IntersectionTypeNode
            or UnionTypeNode
            or TypeLiteralTypeNode
            or TemplateLiteralTypeNode
            or QueryTypeNode
            or KeywordTypeNode)
        {
            var marker = _synthesizedTypes.RegisterConstraint(
                constraintProvenance,
                constraintNode.CheckerType
                    ?? TypeFingerprint(constraintNode));
            return (marker, marker);
        }
        if (constraintNode is not ReferenceTypeNode reference)
        {
            var marker = _synthesizedTypes.RegisterConstraint(
                constraintProvenance,
                constraintNode.CheckerType
                    ?? TypeFingerprint(constraintNode));
            return (marker, marker);
        }
        if (scope.TryResolve(
                reference.Name,
                reference.ResolvedSymbol,
                out _) == false)
        {
            var target = reference.ResolvedSymbol ?? reference.Name;
            if (!IsInterfaceOrMixin(target))
            {
                var marker = _synthesizedTypes.RegisterConstraint(
                    constraintProvenance,
                    reference.CheckerType ?? target);
                return (marker, marker);
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
            NamedTupleMemberTypeNode named =>
                $"namedTuple:{named.Name}:{named.Optional}:{named.Rest}:" +
                TypeFingerprint(named.ElementType),
            OptionalTypeNode optional =>
                $"optional({TypeFingerprint(optional.InnerType)})",
            RestTypeNode rest =>
                $"rest({TypeFingerprint(rest.InnerType)})",
            LiteralTypeNode literal =>
                $"literal:{literal.LiteralKind}:{literal.Text}",
            ParenthesizedTypeNode parenthesized =>
                $"parenthesized({TypeFingerprint(parenthesized.InnerType)})",
            OperatorTypeNode operation =>
                $"operator:{operation.Operator}({TypeFingerprint(operation.OperandType)})",
            IndexedAccessTypeNode indexed =>
                $"indexed({TypeFingerprint(indexed.ObjectType)}," +
                $"{TypeFingerprint(indexed.IndexType)})",
            TemplateLiteralTypeNode template =>
                $"template:{template.Head}(" +
                $"{string.Join(",", template.Spans.Select(span =>
                    $"{TypeFingerprint(span.Type)}:{span.Literal}"))})",
            ImportTypeNode import =>
                $"import:{import.Qualifier}:{import.IsTypeOf}<" +
                $"{string.Join(",", import.TypeArguments.Select(TypeFingerprint))}>",
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
        var contract = name == "ReadonlyMap"
            ? "IReadOnlyBrowserMap"
            : "IBrowserMap";
        return ReferenceType(
            $"global::Microsoft.JSInterop.{contract}<{key.RenderedType}, {value.RenderedType}>",
            isCollection: true,
            canonicalType:
                $"global::Microsoft.JSInterop.{contract}<{key.CanonicalType},{value.CanonicalType}>",
            providerNote: "browser-map-reference",
            typeArguments: [key.Identity, value.Identity],
            transport: BrowserReferenceTransport(reference));
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
        var contract = name == "ReadonlySet"
            ? "IReadOnlyBrowserSet"
            : "IBrowserSet";
        return ReferenceType(
            $"global::Microsoft.JSInterop.{contract}<{item.RenderedType}>",
            isCollection: true,
            canonicalType:
                $"global::Microsoft.JSInterop.{contract}<{item.CanonicalType}>",
            providerNote: "browser-set-reference",
            typeArguments: [item.Identity],
            transport: BrowserReferenceTransport(reference));
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

    private static bool IsCallableReturn(string provenance)
        => provenance.EndsWith("/return", StringComparison.Ordinal)
            || provenance.EndsWith("/return/defaultExpansion", StringComparison.Ordinal)
            || provenance.EndsWith("/return/nullable", StringComparison.Ordinal)
            || provenance.EndsWith(
                "/return/defaultExpansion/nullable",
                StringComparison.Ordinal);

    private static bool RequiresLiveContainer(ReferenceTypeNode reference)
        => reference.Transport?.Kind is "unsupported" or "js-reference"
            && reference.Transport?.Reason?.Contains(
                "contains a non-JSON transport",
                StringComparison.Ordinal) != false;

    private static TransportModel BrowserReferenceTransport(ReferenceTypeNode reference)
        => new(
            "js-reference",
            reference.Transport?.Nullable == true,
            reference.Transport?.SourceType
                ?? reference.CheckerType
                ?? reference.Name,
            false,
            false,
            null);

    private static TransportModel BrowserReferenceTransport(TypeNode node)
        => new(
            "js-reference",
            node.Transport?.Nullable == true,
            node.Transport?.SourceType
                ?? node.CheckerType
                ?? node.Kind,
            false,
            false,
            null);

    private static TransportModel? AwaitedTransport(
        ReferenceTypeNode promise,
        TypeProjection inner)
    {
        if (inner.Identity.Kind == ClrTypeKind.Void)
        {
            return new TransportModel(
                "json-value",
                false,
                promise.Transport?.SourceType ?? promise.CheckerType ?? "Promise<void>",
                false,
                true,
                null);
        }
        if (inner.Transport is null)
            return promise.Transport;

        return inner.Transport with
        {
            Nullable = inner.IsNullable || inner.Transport.Nullable,
        };
    }

    private bool IsSemanticallyImmutable(
        TypeNode node,
        GenericScope? scope,
        HashSet<string> visitingSymbols,
        IReadOnlyDictionary<string, TypeNode> substitutions)
    {
        switch (node)
        {
            case KeywordTypeNode keyword:
                var keywordName = keyword.Name ?? keyword.CheckerType ?? "";
                return keywordName is
                    "BooleanKeyword" or "NumberKeyword" or "StringKeyword" or
                    "BigIntKeyword" or "boolean" or "number" or "string" or "bigint" or
                    "NullKeyword" or "UndefinedKeyword" or "null" or "undefined";

            case LiteralTypeNode literal:
                return literal.LiteralKind is
                    "StringLiteral" or "NumericLiteral" or "TrueKeyword" or
                    "FalseKeyword" or "BooleanLiteral" or "NullLiteral" or
                    "NullKeyword" or "UndefinedKeyword";

            case ParenthesizedTypeNode parenthesized:
                return IsSemanticallyImmutable(
                    parenthesized.InnerType,
                    scope,
                    visitingSymbols,
                    substitutions);

            case UnionTypeNode union:
                return union.Types.All(arm => IsSemanticallyImmutable(
                    arm,
                    scope,
                    visitingSymbols,
                    substitutions));

            case ReferenceTypeNode reference:
                return IsImmutableReference(
                    reference,
                    scope,
                    visitingSymbols,
                    substitutions);

            default:
                return false;
        }
    }

    private bool IsImmutableReference(
        ReferenceTypeNode reference,
        GenericScope? scope,
        HashSet<string> visitingSymbols,
        IReadOnlyDictionary<string, TypeNode> substitutions)
    {
        if (!string.IsNullOrWhiteSpace(reference.ResolvedSymbol)
            && substitutions.TryGetValue(reference.ResolvedSymbol, out var resolved))
        {
            if (ReferenceEquals(resolved, reference))
                return false;
            return IsSemanticallyImmutable(
                resolved,
                scope,
                visitingSymbols,
                substitutions);
        }
        if (substitutions.TryGetValue(reference.Name, out var source))
        {
            if (ReferenceEquals(source, reference))
                return false;
            return IsSemanticallyImmutable(
                source,
                scope,
                visitingSymbols,
                substitutions);
        }
        if (scope?.TryResolve(
                reference.Name,
                reference.ResolvedSymbol,
                out var parameter) == true)
        {
            return parameter.Substitution is not null
                && IsImmutableSubstitution(parameter.Substitution);
        }

        if (GlTypeAliases.ContainsKey(reference.Name)
            || reference.Name is
                "DOMString" or "USVString" or "ByteString" or
                "DOMHighResTimeStamp" or "EpochTimeStamp" or "DOMTimeStamp")
        {
            return true;
        }

        SymbolModel? symbol = null;
        if (!string.IsNullOrWhiteSpace(reference.ResolvedSymbol))
        {
            _symbolIndex.TryGetValue(reference.ResolvedSymbol, out symbol);
            if (symbol is null
                && !string.Equals(
                    reference.ResolvedSymbol,
                    reference.Name,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        if (symbol is null)
            _symbolIndex.TryGetValue(reference.Name, out symbol);
        if (symbol is null)
            return false;

        var classification =
            EffectiveClassificationPolicy.Classify(symbol, _overrides).Name;
        if (classification == "enum")
            return true;
        if (classification != "typedef" || !visitingSymbols.Add(symbol.Name))
            return false;

        try
        {
            var declaration = symbol.Declarations
                .Where(candidate => candidate.Kind == "typeAlias")
                .OrderBy(candidate => candidate.Ordinal)
                .FirstOrDefault();
            if (declaration?.Type is null)
                return false;
            var parameters = GetSymbolTypeParameters(
                symbol,
                $"{symbol.Name}/readonly/typeParameters");
            if (reference.TypeArguments.Count > parameters.Count)
                return false;
            var aliasSubstitutions = new Dictionary<string, TypeNode>(
                substitutions,
                StringComparer.Ordinal);
            for (var index = 0; index < parameters.Count; index++)
            {
                var aliasParameter = parameters[index];
                var argument = index < reference.TypeArguments.Count
                    ? reference.TypeArguments[index]
                    : aliasParameter.Default;
                if (argument is null)
                    return false;
                aliasSubstitutions[aliasParameter.Name] = argument;
                aliasSubstitutions[$"{symbol.Name}.{aliasParameter.Name}"] = argument;
            }
            return IsSemanticallyImmutable(
                declaration.Type,
                scope,
                visitingSymbols,
                aliasSubstitutions);
        }
        finally
        {
            visitingSymbols.Remove(symbol.Name);
        }
    }

    private static bool IsImmutableSubstitution(TypeProjection projection)
        => projection.Identity.CanonicalName is
            "bool" or "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or
            "long" or "ulong" or "float" or "double" or "decimal" or "char" or "string";
    private static bool IsProvablyImmutable(TypeProjection projection)
        => projection.Identity.Kind == ClrTypeKind.Value
            || string.Equals(
                projection.Identity.CanonicalName,
                "string",
                StringComparison.Ordinal)
            || projection.ProviderNote is
                "json-array-tuple" or "anonymous-json-record";

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

    private TypeProjection ProjectIteratorGenericArgument(
        TypeNode type,
        string provenance,
        GenericScope? scope,
        int depth)
    {
        if (type is ReferenceTypeNode { Name: "BuiltinIteratorReturn" })
            return BrowserUndefinedType();
        var projection = Project(type, provenance, scope, depth + 1);
        return projection.Identity.Kind is ClrTypeKind.Null or ClrTypeKind.Void
            ? BrowserUndefinedType()
            : projection;
    }

    private static TypeProjection BrowserUndefinedType()
        => ValueType(
            "global::Microsoft.JSInterop.BrowserUndefined",
            providerNote: "browser-undefined",
            canonicalType: "global::Microsoft.JSInterop.BrowserUndefined");

    private static TypeProjection BrowserNullType()
        => ValueType(
            "global::Microsoft.JSInterop.BrowserNull",
            providerNote: "browser-null",
            canonicalType: "global::Microsoft.JSInterop.BrowserNull");

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
        IReadOnlyList<ClrTypeIdentity>? typeArguments = null,
        TransportModel? transport = null)
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
            providerNote,
            transport);

    private static TypeProjection ReferenceType(
        string csharpType,
        bool isCollection = false,
        string providerNote = "",
        string? canonicalType = null,
        IReadOnlyList<ClrTypeIdentity>? typeArguments = null,
        TransportModel? transport = null)
        => new(
            csharpType,
            false,
            isCollection,
            new ClrTypeIdentity(
                canonicalType ?? csharpType,
                ClrTypeKind.Reference,
                GenericArity: typeArguments?.Count ?? 0,
                TypeArguments: typeArguments),
            providerNote,
            transport);

    private TypeProjection TypeParameter(GenericParameterBinding parameter)
        => new(
            parameter.CSharpName,
            false,
            false,
            new ClrTypeIdentity(
                parameter.CanonicalIdentity,
                ClrTypeKind.Reference,
                IsTypeParameter: true),
            $"type-parameter:{parameter.SourceName}",
            parameter.Model.Constraint?.Transport is
            {
                Kind: not "unsupported",
            } constraintTransport
                ? constraintTransport
                : null);

    private static TypeProjection VoidType()
        => new("void", false, false, new ClrTypeIdentity("void", ClrTypeKind.Void));

    private static TypeProjection NullType()
        => new("null", true, false, new ClrTypeIdentity("null", ClrTypeKind.Null));
}
