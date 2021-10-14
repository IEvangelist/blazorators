// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;
using TypeScript.TypeConverter.CSharp;
using TypeScript.TypeConverter.Extensions;
using TypeScript.TypeConverter.JavaScript;
using TypeScript.TypeConverter.Types;

namespace TypeScript.TypeConverter.Converters;

class InterfaceConverter
{
    private readonly Regex _interfaceTypeName = new("(?:interface )(?'TypeName'\\S+)");
    private readonly Regex _extendsTypeName = new("(?:extends )(?'TypeName'\\S+)");

    /// <summary>
    /// Given a string value of <c>"clearWatch(watchId: number): void;"</c>, the
    /// following capture groups would be present:
    /// <list type="bullet">
    /// <item><c>MethodName</c>: <c>"clearWatch"</c></item>
    /// <item><c>Parameters</c>: <c>"(watchId: number)"</c></item>
    /// <item><c>ReturnType</c>: <c>": void;"</c></item>
    /// </list>
    /// </summary>
    private readonly Regex _typeScriptMethod =
        new(@"^(?'MethodName'\S+(?=\())(?'Parameters'.*\))(?'ReturnType'\:.*)$", RegexOptions.Multiline);

    internal CSharpObject? ToObject(string? typeScriptTypeDeclaration)
    {
        CSharpObject? cSharpObject = null;

        var lineTokens = new StringTokenizer(typeScriptTypeDeclaration, new[] { '\n' });
        foreach (var (index, segment) in lineTokens.Select((s, i) => (i, s)))
        {
            if (index == 0)
            {
                var typeName = _interfaceTypeName.GetMatchGroupValue(segment.Value, "TypeName");
                var subclass = _extendsTypeName.GetMatchGroupValue(segment.Value, "TypeName");
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
                var typeName = _interfaceTypeName.GetMatchGroupValue(segment.Value, "TypeName");
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

    internal static (List<CSharpType> Parameters, JavaScriptMethod? JavaScriptMethod) ParseParameters(
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
            if (!TypeMap.PrimitiveTypes.IsPrimitiveType(parameterType))
            {
                // TODO:
                //appendDependentType?.Invoke();
            }

            parameters.Add(new(parameterName, parameterType, isNullable));
        }

        return (parameters, null);
    }

    internal string ToCSharpSourceText(string typeScriptInterfaceDefinition)
    {
        /*

        NOTES:

        There are several kinds of possible TypeScript type definitions we should try to handle.

        For example:

        interface Geolocation {
            clearWatch(watchId: number): void;
            getCurrentPosition(
                successCallback: PositionCallback,
                errorCallback?: PositionErrorCallback | null,
                options?: PositionOptions): void;
            watchPosition(
                successCallback: PositionCallback,
                errorCallback?: PositionErrorCallback | null,
                options?: PositionOptions): number;
        }

        This interface defines three methods. The only "pure" method is the `clearWatch`. It's considered "pure"
        because it doesn't require any additional types, and can be called directly from JavaScript interop.

        The `getCurrentPosition` on the other hand, is a bit more involved. It defines callbacks. In order for .NET objects
        to satisfy JavaScript callbacks, we need an object reference and the corresponding `JSInvokable` method / method name.

        A bit of JavaScript would also have to be generated from this.
         */

        CSharpObject? csharpObject = null;

        var lineTokens = new StringTokenizer(typeScriptInterfaceDefinition, new[] { '\n' });
        foreach (var (index, segment) in lineTokens.Select((s, i) => (i, s)))
        {
            if (index == 0)
            {
                var typeName = _interfaceTypeName.GetMatchGroupValue(segment.Value, "TypeName");
                var subclass = _extendsTypeName.GetMatchGroupValue(segment.Value, "TypeName");

                if (typeName is not null)
                {
                    csharpObject = new(typeName, subclass);
                    continue;
                }
                else
                {
                    break;
                }
            }

            if (csharpObject is not null)
            {
                var line = segment.Value.Trim();
                if (line is { Length: > 3 })
                {
                    var memberLineTokenizer = new StringTokenizer(line, new[] { ':', ' ', ';' });
                    var memberDefinition = memberLineTokenizer.Where(t => t.Length > 0).ToArray();
                    if (memberDefinition is { Length: 2 })
                    {
                        var memberName = memberDefinition[0];
                        var isNullable = memberName.Value.EndsWith('?');
                        var memberType = memberDefinition[1];

                        CSharpProperty member = new(memberName.Value, memberType.Value, isNullable);
                        csharpObject.Properties[isNullable ? memberName.Value[0..^1] : memberName.Value] = member;
                    }
                }
            }
        }

        return csharpObject is not null
            ? csharpObject.ToString()
            : throw new ApplicationException("Unable to parse TypeScript type definition.");
    }

    internal bool IsMethod(
        string line, [NotNullWhen(true)] out Match? match)
    {
        match = _typeScriptMethod.Match(line);
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
                var memberName = memberDefinition[0];
                var isNullable = memberName.Value.EndsWith('?');
                var memberType = memberDefinition[1];

                property = (memberName.Value, isNullable, memberType.Value);
                return true;
            }
        }

        property = null!;
        return false;
    }
}
