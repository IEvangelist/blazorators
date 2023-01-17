// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TypeLiteralNode : Node, ITypeNode, IDeclaration
{
    public TypeLiteralNode()
    {
        Kind = TypeScriptSyntaxKind.TypeLiteral;
    }

    public NodeArray<ITypeElement> Members { get; set; }
    public object DeclarationBrand { get; set; }
    public INode Name { get; set; }
    public object TypeNodeBrand { get; set; }
}