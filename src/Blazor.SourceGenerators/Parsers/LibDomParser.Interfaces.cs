// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Blazor.SourceGenerators.CSharp;
using Blazor.SourceGenerators.Extensions;
using Blazor.SourceGenerators.JavaScript;
using Blazor.SourceGenerators.Types;
using static Blazor.SourceGenerators.Expressions.SharedRegex;

namespace Blazor.SourceGenerators.Parsers
{
    public partial class LibDomParser
    {
        internal CSharpObject? ToObject(string typeScriptTypeDeclaration)
        {
            CSharpObject? cSharpObject = null;

            var lineTokens = typeScriptTypeDeclaration.Split(new[] { '\n' });
            foreach (var (index, segment) in lineTokens.Select((s, i) => (i, s)))
            {
                if (index == 0)
                {
                    var typeName = InterfaceTypeNameRegex.GetMatchGroupValue(segment, "TypeName");
                    var subclass = ExtendsTypeNameRegex.GetMatchGroupValue(segment, "TypeName");
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
                            methodName,
                            parameters,
                            obj => cSharpObject.DependentTypes![obj.TypeName] = obj);

                    CSharpMethod cSharpMethod =
                        new(methodName, CleanseReturnType(returnType), parameterDefinitions, javaScriptMethod);

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
                    var isNullable = name.EndsWith("?");

                    name = name.Replace("?", "").Replace("readonly ", "");

                    CSharpProperty cSharpProperty = new(name, type, isNullable, isReadonly);
                    cSharpObject.Properties[cSharpProperty.RawName] = cSharpProperty;

                    continue;
                }
            }

            return cSharpObject;
        }

        internal CSharpExtensionObject? ToExtensionObject(string typeScriptTypeDeclaration)
        {
            CSharpExtensionObject? extensionObject = null;

            var lineTokens = typeScriptTypeDeclaration.Split(new[] { '\n' });
            foreach (var (index, segment) in lineTokens.Select((s, i) => (i, s)))
            {
                if (index == 0)
                {
                    var typeName = InterfaceTypeNameRegex.GetMatchGroupValue(segment, "TypeName");
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
                            methodName,
                            parameters,
                            obj => extensionObject.DependentTypes![obj.TypeName] = obj);

                    CSharpMethod cSharpMethod =
                        new(methodName, CleanseReturnType(returnType), parameterDefinitions, javaScriptMethod);

                    extensionObject.Methods!.Add(cSharpMethod);

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
                    var isNullable = name.EndsWith("?");

                    name = name.Replace("?", "").Replace("readonly ", "");

                    CSharpProperty cSharpProperty = new(name, type, isNullable, isReadonly);
                    extensionObject.Properties!.Add(cSharpProperty);

                    continue;
                }
            }

            return extensionObject;
        }

        internal CSharpAction? ToAction(string typeScriptTypeDeclaration)
        {
            CSharpAction? cSharpAction = null;

            var lineTokens = typeScriptTypeDeclaration.Split(new[] { '\n' });
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

            //var isNullable = false;
            //if (returnType.Contains(" | null"))
            //{
            //    isNullable = true;
            //    returnType = returnType.Replace(" | null", "");
            //}

            //var cleansedType = returnType.Replace(":", "").Replace(";", "").Trim();
            //return $"{cleansedType}{(isNullable ? "?" : "")}";
        }

        internal (List<CSharpType> Parameters, JavaScriptMethod? JavaScriptMethod) ParseParameters(
            string rawName,
            string parametersString,
            Action<CSharpObject> appendDependentType)
        {
            List<CSharpType> parameters = new();

            // Example input:
            // "(someCallback: CallbackType, someId?: number | null)"
            var trimmedParameters = parametersString.Replace("(", "").Replace(")", "");
            var parameterLineTokenizer = trimmedParameters.Split(new[] { ':', ',', });

            JavaScriptMethod? javaScriptMethod = new(rawName);
            foreach (var parameterPair in parameterLineTokenizer.Where(t => t.Length > 0).Chunk(2))
            {
                var parameterName = parameterPair[0].Replace("?", "").Trim();
                var isNullable = parameterPair[0].EndsWith("?");
                var parameterType = isNullable
                    ? parameterPair[1].Trim().Replace(" | null", "")
                    : parameterPair[1].Trim();

                CSharpAction? action = null;

                // When a parameter defines a custom type, that type needs to also be parsed
                // and source generated. This is so that dependent types are known / resolved.
                if (!TypeMap.PrimitiveTypes.IsPrimitiveType(parameterType) &&
                    _reader.TryGetDeclaration(parameterType, out var typeScriptDefinitionText) &&
                    typeScriptDefinitionText is not null)
                {
                    javaScriptMethod = javaScriptMethod with
                    {
                        InvokableMethodName = $"blazorators.{rawName}"
                    };

                    if (parameterType.EndsWith("Callback"))
                    {
                        action = ToAction(typeScriptDefinitionText);
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
            return match.Success;
        }

        internal static bool IsProperty(
            string line,
            out Match? match)
        {
            match = TypeScriptPropertyRegex.Match(line);
            return match.Success;
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
}