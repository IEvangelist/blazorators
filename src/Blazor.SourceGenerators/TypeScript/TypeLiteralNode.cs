// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TypeLiteralNode : Node, ITypeNode, IDeclaration
{
    internal TypeLiteralNode() => ((INode)this).Kind = CommentKind.TypeLiteral;

    internal NodeArray<ITypeElement> Members { get; set; }
    internal object DeclarationBrand { get; set; }
    internal INode Name { get; set; }
    internal object TypeNodeBrand { get; set; }
}