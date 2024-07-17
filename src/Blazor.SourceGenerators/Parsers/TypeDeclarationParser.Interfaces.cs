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

    internal CSharpObject ToObject(DeclarationStatement declaration)
    {
        var heritage = declaration is InterfaceDeclaration @interface
            ? @interface.HeritageClauses?
                .SelectMany(heritage => heritage.Types)
                .Where(type => type.Identifier is not "EventTarget")
                .Select(type => type.Identifier)
                .ToArray()
            : [];

        var subclass = heritage is null || heritage.Length == 0 ? "" : string.Join(", ", heritage);

        var csharpObject = new CSharpObject(declaration.Identifier, subclass);

        var objectMethods = declaration.OfKind(TypeScriptSyntaxKind.MethodSignature);
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

        var objectProperties = declaration.OfKind(TypeScriptSyntaxKind.PropertySignature);
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
        if (TryGetCustomType(typeName, out var declaration))
        {
            return ToTopLevelObject(declaration);
        }

        return default!;
    }

    internal CSharpTopLevelObject ToTopLevelObject(DeclarationStatement declaration)
    {
        var csharpTopLevelObject = new CSharpTopLevelObject(declaration.Identifier);

        var objectMethods = declaration.OfKind(TypeScriptSyntaxKind.MethodSignature);
        var methods = ParseMethods(
            csharpTopLevelObject.RawTypeName,
            objectMethods,
            (dependency) => csharpTopLevelObject.DependentTypes[dependency.TypeName] = dependency);

        csharpTopLevelObject.Methods.AddRange(methods);

        var objectProperties = declaration.OfKind(TypeScriptSyntaxKind.PropertySignature);
        var properties = ParseProperties(
            objectProperties,
            (dependency) => csharpTopLevelObject.DependentTypes[dependency.TypeName] = dependency);

        csharpTopLevelObject.Properties.AddRange(properties);

        return csharpTopLevelObject;
    }

    private static string GetNodeText(INode propertyTypeNode)
    {
        return propertyTypeNode.GetText().ToString().Trim();
    }

    private IEnumerable<CSharpMethod> ParseMethods(string rawTypeName, IEnumerable<Node> objectMethods, Action<CSharpObject> appendDependency)
    {
        ICollection<CSharpMethod> methods = [];
        foreach (var method in objectMethods.Cast<MethodSignature>())
        {
            var methodName = method.Identifier;
            var methodParameters = method.Parameters;
            var methodReturnType = GetNodeText(method.Type);

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

            var parameterType = GetNodeText(parameter.Children[parameter.Children.Count - 1]);
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

            var propertyTypeNode = property.Children[property.Children.Count - 1];

            // TODO: Handle other type of nodes correctly
            // Examples:
            // -    SomeCustomType | null                   #UnionNodeType
            // -    ((this: SomeCustom, ev: Event) => any)  #ParenthesizedTypeNode, inside #FunctionTypeNode

            var propertyType = propertyTypeNode switch
            {
                _ when isNullable => GetNodeText(propertyTypeNode).Replace(" | null", ""),
                _ => GetNodeText(propertyTypeNode)
            };

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

    private CSharpAction ToAction(DeclarationStatement declaration)
    {
        var csharpAction = new CSharpAction(declaration.Identifier);

        var callSignatureDeclaration = declaration.OfKind(TypeScriptSyntaxKind.CallSignature).FirstOrDefault() as CallSignatureDeclaration;

        if (callSignatureDeclaration is not null)
        {
            var actionParameters = callSignatureDeclaration.Parameters;
            var actionReturnType = GetNodeText(callSignatureDeclaration.Type);

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

        if (TryGetCustomType(nonArrayMethodReturnType, out var declaration))
        {
            var csharpObject = ToObject(declaration);
            if (csharpObject is not null)
            {
                csharpMethod.DependentTypes[nonArrayMethodReturnType] = csharpObject;
            }
        }

        return csharpMethod;
    }

    private bool TryGetCustomType(string typeName, out DeclarationStatement declaration)
    {
        declaration = default!;

        if (Primitives.IsPrimitiveType(typeName)) return false;

        var success = _reader.TryGetInterface(typeName, out var @interface) && @interface is not null;
        if (success)
        {
            declaration = @interface!;
            return true;
        }

        // TODO: Add typealiases as type to find inside the dependencies
        //success = _reader.TryGetTypeAlias(typeName, out var @type) && @type is not null;
        //if (success)
        //{
        //    declaration = @type!;
        //    return true;
        //}

        return false;
    }
}