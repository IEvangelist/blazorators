// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;
using TypeScript.TypeConverter.CSharp;
using TypeScript.TypeConverter.Extensions;
using TypeScript.TypeConverter.JavaScript;
using TypeScript.TypeConverter.Types;
using static TypeScript.TypeConverter.Expressions.SharedRegex;

namespace TypeScript.TypeConverter.Parsers;

public partial class LibDomParser
{
    internal CSharpObject? ToObject(string? typeScriptTypeDeclaration)
    {
        CSharpObject? cSharpObject = null;

        var lineTokens = new StringTokenizer(typeScriptTypeDeclaration, new[] { '\n' });
        foreach (var (index, segment) in lineTokens.Select((s, i) => (i, s)))
        {
            if (index == 0)
            {
                var typeName = InterfaceTypeNameRegex.GetMatchGroupValue(segment.Value, "TypeName");
                var subclass = ExtendsTypeNameRegex.GetMatchGroupValue(segment.Value, "TypeName");
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

            var line = segment.Value.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line == "}")
            {
                // We're done
                break;
            }

            if (IsMethod(line, out var method))
            {
                var methodName = method.GetGroupValue("MethodName");
                var parameters = method.GetGroupValue("Parameters");
                var returnType = method.GetGroupValue("ReturnType");

                if (methodName is null || parameters is null || returnType is null)
                {
                    continue;
                }

                var (parameterDefinitions, javaScriptMethod) =
                    ParseParameters(parameters, obj => cSharpObject.DependentTypes!.Add(obj));

                CSharpMethod cSharpMethod =
                    new(methodName, CleanseReturnType(returnType), parameterDefinitions, javaScriptMethod);

                cSharpObject.Methods[cSharpMethod.RawName] = cSharpMethod;

                continue;
            }

            if (IsProperty(line, out var property))
            {
                var (name, isNullable, type) = property.Value;
                CSharpProperty cSharpProperty = new(name, type, isNullable);
                cSharpObject.Properties[cSharpProperty.RawName] = cSharpProperty;

                continue;
            }
        }

        return cSharpObject;
    }

    internal CSharpExtensionObject? ToExtensionObject(string? typeScriptTypeDeclaration)
    {
        CSharpExtensionObject? extensionObject = null;

        var lineTokens = new StringTokenizer(typeScriptTypeDeclaration, new[] { '\n' });
        foreach (var (index, segment) in lineTokens.Select((s, i) => (i, s)))
        {
            if (index == 0)
            {
                var typeName = InterfaceTypeNameRegex.GetMatchGroupValue(segment.Value, "TypeName");
                if (typeName is not null)
                {
                    extensionObject = new(typeName);
                    continue;
                }
                else
                {
                    break;
                }
            }

            if (extensionObject is null)
            {
                break;
            }

            var line = segment.Value.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line == "}")
            {
                // We're done
                break;
            }

            if (IsMethod(line, out var method))
            {
                var methodName = method.GetGroupValue("MethodName");
                var parameters = method.GetGroupValue("Parameters");
                var returnType = method.GetGroupValue("ReturnType");

                if (methodName is null || parameters is null || returnType is null)
                {
                    continue;
                }

                var (parameterDefinitions, javaScriptMethod) =
                    ParseParameters(parameters, obj => extensionObject.DependentTypes!.Add(obj));

                CSharpMethod cSharpMethod =
                    new(methodName, CleanseReturnType(returnType), parameterDefinitions, javaScriptMethod);

                extensionObject.Methods!.Add(cSharpMethod);

                continue;
            }

            if (IsProperty(line, out var property))
            {
                var (name, isNullable, type) = property.Value;
                CSharpProperty cSharpProperty = new(name, type, isNullable);
                extensionObject.Properties!.Add(cSharpProperty);

                continue;
            }
        }

        return extensionObject;
    }

    internal static string CleanseReturnType(string returnType)
    {
        // Example input:
        // ": void;"
        return returnType.Replace(":", "").Replace(";", "").Trim();
    }

    internal (List<CSharpType> Parameters, JavaScriptMethod? JavaScriptMethod) ParseParameters(
        string parametersString, Action<CSharpObject> appendDependentType)
    {
        List<CSharpType> parameters = new();

        // Example input:
        // "(someCallback: CallbackType, someId?: number | null)"
        var trimmedParameters = parametersString.Replace("(", "").Replace(")", "");
        var parameterLineTokenizer = new StringTokenizer(trimmedParameters, new[] { ':', ',', });

        foreach (var parameterPair in parameterLineTokenizer.Where(t => t.Length > 0).Chunk(2))
        {
            var parameterName = parameterPair[0].Value.Replace("?", "");
            var isNullable = parameterPair[0].Value.EndsWith('?');
            var parameterType = parameterPair[1].Value;

            // When a parameter defines a custom type, that type needs to also be parsed
            // and source generated. This is so that dependent types are known / resolved.
            if (!TypeMap.PrimitiveTypes.IsPrimitiveType(parameterType) &&
                _reader.TryGetDeclaration(parameterType, out var typeScriptDefinitionText))
            {
                var obj = ToObject(typeScriptDefinitionText);
                if (obj is not null)
                {
                    appendDependentType(obj);
                }
            }

            parameters.Add(new(parameterName, parameterType, isNullable));
        }

        return (parameters, null);
    }

    internal bool IsMethod(
        string line, [NotNullWhen(true)] out Match? match)
    {
        match = TypeScriptMethodRegex.Match(line);
        return match.Success;
    }

    internal static bool IsProperty(
        string line,
        [NotNullWhen(true)] out (string Name, bool IsNullable, string ReturnType)? property)
    {
        // TODO: refactor avoiding brute force parsing.
        // See "IsMethod" functiom for inspiration.
        if (line is { Length: > 3 })
        {
            var memberLineTokenizer = new StringTokenizer(line, new[] { ':', ' ', ';' });
            var memberDefinition = memberLineTokenizer.Where(t => t.Length > 0).ToArray();
            if (memberDefinition is { Length: 2 })
            {
                var memberName = memberDefinition[0].Value.Replace("?", "");
                var isNullable = memberDefinition[0].Value.EndsWith('?');
                var memberType = memberDefinition[1].Value;

                property = (memberName, isNullable, memberType);
                return true;
            }
        }

        property = null!;
        return false;
    }
}
