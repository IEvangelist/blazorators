// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Parsers;

internal sealed partial class TypeDeclarationParser
{
    private static HashSet<string> CreateVisitingSet() =>
        new(StringComparer.Ordinal);

    private static Dictionary<string, CSharpObject> CreateMemo() =>
        new(StringComparer.Ordinal);

    internal CSharpObject? ToObject(string typeScriptTypeDeclaration) =>
        ToObject(typeScriptTypeDeclaration, CreateVisitingSet(), CreateMemo());

    private CSharpObject? ToObject(
        string typeScriptTypeDeclaration,
        HashSet<string> visiting,
        Dictionary<string, CSharpObject> memo)
    {
        // Callback interfaces don't have parseable methods or properties in
        // the regex grammar (the body is an anonymous call signature). We
        // still emit a placeholder `CSharpObject` so downstream emission
        // can filter on `IsActionParameter`/`IsCallback`, but bail out of
        // the rest of the per-line parser.
        var isCallback = IsCallbackTypeDeclaration(typeScriptTypeDeclaration);

        CSharpObject? cSharpObject = null;
        string? trackedTypeName = null;

        var lineTokens = typeScriptTypeDeclaration.Split(['\n']);
        foreach (var (index, segment) in lineTokens.Select((s, i) => (i, s)))
        {
            if (index == 0)
            {
                var rawTypeName = InterfaceTypeNameRegex.GetMatchGroupValue(segment, "TypeName");
                // Generic interface headers like `interface CustomEventInit<T = any> extends ... {`
                // capture as `"CustomEventInit<T"` because the underlying
                // regex greedy-matches non-whitespace. Normalize so the
                // resulting `CSharpObject.TypeName` is a bare identifier
                // and the reader/parser dictionaries can look the type
                // up. Generic propagation (rendering `<T>` in emitted
                // C#) is deferred to a later phase.
                var typeName = NormalizeTypeName(rawTypeName);

                // Parse the *full* extends list. TS supports
                // `interface X extends A, B, C { ... }` and the
                // single-token regex used to silently drop B and C
                // (and would even capture `A,` as the subclass name).
                // EventTarget is excluded from the merge: it carries
                // the DOM event-listener machinery that isn't useful
                // through JS interop, and the original code already
                // stripped it for the single-extend case.
                var extendsBases = ExtractExtendsBases(segment);
                string? subclass = extendsBases.Count > 0 ? extendsBases[0] : null;

                if (typeName is not null)
                {
                    // Memo first: if we already finished parsing this type
                    // in this root call, hand back the same object so the
                    // entire dependent-type graph is parsed at most once
                    // per root. Without this, types shared across many
                    // properties (e.g. `Element`, `Node`) get re-parsed
                    // dozens of times per root and the corpus harness
                    // takes minutes instead of seconds.
                    if (memo.TryGetValue(typeName, out var memoized))
                    {
                        return memoized;
                    }

                    // Cycle guard: lib.dom.d.ts has type graphs that recurse
                    // (Node <-> Document, ParentNode <-> Element, etc.). The
                    // dependent-type discovery below recurses into ToObject
                    // for every non-primitive property type and return type;
                    // without this short-circuit a single Node-rooted parse
                    // blows the stack. If we're already building this type
                    // in an outer frame, return a placeholder so the outer
                    // frame populates the full members.
                    if (!visiting.Add(typeName))
                    {
                        return new(typeName, subclass) { IsCallback = isCallback };
                    }

                    trackedTypeName = typeName;
                    cSharpObject = new(typeName, subclass) { IsCallback = isCallback };
                    if (isCallback)
                    {
                        visiting.Remove(typeName);
                        memo[typeName] = cSharpObject;
                        return cSharpObject;
                    }

                    // Merge every base interface's members into the
                    // derived type. We do this *before* walking the
                    // derived's own member lines so that any member
                    // the derived re-declares naturally shadows the
                    // base via the dictionary writes below (derived
                    // wins on conflict).
                    foreach (var baseName in extendsBases)
                    {
                        MergeBaseMembers(cSharpObject, baseName, visiting, memo);
                    }

                    continue;
                }
                else
                {
                    break;
                }
            }

            if (cSharpObject is null)
            {
                break;
            }

            var line = segment.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line == "}")
            {
                // We're done
                break;
            }

            if (IsMethod(line, out var method) && method is not null)
            {
                var methodName = method.GetGroupValue("MethodName");
                var parameters = method.GetGroupValue("Parameters");
                var returnType = method.GetGroupValue("ReturnType");

                if (methodName is null || parameters is null || returnType is null)
                {
                    continue;
                }

                var (parameterDefinitions, javaScriptMethod) =
                    ParseParameters(
                        cSharpObject.TypeName,
                        methodName,
                        parameters,
                        obj => cSharpObject.DependentTypes![obj.TypeName] = obj,
                        visiting,
                        memo);


                var cSharpMethod =
                    ToMethod(methodName, returnType, parameterDefinitions, javaScriptMethod, visiting, memo);
                cSharpObject.Methods[cSharpMethod.RawName] = cSharpMethod;

                continue;
            }

            if (IsProperty(line, out var property) && property is not null)
            {
                var name = property.GetGroupValue("Name");
                var type = property.GetGroupValue("Type");

                if (name is null || type is null)
                {
                    continue;
                }

                var isReadonly = name.StartsWith("readonly ", StringComparison.Ordinal);
                // TS uses `?:` (optional), ` | null`, and ` | undefined`
                // somewhat interchangeably for "this value might not
                // be present". They all collapse to a C# nullable
                // property; the runtime distinction isn't observable
                // through JSON round-tripping. `HasNullClause` reads
                // the trimmed type token from the property regex --
                // the regex captures whitespace-trimmed content so a
                // leading space (` | null` vs `| null`) needs to be
                // tolerated by checking both spellings.
                var isNullable = name.EndsWith("?", StringComparison.Ordinal)
                    || type.EndsWith("| null", StringComparison.Ordinal)
                    || type.EndsWith("| undefined", StringComparison.Ordinal);

                name = name.Replace("?", "").Replace("readonly ", "");
                type = TryGetPrimitiveType(type);

                CSharpProperty cSharpProperty = new(name, type, isNullable, isReadonly);
                cSharpObject.Properties[cSharpProperty.RawName] = cSharpProperty;

                var mappedType = cSharpProperty.MappedTypeName;

                // When a property defines a custom type, that type needs to also be parsed
                // and source generated. This is so that dependent types are known and resolved.
                if (!TypeMap.PrimitiveTypes.IsPrimitiveType(mappedType) &&
                    !visiting.Contains(mappedType) &&
                    _reader.TryGetDeclaration(mappedType, out var typeScriptDefinitionText) &&
                    typeScriptDefinitionText is not null)
                {
                    var obj = ToObject(typeScriptDefinitionText, visiting, memo);
                    if (obj is not null)
                    {
                        cSharpObject.DependentTypes![obj.TypeName] = obj;
                    }
                }

                continue;
            }
        }

