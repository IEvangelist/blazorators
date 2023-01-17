// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ConstructorTypeNode : Node, ITypeNode, IFunctionOrConstructorTypeNode
{
    public ConstructorTypeNode()
    {
        Kind = TypeScriptSyntaxKind.ConstructorType;
    }

    public INode Name { get; set; }
    public NodeArray<TypeParameterDeclaration> TypeParameters { get; set; }
    public NodeArray<ParameterDeclaration> Parameters { get; set; }
    public ITypeNode Type { get; set; }
    public object DeclarationBrand { get; set; }
    public object TypeNodeBrand { get; set; }
}