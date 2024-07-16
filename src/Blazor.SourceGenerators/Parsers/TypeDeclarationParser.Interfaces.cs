// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript.Types;

namespace Blazor.SourceGenerators.Parsers;

internal sealed partial class TypeDeclarationParser
{
    internal CSharpObject ToObject(string typeName)
    {
        if (TryGetCustomType(typeName, out var typescriptInterface))
        {
            return ToObject(typescriptInterface);
        }

        return default!;
    }

    internal CSharpObject ToObject(InterfaceDeclaration typescriptInterface)
    {
        var heritage = typescriptInterface.HeritageClauses?
            .SelectMany(heritage => heritage.Types)
            .Where(type => type.Identifier is not "EventTarget")
            .Select(type => type.Identifier)
            .ToArray();

        var subclass = heritage is null || heritage.Length == 0 ? "" : string.Join(", ", heritage);

        var csharpObject = new CSharpObject(typescriptInterface.Identifier, subclass);

        var objectMethods = typescriptInterface.OfKind(TypeScriptSyntaxKind.MethodSignature);
        var methods = ParseMethods(
            csharpObject.TypeName,
            objectMethods,
            (dependency) => csharpObject.DependentTypes[dependency.TypeName] = dependency);

        csharpObject = csharpObject with
        {
            Methods = methods
                .GroupBy(method => method.RawName)
                .ToDictionary(method => method.Key, method => method.Last())
        };

        var objectProperties = typescriptInterface.OfKind(TypeScriptSyntaxKind.PropertySignature);
        var properties = ParseProperties(
            objectProperties,
            (dependency) => csharpObject.DependentTypes[dependency.TypeName] = dependency);

        csharpObject = csharpObject with
        {
            Properties = properties
                .GroupBy(property => property.RawName)
                .ToDictionary(property => property.Key, method => method.Last())
        };

        return csharpObject;
    }

    internal CSharpTopLevelObject ToTopLevelObject(string typeName)
    {
        if (TryGetCustomType(typeName, out var typescriptInterface))
        {
            return ToTopLevelObject(typescriptInterface);
        }

        return default!;
    }

    internal CSharpTopLevelObject ToTopLevelObject(InterfaceDeclaration typescriptInterface)
    {
        var csharpTopLevelObject = new CSharpTopLevelObject(typescriptInterface.Identifier);

        var objectMethods = typescriptInterface.OfKind(TypeScriptSyntaxKind.MethodSignature);
        var methods = ParseMethods(
            csharpTopLevelObject.RawTypeName,
            objectMethods,
            (dependency) => csharpTopLevelObject.DependentTypes[dependency.TypeName] = dependency);

        csharpTopLevelObject.Methods.AddRange(methods);

        var objectProperties = typescriptInterface.OfKind(TypeScriptSyntaxKind.PropertySignature);
        var properties = ParseProperties(
            objectProperties,
            (dependency) => csharpTopLevelObject.DependentTypes[dependency.TypeName] = dependency);

        csharpTopLevelObject.Properties.AddRange(properties);

        return csharpTopLevelObject;
    }

    private IEnumerable<CSharpMethod> ParseMethods(string rawTypeName, IEnumerable<Node> objectMethods, Action<CSharpObject> appendDependency)
    {
        ICollection<CSharpMethod> methods = [];
        foreach (var method in objectMethods.Cast<MethodSignature>())
        {
            var methodName = method.Identifier;
            var methodParameters = method.Parameters;
            var methodReturnType = method.Type.GetText().ToString().Trim();

            if (methodName is null || methodParameters is null || string.IsNullOrEmpty(methodReturnType))
            {
                continue;
            }

            var (csharpParameters, javascriptMethod) = ParseParameters(
                rawTypeName,
                methodName,
                methodParameters,
                appendDependency);

            var csharpMethod = ToMethod(methodName, methodReturnType, csharpParameters, javascriptMethod);

            methods.Add(csharpMethod);
        }

        return methods;
    }

