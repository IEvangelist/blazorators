// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Parsers;

internal sealed partial class TypeDeclarationParser
{
    internal CSharpObject? ToObject(string typeScriptTypeDeclaration)
    {
        // Callback interfaces don't have parseable methods or properties in
        // the regex grammar (the body is an anonymous call signature). We
        // still emit a placeholder `CSharpObject` so downstream emission
        // can filter on `IsActionParameter`/`IsCallback`, but bail out of
        // the rest of the per-line parser.
        var isCallback = IsCallbackTypeDeclaration(typeScriptTypeDeclaration);

        CSharpObject? cSharpObject = null;

        var lineTokens = typeScriptTypeDeclaration.Split(['\n']);
        foreach (var (index, segment) in lineTokens.Select((s, i) => (i, s)))
        {
            if (index == 0)
            {
                var typeName = InterfaceTypeNameRegex.GetMatchGroupValue(segment, "TypeName");

                // Ignore event targets for now.
                var seg = segment.Replace(" extends EventTarget", "");
                var subclass = ExtendsTypeNameRegex.GetMatchGroupValue(seg, "TypeName");
                if (typeName is not null)
                {
                    cSharpObject = new(typeName, subclass) { IsCallback = isCallback };
                    if (isCallback)
                    {
                        return cSharpObject;
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
                        obj => cSharpObject.DependentTypes![obj.TypeName] = obj);


                var cSharpMethod =
                    ToMethod(methodName, returnType, parameterDefinitions, javaScriptMethod);
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

                var isReadonly = name.StartsWith("readonly ");
                var isNullable = name.EndsWith("?") || type.EndsWith("| null");

                name = name.Replace("?", "").Replace("readonly ", "");
                type = TryGetPrimitiveType(type);

                CSharpProperty cSharpProperty = new(name, type, isNullable, isReadonly);
                cSharpObject.Properties[cSharpProperty.RawName] = cSharpProperty;

                var mappedType = cSharpProperty.MappedTypeName;

                // When a property defines a custom type, that type needs to also be parsed
                // and source generated. This is so that dependent types are known and resolved.
                if (!TypeMap.PrimitiveTypes.IsPrimitiveType(mappedType) &&
                    _reader.TryGetDeclaration(mappedType, out var typeScriptDefinitionText) &&
                    typeScriptDefinitionText is not null)
                {
                    var obj = ToObject(typeScriptDefinitionText);
                    if (obj is not null)
                    {
                        cSharpObject.DependentTypes![obj.TypeName] = obj;
                    }
                }

                continue;
            }
        }

        return cSharpObject;
    }

    internal CSharpTopLevelObject? ToTopLevelObject(string typeScriptTypeDeclaration)
    {
        CSharpTopLevelObject? topLevelObject = null;

        var lineTokens = typeScriptTypeDeclaration.Split(['\n']);
        foreach (var (index, segment) in lineTokens.Select((s, i) => (i, s)))
        {
            if (index == 0)
            {
                var typeName = InterfaceTypeNameRegex.GetMatchGroupValue(segment, "TypeName");
                if (typeName is not null)
                {
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
                        obj => topLevelObject.DependentTypes![obj.TypeName] = obj);

                var cSharpMethod =
                    ToMethod(methodName, returnType, parameterDefinitions, javaScriptMethod);

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

                var isReadonly = name.StartsWith("readonly ");
                var isNullable = name.EndsWith("?") || type.EndsWith("| null");

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
                    _reader.TryGetDeclaration(mappedType, out var typeScriptDefinitionText) &&
                    typeScriptDefinitionText is not null)
                {
                    var obj = ToObject(typeScriptDefinitionText);
                    if (obj is not null)
                    {
                        topLevelObject.DependentTypes![obj.TypeName] = obj;
                    }
                }

                continue;
            }
        }

        return topLevelObject;
    }