        if (trackedTypeName is not null)
        {
            visiting.Remove(trackedTypeName);
            if (cSharpObject is not null)
            {
                memo[trackedTypeName] = cSharpObject;
            }
        }

        return cSharpObject;
    }

    internal CSharpTopLevelObject? ToTopLevelObject(string typeScriptTypeDeclaration) =>
        ToTopLevelObject(typeScriptTypeDeclaration, CreateVisitingSet(), CreateMemo());

    private CSharpTopLevelObject? ToTopLevelObject(
        string typeScriptTypeDeclaration,
        HashSet<string> visiting,
        Dictionary<string, CSharpObject> memo)
    {
        CSharpTopLevelObject? topLevelObject = null;
        string? trackedTypeName = null;

        var lineTokens = typeScriptTypeDeclaration.Split(['\n']);
        foreach (var (index, segment) in lineTokens.Select((s, i) => (i, s)))
        {
            if (index == 0)
            {
                var typeName = NormalizeTypeName(
                    InterfaceTypeNameRegex.GetMatchGroupValue(segment, "TypeName"));
                if (typeName is not null)
                {
                    // Cycle guard: see ToObject for rationale.
                    if (!visiting.Add(typeName))
                    {
                        return new(typeName);
                    }

                    trackedTypeName = typeName;
                    topLevelObject = new(typeName);
                    continue;
                }
                else
                {
                    break;
                }
            }

            if (topLevelObject is null)
            {
                break;
            }

            var line = segment.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line == "}")
            {
                // We're done
                break;
            }

            if (IsMethod(line, out var method) && method is not null)
            {
                var methodName = method.GetGroupValue("MethodName");
                var parameters = method.GetGroupValue("Parameters");
                var returnType = method.GetGroupValue("ReturnType");

                if (methodName is null || parameters is null || returnType is null)
                {
                    continue;
                }

                var (parameterDefinitions, javaScriptMethod) =
                    ParseParameters(
                        topLevelObject.RawTypeName,
                        methodName,
                        parameters,
                        obj => topLevelObject.DependentTypes![obj.TypeName] = obj,
                        visiting,
                        memo);

                var cSharpMethod =
                    ToMethod(methodName, returnType, parameterDefinitions, javaScriptMethod, visiting, memo);

                topLevelObject.Methods!.Add(cSharpMethod);

                continue;
            }

            if (IsProperty(line, out var property) && property is not null)
            {
                var name = property.GetGroupValue("Name");
                var type = property.GetGroupValue("Type");

                if (name is null || type is null)
                {
                    continue;
                }

                var isReadonly = name.StartsWith("readonly ", StringComparison.Ordinal);
                // See `ToObject` for rationale: `?:`, ` | null`,
                // and ` | undefined` all collapse to a C# nullable
                // property; both clause suffixes need to be detected.
                var isNullable = name.EndsWith("?", StringComparison.Ordinal)
                    || type.EndsWith("| null", StringComparison.Ordinal)
                    || type.EndsWith("| undefined", StringComparison.Ordinal);

                name = name.Replace("?", "").Replace("readonly ", "");
                type = TryGetPrimitiveType(type);

                CSharpProperty cSharpProperty =
                    new(name,
                    type,
                    isNullable,
                    isReadonly);

                topLevelObject.Properties!.Add(cSharpProperty);

                var mappedType = cSharpProperty.MappedTypeName;

                // When a property defines a custom type, that type needs to also be parsed
                // and source generated. This is so that dependent types are known and resolved.
                if (!TypeMap.PrimitiveTypes.IsPrimitiveType(mappedType) &&
                    !visiting.Contains(mappedType) &&
                    _reader.TryGetDeclaration(mappedType, out var typeScriptDefinitionText) &&
                    typeScriptDefinitionText is not null)
                {
                    var obj = ToObject(typeScriptDefinitionText, visiting, memo);
                    if (obj is not null)
                    {
                        topLevelObject.DependentTypes![obj.TypeName] = obj;
                    }
                }

                continue;
            }
        }

        if (trackedTypeName is not null)
        {
            visiting.Remove(trackedTypeName);
        }

        return topLevelObject;
    }

    private string TryGetPrimitiveType(string type)
    {
        if (TypeMap.PrimitiveTypes.IsPrimitiveType(type))
        {
            return type;
        }

        // Carry the trailing nullable clause (` | null` or
        // ` | undefined`) across the alias lookup. The alias map is
        // keyed by the bare identifier (e.g. `DOMHighResTimeStamp`),
        // never `DOMHighResTimeStamp | null`, so without this strip
        // the resolution missed any property/parameter whose alias
        // type also tolerated `null` (or `undefined`). Both forms map
        // onto a C# nullable type so we collapse to ` | null` after
        // detection -- downstream callers only inspect for ` | null`.
        var hadNullClause = TypeShape.HasNullClause(type);
        var bareType = hadNullClause ? TypeShape.StripNullClause(type) : type;

        if (TypeMap.PrimitiveTypes.IsPrimitiveType(bareType))
        {
            // Normalise to the ` | null` form so the primitive map's
            // pre-baked nullable entries (`"number | null"` ->
            // `"double?"`, etc.) light up regardless of whether the
            // source spelled it ` | undefined`.
            return hadNullClause ? $"{bareType} | null" : type;
        }

        if (!_reader.TryGetTypeAlias(bareType, out var typeAliasLine) ||
            typeAliasLine is null)
        {
            return type;
        }

        if (typeAliasLine.Replace(";", "").Split('=')
            is not { Length: 2 } split)
        {
            return type;
        }

        var rhs = split[1].Trim();

        // Single-token primitive alias, e.g. `type DOMHighResTimeStamp = number;`,
        // `type GLuint = number;`, `type GLboolean = boolean;`. These appear
        // throughout the DOM lib for unit/precision affordances; without
        // resolution they slipped through as raw TS identifiers in the
        // generated C# signatures.
        if (TypeMap.PrimitiveTypes.IsPrimitiveType(rhs))
        {
            return hadNullClause ? $"{rhs} | null" : rhs;
        }

        if (rhs.Split('|')
            is { Length: > 0 } values &&
            values.Select(v => v.Trim())?.ToList()
            is { Count: > 0 } list)
        {
            var isStringAlias = list.All(v => v.StartsWith("\"", StringComparison.Ordinal) && v.EndsWith("\"", StringComparison.Ordinal));
            if (isStringAlias)
            {
                return hadNullClause ? "string | null" : "string";
            }
        }

        return type;
    }

    private CSharpMethod ToMethod(
        string methodName,
        string returnType,
        List<CSharpType> parameterDefinitions,
        JavaScriptMethod? javaScriptMethod,
        HashSet<string> visiting,
        Dictionary<string, CSharpObject> memo)
    {
        // Resolve simple TS type aliases (`type GLuint = number;`,
        // `type DOMHighResTimeStamp = number;`, etc.) up front so that the
        // downstream primitive map / declaration lookup sees the bare TS
        // primitive instead of the alias identifier. Without this the
        // emitter dropped the raw alias name into the method signature.
        var methodReturnType = TryGetPrimitiveType(CleanseReturnType(returnType));
        CSharpMethod cSharpMethod =
            new(methodName,
            methodReturnType,
            parameterDefinitions,
            javaScriptMethod);

        // For `Promise<T>` returns the dependent-type lookup must see
        // the unwrapped `T`. Without this peel the registration call
        // searched the declaration map for the literal `Promise<X>`
        // key, never found it, and the consuming code referenced an
        // undefined DTO class. Use the same `PromiseUnwrappedTypeName`
        // helper that drives the C# emit path so both stay in sync.
        var lookupReturnType = cSharpMethod.IsPromise
            ? cSharpMethod.PromiseUnwrappedTypeName
            : methodReturnType;

        // Resolve the element type for dependent-DTO emission. Previously
        // the code did a textual `Replace("[]", "")` which only handled
        // the `T[]` shape (missing `Array<T>` and `ReadonlyArray<T>`) and
        // would also strip a `[]` from inside nested generic arguments.
        // Route through `TypeShape` so all three array forms agree with
        // the parameter side and `CSharpProperty.MappedTypeName`. Also
        // strip a trailing nullable clause -- `Promise<T | null>` and
        // `T | null` returns both need the dependent type resolved
        // against the bare `T`.
        var bareLookupType = TypeShape.StripNullClause(lookupReturnType);
        var nonArrayMethodReturnType =
            TypeShape.TryGetArrayElementTypeName(bareLookupType, out var elementTypeName)
                ? elementTypeName
                : bareLookupType;

        if (!TypeMap.PrimitiveTypes.IsPrimitiveType(nonArrayMethodReturnType) &&
            !visiting.Contains(nonArrayMethodReturnType) &&
            _reader.TryGetDeclaration(nonArrayMethodReturnType, out var typeScriptDefinitionText) &&
            typeScriptDefinitionText is not null)
        {
            var dependentType = ToObject(typeScriptDefinitionText, visiting, memo);
            if (dependentType is not null)
            {
                cSharpMethod.DependentTypes![nonArrayMethodReturnType] = dependentType;
            }
        }

        return cSharpMethod;
    }

    internal CSharpAction? ToAction(string typeScriptTypeDeclaration) =>
        ToAction(typeScriptTypeDeclaration, CreateVisitingSet(), CreateMemo());

    private CSharpAction? ToAction(
        string typeScriptTypeDeclaration,
        HashSet<string> visiting,
        Dictionary<string, CSharpObject> memo)
    {
        CSharpAction? cSharpAction = null;
        string? trackedTypeName = null;

        var lineTokens = typeScriptTypeDeclaration.Split(['\n']);
        foreach (var (index, segment) in lineTokens.Select((s, i) => (i, s)))
        {
            if (index == 0)
            {
                var typeName = NormalizeTypeName(
                    InterfaceTypeNameRegex.GetMatchGroupValue(segment, "TypeName"));
                if (typeName is not null)
                {
                    // Cycle guard: see ToObject for rationale.
                    if (!visiting.Add(typeName))
                    {
                        return new(typeName);
                    }

                    trackedTypeName = typeName;
                    cSharpAction = new(typeName);
                    continue;
                }
                else
                {
                    break;
                }
            }

            if (cSharpAction is null)
            {
                break;
            }

            var line = segment.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line == "}")
            {
                // We're done
                break;
            }

            if (IsAction(line, out var action) && action is not null)
            {
                var parameters = action.GetGroupValue("Parameters");
                var returnType = action.GetGroupValue("ReturnType");

                if (parameters is null || returnType is null)
                {
                    continue;
                }

                var (parameterDefinitions, _) =
                    ParseParameters(
                        cSharpAction.RawName,
                        cSharpAction.RawName,
                        parameters,
                        obj => cSharpAction.DependentTypes![obj.TypeName] = obj,
                        visiting,
                        memo);

                cSharpAction = cSharpAction with
                {
                    ParameterDefinitions = parameterDefinitions
                };

                continue;
            }
        }

        if (trackedTypeName is not null)
        {
            visiting.Remove(trackedTypeName);
        }

        return cSharpAction;
    }

    internal static string CleanseReturnType(string returnType)
    {
        // Example inputs:
        // 1) ": void;"
        // 2) ": string | null;"
        return returnType.Replace(":", "").Replace(";", "").Trim();
    }

    internal (List<CSharpType> Parameters, JavaScriptMethod? JavaScriptMethod) ParseParameters(
        string typeName,
        string rawName,
        string parametersString,
        Action<CSharpObject> appendDependentType) =>
        ParseParameters(typeName, rawName, parametersString, appendDependentType,
            CreateVisitingSet(), CreateMemo());

    private (List<CSharpType> Parameters, JavaScriptMethod? JavaScriptMethod) ParseParameters(
        string typeName,
        string rawName,
        string parametersString,
        Action<CSharpObject> appendDependentType,
        HashSet<string> visiting,
        Dictionary<string, CSharpObject> memo)
    {
        List<CSharpType> parameters = [];

        // Example input:
        // "(someCallback: CallbackType, someId?: number | null)"
        JavaScriptMethod? javaScriptMethod = new(rawName);
        foreach (var parameterSegment in SplitTopLevelParameters(parametersString))
        {
            if (!TrySplitParameterNameAndType(parameterSegment, out var rawNameToken, out var rawTypeToken))
            {
                continue;
            }

            var isNullable = rawNameToken.EndsWith("?", StringComparison.Ordinal);
            var parameterName = isNullable
                ? rawNameToken.Substring(0, rawNameToken.Length - 1).Trim()
                : rawNameToken.Trim();

            var trimmedType = rawTypeToken.Trim();

            // A TS parameter is nullable if EITHER the name has a `?` suffix
            // (`x?: T`) OR the type has a ` | null` / ` | undefined` clause
            // (`x: T | null`, `x: T | undefined`). We normalize all forms
            // onto `isNullable=true` and strip the clause so the
            // downstream primitive/declaration lookups see a clean `T`.
            // Previously only primitives whose exact `"T | null"` key
            // existed in `TypeMap` survived this -- e.g. `string | null`
            // worked because the map had an entry, but `string[] | null`
            // (or any custom interface) emitted `string[] | null x`
            // verbatim, which isn't valid C#. `| undefined` was not
            // recognised at all, leaving the clause in the emitted
            // signature for callbacks and Promise-derived parameters.
            if (TypeShape.HasNullClause(trimmedType))
            {
                isNullable = true;
                trimmedType = TypeShape.StripNullClause(trimmedType);
            }

            // Resolve simple TS type aliases here too. The parameter path
            // shares the same alias-aware behaviour as the property and
            // return-type paths so that a method like
            // `requestAnimationFrame(callback: FrameRequestCallback)` is
            // distinct from a hypothetical `(time: DOMHighResTimeStamp)`
            // and the latter emits `double time` instead of the raw alias.
            trimmedType = TryGetPrimitiveType(trimmedType);

            var parameterType = trimmedType;

            CSharpAction? action = null;

            // Look up the parameter type in the declaration map. For array
            // shapes (`T[]`, `Array<T>`, `ReadonlyArray<T>`) the registered
            // declaration is the element type, so peel the wrapper before
            // hitting the reader. Previously array-of-custom-type parameters
            // never reached `appendDependentType`, so the consuming code
            // ended up referencing an undefined DTO class.
            var declarationLookupType =
                TypeShape.TryGetArrayElementTypeName(parameterType, out var elementType)
                    ? elementType
                    : parameterType;

            if (!TypeMap.PrimitiveTypes.IsPrimitiveType(declarationLookupType) &&
                !visiting.Contains(declarationLookupType) &&
                _reader.TryGetDeclaration(declarationLookupType, out var typeScriptDefinitionText) &&
                typeScriptDefinitionText is not null)
            {
                javaScriptMethod = javaScriptMethod with
                {
                    InvokableMethodName = $"blazorators.{typeName.LowerCaseFirstLetter()}.{rawName}"
                };

                if (IsCallbackTypeDeclaration(typeScriptDefinitionText))
                {
                    action = ToAction(typeScriptDefinitionText, visiting, memo);
                    javaScriptMethod = javaScriptMethod with
                    {
                        IsBiDirectionalJavaScript = true
                    };
                }
                else
                {
                    var obj = ToObject(typeScriptDefinitionText, visiting, memo);
                    if (obj is not null)
                    {
                        appendDependentType(obj);
                    }
                }
            }

            parameters.Add(new(parameterName, parameterType, isNullable, action));
        }

        javaScriptMethod = javaScriptMethod with
        {
            ParameterDefinitions = parameters
        };

        return (parameters, javaScriptMethod);
    }

    /// <summary>
    /// Splits a raw TypeScript parameter list (with or without the enclosing
    /// parentheses) into per-parameter segments, respecting nested generics,
    /// function-type parameters, object-literal types, and tuple/array types.
    /// </summary>
    internal static List<string> SplitTopLevelParameters(string parametersString)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(parametersString))
        {
            return result;
        }

        var trimmed = parametersString.Trim();
        if (trimmed.Length >= 2 &&
            trimmed[0] == '(' &&
            trimmed[trimmed.Length - 1] == ')')
        {
            trimmed = trimmed.Substring(1, trimmed.Length - 2);
        }

        if (trimmed.Length == 0)
        {
            return result;
        }

        var brackets = 0;
        var angles = 0;
        var current = new StringBuilder();

        foreach (var c in trimmed)
        {
            switch (c)
            {
                case '(':
                case '[':
                case '{':
                    brackets++;
                    break;

                case ')':
                case ']':
                case '}':
                    if (brackets > 0)
                    {
                        brackets--;
                    }
                    break;

                case '<':
                    angles++;
                    break;

                case '>':
                    // '>' may appear as part of '=>' in TS function types; only treat it
                    // as a generic-list close when we've actually opened a '<'.
                    if (angles > 0)
                    {
                        angles--;
                    }
                    break;

                case ',' when brackets == 0 && angles == 0:
                    AppendSegment(result, current);
                    current.Clear();
                    continue;
            }

            current.Append(c);
        }

        AppendSegment(result, current);
        return result;

        static void AppendSegment(List<string> sink, StringBuilder buffer)
        {
            if (buffer.Length == 0)
            {
                return;
            }

            var segment = buffer.ToString().Trim();
            if (segment.Length > 0)
            {
                sink.Add(segment);
            }
        }
    }

    /// <summary>
    /// Splits a single TypeScript parameter segment of the form
    /// <c>name[?]: type</c> into its (name, type) parts. Inner colons inside
    /// nested generics, function-type parameters, or object literals do not
    /// participate in the split.
    /// </summary>
    internal static bool TrySplitParameterNameAndType(
        string parameter,
        out string name,
        out string type)
    {
        var brackets = 0;
        var angles = 0;

        for (var i = 0; i < parameter.Length; i++)
        {
            var c = parameter[i];
            switch (c)
            {
                case '(':
                case '[':
                case '{':
                    brackets++;
                    break;

                case ')':
                case ']':
                case '}':
                    if (brackets > 0)
                    {
                        brackets--;
                    }
                    break;

                case '<':
                    angles++;
                    break;

                case '>':
                    if (angles > 0)
                    {
                        angles--;
                    }
                    break;

                case ':' when brackets == 0 && angles == 0:
                    name = parameter.Substring(0, i).Trim();
                    type = parameter.Substring(i + 1).Trim();
                    return true;
            }
        }

        name = string.Empty;
        type = string.Empty;
        return false;
    }

    internal static bool IsAction(
        string line, out Match? match)
    {
        match = TypeScriptCallbackRegex.Match(line);
        return match.Success;
    }

    internal static bool IsMethod(
        string line, out Match? match)
    {
        match = TypeScriptMethodRegex.Match(line);
        var isSuccess = match.Success;
        if (isSuccess)
        {
            var methodName = match.GetGroupValue("MethodName");
            if (IsEventListenerMethod(methodName))
            {
                return false;
            }

            var returnType = match.GetGroupValue("ReturnType");
            // `Contains(string)` is culture-sensitive on netstandard2.0
            // (no `StringComparison` overload until .NET Core 2.1).
            // Use `IndexOf(string, StringComparison.Ordinal)` instead so
            // the comparison is always ordinal -- the analyzer process
            // inherits the host machine's culture and the same Turkish-
            // locale class of bug that motivated the broader culture-
            // invariant audit pass would otherwise apply.
            if (returnType is not null && returnType.IndexOf("this", StringComparison.Ordinal) >= 0)
            {
                return false;
            }
        }

        return isSuccess;
    }

    private static bool IsEventListenerMethod(string? methodName)
    {
        if (string.IsNullOrEmpty(methodName))
        {
            return false;
        }

        var bareName = methodName!;
        var genericStart = bareName.IndexOf('<');
        if (genericStart >= 0)
        {
            bareName = bareName.Substring(0, genericStart);
        }

        return bareName is "addEventListener" or "removeEventListener";
    }

    internal static bool IsProperty(
        string line,
        out Match? match)
    {
        match = TypeScriptPropertyRegex.Match(line);
        var isSuccess = match.Success;
        if (isSuccess)
        {
            var name = match.GetGroupValue("Name");
            if ((name is "addEventListener" or "removeEventListener") ||
                (name is not null && name.IndexOf("this", StringComparison.Ordinal) >= 0))
            {
                return false;
            }

            // Arrow-function-typed properties (callback handlers, e.g.
            // `pull?: (controller: ReadableStreamDefaultController) => void;`
            // or `onabort: ((this: AbortSignal, ev: Event) => any) | null;`)
            // misparse because the property regex's `Name` capture is
            // greedy and walks to the *last* `:` in the line - including
            // the one inside the parameter list. That yields a bogus
            // identifier like "pull?: (controller". Skip these lines:
            // Blazor JS-interop cannot round-trip callback handler
            // properties anyway (would require DotNetObjectReference
            // wiring the generator does not emit for properties), and
            // the bogus name is never a legal C# identifier.
            var type = match.GetGroupValue("Type");
            if ((name is not null && name.IndexOf('(') >= 0) ||
                (type is not null && type.IndexOf("=>", StringComparison.Ordinal) >= 0))
            {
                return false;
            }

            if (match.GetGroupValue("Type") is "void")
            {
                return false;
            }
        }

        return isSuccess;
    }

    /// <summary>
    /// Parses the comma-separated list of base interfaces from an
    /// interface header line. Returns each identifier with surrounding
    /// whitespace trimmed and the special <c>EventTarget</c> base
    /// dropped, since DOM event-listener machinery is intentionally not
    /// projected through JS interop.
    /// </summary>
    /// <remarks>
    /// The split is depth-aware on <c>&lt;</c>/<c>&gt;</c> so a generic
    /// base like <c>Map&lt;K, V&gt;</c> is treated as a single token.
    /// Examples:
    /// <code>
    /// "interface X extends A, B, C {"               => [A, B, C]
    /// "interface X extends EventTarget {"           => []
    /// "interface X extends Map&lt;K, V&gt;, Y {"    => [Map&lt;K, V&gt;, Y]
    /// "interface X {"                               => []
    /// </code>
    /// </remarks>
    private static List<string> ExtractExtendsBases(string interfaceHeaderSegment)
    {
        const string ExtendsKeyword = " extends ";
        var bases = new List<string>();

        var idx = interfaceHeaderSegment.IndexOf(
            ExtendsKeyword, StringComparison.Ordinal);
        if (idx < 0)
        {
            return bases;
        }

        // Take from end of "extends " up to the opening brace (or end of segment).
        var start = idx + ExtendsKeyword.Length;
        var brace = interfaceHeaderSegment.IndexOf('{', start);
        var end = brace >= 0 ? brace : interfaceHeaderSegment.Length;
        var clause = interfaceHeaderSegment.Substring(start, end - start);

        // Depth-aware split on `,` -- commas inside a generic type
        // argument list (e.g. `Map<K, V>`) must not separate bases.
        var depth = 0;
        var tokenStart = 0;
        for (var i = 0; i < clause.Length; i++)
        {
            var ch = clause[i];
            if (ch == '<')
            {
                depth++;
            }
            else if (ch == '>')
            {
                if (depth > 0)
                {
                    depth--;
                }
            }
            else if (ch == ',' && depth == 0)
            {
                AddBase(bases, clause.Substring(tokenStart, i - tokenStart));
                tokenStart = i + 1;
            }
        }

        AddBase(bases, clause.Substring(tokenStart, clause.Length - tokenStart));
        return bases;

        static void AddBase(List<string> sink, string token)
        {
            var trimmed = token.Trim();
            if (trimmed.Length == 0)
            {
                return;
            }

            // EventTarget contributes the DOM event-listener surface,
            // which the existing single-extends path also stripped.
            // Preserve that behavior for multi-extends.
            if (string.Equals(trimmed, "EventTarget", StringComparison.Ordinal))
            {
                return;
            }

            sink.Add(trimmed);
        }
    }

    /// <summary>
    /// Parses <paramref name="baseTypeName"/> via <c>ToObject</c>
    /// and copies its members and dependent types into
    /// <paramref name="derived"/>. Dictionary writes are unconditional,
    /// so callers must invoke this <em>before</em> walking the derived
    /// type's own member lines (which will then overwrite the base on
    /// any conflict and give the expected "derived wins" shadowing).
    /// </summary>
    /// <remarks>
    /// When the base declaration cannot be resolved (e.g. it lives in a
    /// type-declaration source we don't carry) the merge is silently
    /// skipped. The derived type still parses with its own members; the
    /// only loss is inherited surface. This matches the pre-existing
    /// behavior for unresolved single-extends bases.
    /// </remarks>
    private void MergeBaseMembers(
        CSharpObject derived,
        string baseTypeName,
        HashSet<string> visiting,
        Dictionary<string, CSharpObject> memo)
    {
        if (!_reader.TryGetDeclaration(baseTypeName, out var baseText)
            || string.IsNullOrWhiteSpace(baseText))
        {
            return;
        }

        var baseObj = ToObject(baseText!, visiting, memo);
        if (baseObj is null)
        {
            return;
        }

        foreach (var kvp in baseObj.Properties)
        {
            derived.Properties[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in baseObj.Methods)
        {
            derived.Methods[kvp.Key] = kvp.Value;
        }

        if (baseObj.DependentTypes is { Count: > 0 })
        {
            foreach (var kvp in baseObj.DependentTypes)
            {
                derived.DependentTypes![kvp.Key] = kvp.Value;
            }
        }
    }
}
