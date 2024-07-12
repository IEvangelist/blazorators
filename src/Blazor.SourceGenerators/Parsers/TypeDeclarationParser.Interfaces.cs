// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript.Types;

namespace Blazor.SourceGenerators.Parsers;

internal sealed partial class TypeDeclarationParser
{
    internal CSharpObject? ToObject(string typeScriptTypeDeclaration)
    {
        CSharpObject? cSharpObject = null;

        var lineTokens = typeScriptTypeDeclaration.Split('\n');
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
                    cSharpObject = new(typeName, subclass);
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
                var isNullable = name.EndsWith("?") || type.Contains("| null");

                name = name.Replace("?", "").Replace("readonly ", "");
                type = TryGetPrimitiveType(type);

                CSharpProperty cSharpProperty = new(name, type, isNullable, isReadonly);
                cSharpObject.Properties[cSharpProperty.RawName] = cSharpProperty;

                var mappedType = cSharpProperty.MappedTypeName;

                // When a property defines a custom type, that type needs to also be parsed
                // and source generated. This is so that dependent types are known and resolved.
                if (!TypeMap.PrimitiveTypes.IsPrimitiveType(mappedType) &&
                    _reader.TryGetDeclaration(mappedType, out string? typeScriptDefinitionText) &&
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

    internal CSharpTopLevelObject? ToTopLevelObject(InterfaceDeclaration typeScriptTypeDeclaration)
    {
        var topLevelObject = new CSharpTopLevelObject(typeScriptTypeDeclaration.Identifier);

        var methods = typeScriptTypeDeclaration.OfKind(TypeScriptSyntaxKind.MethodSignature);
        foreach (var method in methods)
        {
            var methodName = method.Identifier;
            var parameters = method.OfKind(TypeScriptSyntaxKind.Parameter);
            var returnType = method.OfKind(TypeScriptSyntaxKind.TypeReference).FirstOrDefault();


        }

        return topLevelObject;
    }

    internal CSharpTopLevelObject? ToTopLevelObject(string typeScriptTypeDeclaration)
    {
        CSharpTopLevelObject? topLevelObject = null;

        var lineTokens = typeScriptTypeDeclaration.Split('\n');
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
                var isNullable = name.EndsWith("?") || type.Contains("| null");

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
                    _reader.TryGetDeclaration(mappedType, out string? typeScriptDefinitionText) &&
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
        if (!TypeMap.PrimitiveTypes.IsPrimitiveType(type) &&
            _reader.TryGetTypeAlias(type, out var typeAliasLine) &&
            typeAliasLine is not null && typeAliasLine.Replace(";", "").Split('=')
                is { Length: 2 } split && split[1].Trim().Split('|')
                is { Length: > 0 } values &&
                values.Select(v => v.Trim()).ToList()
                is { Count: > 0 } list)
        {
            var isStringAlias = list.TrueForAll(v => v.StartsWith("\"") && v.EndsWith("\""));
            if (isStringAlias)
            {
                type = "string";
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
        var methodReturnType = CleanseReturnType(returnType);
        CSharpMethod cSharpMethod =
            new(methodName,
            methodReturnType,
            parameterDefinitions,
            javaScriptMethod);

        var nonArrayMethodReturnType = methodReturnType.Replace("[]", "");
        if (!TypeMap.PrimitiveTypes.IsPrimitiveType(nonArrayMethodReturnType) &&
            _reader.TryGetDeclaration(nonArrayMethodReturnType, out string? typeScriptDefinitionText) &&
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

        var lineTokens = typeScriptTypeDeclaration.Split('\n');
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

    internal static string CleanseReturnType(string returnType) =>
        // Example inputs:
        // 1) ": void;"
        // 2) ": string | null;"
        returnType.Replace(":", "").Replace(";", "").Trim();

    internal (List<CSharpType> Parameters, JavaScriptMethod? JavaScriptMethod) ParseParameters(
        string typeName,
        string rawName,
        string parametersString,
        Action<CSharpObject> appendDependentType)
    {
        List<CSharpType> parameters = [];

        // Example input:
        // "(someCallback: CallbackType, someId?: number | null)"
        var trimmedParameters = parametersString.Replace("(", "").Replace(")", "");
        var parameterLineTokenizer = trimmedParameters.Split(':', ',');

        JavaScriptMethod? javaScriptMethod = new(rawName);
        foreach (var parameterPair in parameterLineTokenizer.Where(t => t.Length > 0).Chunk(2))
        {
            var isNullable = parameterPair[0].EndsWith("?");
            var parameterName = parameterPair[0].Replace("?", "").Trim();
            var parameterType = isNullable
                ? parameterPair[1].Trim().Replace(" | null", "")
                : parameterPair[1].Trim();

            CSharpAction? action = null;

            // When a parameter defines a custom type, that type needs to also be parsed
            // and source generated. This is so that dependent types are known and resolved.
            if (!TypeMap.PrimitiveTypes.IsPrimitiveType(parameterType) &&
                _reader.TryGetDeclaration(parameterType, out string? typeScriptDefinitionText) &&
                typeScriptDefinitionText is not null)
            {
                javaScriptMethod = javaScriptMethod with
                {
                    InvokableMethodName = $"blazorators.{typeName.LowerCaseFirstLetter()}.{rawName}"
                };

                if (parameterType.EndsWith("Callback"))
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
            if (methodName is "addEventListener" or "removeEventListener")
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

    internal static bool IsProperty(
        string line,
        out Match? match)
    {
        match = TypeScriptPropertyRegex.Match(line);
        var isSuccess = match.Success;
        if (isSuccess)
        {
            var name = match.GetGroupValue("Name");
            if (name is "addEventListener" or "removeEventListener" ||
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

static class EnumerableExtensions
{
    internal static IEnumerable<T[]> Chunk<T>(this IEnumerable<T> source, int chunksize)
    {
        while (source.Any())
        {
            yield return source.Take(chunksize).ToArray();
            source = source.Skip(chunksize);
        }
    }
}