    private string TryGetPrimitiveType(string type)
    {
        if (TypeMap.PrimitiveTypes.IsPrimitiveType(type))
        {
            return type;
        }

        // Carry the `| null` clause across the alias lookup. The alias map
        // is keyed by the bare identifier (e.g. `DOMHighResTimeStamp`),
        // never `DOMHighResTimeStamp | null`, so without this strip the
        // resolution missed any property/parameter whose alias type also
        // tolerated `null`.
        var hadNullClause = type.EndsWith(" | null", StringComparison.Ordinal);
        var bareType = hadNullClause
            ? type.Substring(0, type.Length - " | null".Length).Trim()
            : type;

        if (TypeMap.PrimitiveTypes.IsPrimitiveType(bareType))
        {
            return type;
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
            var isStringAlias = list.All(v => v.StartsWith("\"") && v.EndsWith("\""));
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
        JavaScriptMethod? javaScriptMethod)
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

        // Resolve the element type for dependent-DTO emission. Previously
        // the code did a textual `Replace("[]", "")` which only handled
        // the `T[]` shape (missing `Array<T>` and `ReadonlyArray<T>`) and
        // would also strip a `[]` from inside nested generic arguments.
        // Route through `TypeShape` so all three array forms agree with
        // the parameter side and `CSharpProperty.MappedTypeName`.
        var nonArrayMethodReturnType =
            TypeShape.TryGetArrayElementTypeName(methodReturnType, out var elementTypeName)
                ? elementTypeName
                : methodReturnType;

        if (!TypeMap.PrimitiveTypes.IsPrimitiveType(nonArrayMethodReturnType) &&
            _reader.TryGetDeclaration(nonArrayMethodReturnType, out var typeScriptDefinitionText) &&
            typeScriptDefinitionText is not null)
        {
            var dependentType = ToObject(typeScriptDefinitionText);
            if (dependentType is not null)
            {
                cSharpMethod.DependentTypes![nonArrayMethodReturnType] = dependentType;
            }
        }

        return cSharpMethod;
    }

    internal CSharpAction? ToAction(string typeScriptTypeDeclaration)
    {
        CSharpAction? cSharpAction = null;

        var lineTokens = typeScriptTypeDeclaration.Split(['\n']);
        foreach (var (index, segment) in lineTokens.Select((s, i) => (i, s)))
        {
            if (index == 0)
            {
                var typeName = InterfaceTypeNameRegex.GetMatchGroupValue(segment, "TypeName");
                if (typeName is not null)
                {
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
                        obj => cSharpAction.DependentTypes![obj.TypeName] = obj);

                cSharpAction = cSharpAction with
                {
                    ParameterDefinitions = parameterDefinitions
                };

                continue;
            }
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
        Action<CSharpObject> appendDependentType)
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
            // (`x?: T`) OR the type has a ` | null` clause (`x: T | null`).
            // We normalize both forms onto `isNullable=true` and strip the
            // `| null` from the type so the downstream primitive/declaration
            // lookups see a clean `T`. Previously only primitives whose
            // exact `"T | null"` key existed in `TypeMap` survived this -
            // e.g. `string | null` worked because the map had an entry,
            // but `string[] | null` (or any custom interface) emitted
            // `string[] | null x` verbatim, which isn't valid C#.
            if (trimmedType.EndsWith(" | null", StringComparison.Ordinal))
            {
                isNullable = true;
                trimmedType = trimmedType
                    .Substring(0, trimmedType.Length - " | null".Length)
                    .Trim();
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
                _reader.TryGetDeclaration(declarationLookupType, out var typeScriptDefinitionText) &&
                typeScriptDefinitionText is not null)
            {
                javaScriptMethod = javaScriptMethod with
                {
                    InvokableMethodName = $"blazorators.{typeName.LowerCaseFirstLetter()}.{rawName}"
                };

                if (IsCallbackTypeDeclaration(typeScriptDefinitionText))
                {
                    action = ToAction(typeScriptDefinitionText);
                    javaScriptMethod = javaScriptMethod with
                    {
                        IsBiDirectionalJavaScript = true
                    };
                }
                else
                {
                    var obj = ToObject(typeScriptDefinitionText);
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
            if (returnType?.Contains("this") ?? false)
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
                (name is not null && name.Contains("this")))
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
}
