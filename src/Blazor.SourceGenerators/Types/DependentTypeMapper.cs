// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript;
using Blazor.SourceGenerators.TypeScript.Types;

namespace Blazor.SourceGenerators.Types;

internal static class DependentTypeMapper
{
    private static readonly Lazy<ITypeScriptAbstractSyntaxTree> _defaultAst = new(() =>
    {
        var reader = TypeDeclarationReader.Default;
        return TypeScriptAbstractSyntaxTree.FromSourceText(reader.RawSourceText);
    });

    /// <summary>
    /// Gets a <see cref="Dictionary{TKey, TValue}"/> where the <c>TKey</c> is a
    /// <see cref="string"/> and the <c>TValue</c> is a <see cref="Node"/>.
    /// </summary>
    /// <param name="interfaceName">The name of the <c>Name</c> in the TypeScript
    /// _.d.ts_ file's <c>interface name { ... }</c> definition.</param>
    /// <returns>A representation of all the dependent types as a
    /// <see cref="Dictionary{TKey, TValue}"/> where the <c>TKey</c> is a
    /// <see cref="string"/> and the <c>TValue</c> is a <see cref="Node"/>.
    /// Returns empty when the underlying AST parser is null or has no
    /// root node, or if there isn't an <c>interface</c> found.
    /// </returns>
    public static Dictionary<string, Node> GetDependentTypeMap(string interfaceName)
    {
        var ast = _defaultAst.Value;
        if (ast is null or { RootNode: null })
        {
            return [];
        }

        var rootNode = ast.RootNode;

        var interfaceNode = rootNode.Children
            .OfKind(TypeScriptSyntaxKind.InterfaceDeclaration)
            .FirstOrDefault(type => type.Identifier == interfaceName);

        if (interfaceNode is null)
        {
            return [];
        }

        var dependentTypes = new Dictionary<string, Node>
        {
            [interfaceNode.Identifier] = interfaceNode
        };

        var queue = new Queue<Node>();
        queue.Enqueue(interfaceNode);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();

            // Get methods
            var methods =
                node.OfKind(TypeScriptSyntaxKind.MethodSignature).Cast<MethodSignature>();

            foreach (var method in methods)
            {
                // Get method parameters
                var parameters =
                    method.OfKind(TypeScriptSyntaxKind.Parameter).Cast<ParameterDeclaration>();

                foreach (var parameter in parameters)
                {
                    // Get parameter type
                    var typeReferences =
                        parameter.OfKind(TypeScriptSyntaxKind.TypeReference)
                            .Cast<TypeReferenceNode>();

                    foreach (var typeReference in typeReferences)
                    {
                        dependentTypes[typeReference.Identifier] = typeReference;
                        queue.Enqueue(typeReference);

                        foreach ((var key, var @interface) in
                            GetDependentTypeMap(interfaceName: typeReference.Identifier))
                        {
                            dependentTypes[@interface.Identifier] = @interface;
                            queue.Enqueue(@interface);
                        }
                    }
                }

                // Get method return type
                var returnTypeReferences =
                    method.OfKind(TypeScriptSyntaxKind.TypeReference)
                        .Cast<TypeReferenceNode>();

                foreach (var typeReference in returnTypeReferences)
                {
                    dependentTypes[typeReference.Identifier] = typeReference;
                    queue.Enqueue(typeReference);
                }
            }

            // Get properties
            var properties =
                node.OfKind(TypeScriptSyntaxKind.PropertySignature)
                    .Cast<PropertySignature>();

            foreach (var property in properties)
            {
                dependentTypes[property.Identifier] = property;
                queue.Enqueue(property);
            }

            foreach (var childNode in node.Children)
            {
                queue.Enqueue(childNode);
            }
        }

        return dependentTypes;
    }
}