    private (IList<CSharpType>, JavaScriptMethod) ParseParameters(string rawTypeName, string methodName, NodeArray<ParameterDeclaration> methodParameters, Action<CSharpObject> appendDependency)
    {
        IList<CSharpType> parameters = [];
        var javascriptMethod = new JavaScriptMethod(methodName);

        foreach (var parameter in methodParameters)
        {
            var isNullable = parameter.QuestionToken is not null;
            var parameterName = parameter.Identifier;

            var parameterType = parameter.Children[parameter.Children.Count - 1].GetText().ToString().Trim();
            parameterType = isNullable ? parameterType.Replace(" | null", "") : parameterType;

            CSharpAction csharpAction = null!;

            // When a parameter defines a custom type, that type needs to also be parsed
            // and source generated. This is so that dependent types are known and resolved.
            if (TryGetCustomType(parameterType, out var typescriptInterface))
            {
                javascriptMethod = javascriptMethod with
                {
                    InvokableMethodName = $"blazorators.{rawTypeName.LowerCaseFirstLetter()}.{methodName}"
                };

                if (parameterName.EndsWith("Callback"))
                {
                    csharpAction = ToAction(typescriptInterface);
                    javascriptMethod = javascriptMethod with
                    {
                        IsBiDirectionalJavaScript = true,
                    };
                }
                else
                {
                    var csharpObject = ToObject(typescriptInterface);
                    if (csharpObject is not null)
                    {
                        appendDependency.Invoke(csharpObject);
                    }
                }
            }

            parameters.Add(new CSharpType(parameterName, parameterType, isNullable, csharpAction));
        }

        return (parameters, javascriptMethod);
    }

    private IEnumerable<CSharpProperty> ParseProperties(IEnumerable<Node> objectProperties, Action<CSharpObject> appendDependency)
    {
        ICollection<CSharpProperty> properties = [];
        foreach (var property in objectProperties.Cast<PropertySignature>())
        {
            var isReadonly = property.Modifiers.Exists(modifier => modifier.Kind is TypeScriptSyntaxKind.ReadonlyKeyword);
            var isNullable = property.QuestionToken is not null;

            var propertyName = property.Identifier;
            var propertyType = property.Children[property.Children.Count - 1].GetText().ToString().Trim();
            propertyType = isNullable ? propertyType.Replace(" | null", "") : propertyType;

            if (propertyName is null || string.IsNullOrEmpty(propertyType))
            {
                continue;
            }

            var csharpProperty = new CSharpProperty(propertyName, propertyType, isNullable, isReadonly);
            properties.Add(csharpProperty);

            var mappedType = csharpProperty.MappedTypeName;

            // When a property defines a custom type, that type needs to also be parsed
            // and source generated. This is so that dependent types are known and resolved.
            if (TryGetCustomType(mappedType, out var typescriptInterface))
            {
                var csharpObject = ToObject(typescriptInterface);
                if (csharpObject is not null)
                {
                    appendDependency.Invoke(csharpObject);
                }
            }
        }

        return properties;
    }

    private CSharpAction ToAction(InterfaceDeclaration typescriptInterface)
    {
        var csharpAction = new CSharpAction(typescriptInterface.Identifier);

        var callSignatureDeclaration = typescriptInterface.OfKind(TypeScriptSyntaxKind.CallSignature).FirstOrDefault() as CallSignatureDeclaration;

        if (callSignatureDeclaration is not null)
        {
            var actionParameters = callSignatureDeclaration.Parameters;
            var actionReturnType = callSignatureDeclaration.Type.GetText().ToString().Trim();

            if (actionParameters is null || string.IsNullOrEmpty(actionReturnType))
            {
                return csharpAction;
            }

            var (csharpParameters, _) = ParseParameters(
                csharpAction.RawName,
                csharpAction.RawName,
                actionParameters,
                dependency => csharpAction.DependentTypes[dependency.TypeName] = dependency);

            csharpAction = csharpAction with
            {
                ParameterDefinitions = csharpParameters
            };
        }

        return csharpAction;
    }

    private CSharpMethod ToMethod(string methodName, string methodReturnType, IList<CSharpType> csharpParameters, JavaScriptMethod javascriptMethod)
    {
        var csharpMethod = new CSharpMethod(methodName, methodReturnType, csharpParameters, javascriptMethod);
        var nonGenericMethodReturnType = methodReturnType.ExtractGenericType();
        var nonArrayMethodReturnType = nonGenericMethodReturnType.Replace("[]", "");

        if (TryGetCustomType(nonArrayMethodReturnType, out var typescriptInterface))
        {
            var csharpObject = ToObject(typescriptInterface);
            if (csharpObject is not null)
            {
                csharpMethod.DependentTypes[nonArrayMethodReturnType] = csharpObject;
            }
        }

        return csharpMethod;
    }

    private bool TryGetCustomType(string typeName, out InterfaceDeclaration typescriptInterface)
    {
        typescriptInterface = default!;
        return !TypeMap.PrimitiveTypes.IsPrimitiveType(typeName) &&
            _reader.TryGetInterface(typeName, out typescriptInterface!) &&
            typescriptInterface is not null;
    }
}