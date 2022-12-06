// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TypeLiteralNode : Node, ITypeNode, IDeclaration
{
    public TypeLiteralNode() => ((INode)this).Kind = TypeScriptSyntaxKind.TypeLiteral;

    public NodeArray<ITypeElement> Members { get; set; }
    public object DeclarationBrand { get; set; }
    public INode? Name { get; set; }
    public object TypeNodeBrand { get; set; }
}