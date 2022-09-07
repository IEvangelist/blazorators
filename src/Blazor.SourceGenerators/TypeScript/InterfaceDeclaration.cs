// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class InterfaceDeclaration : DeclarationStatement
{
    internal InterfaceDeclaration() => ((INode)this).Kind = CommentKind.InterfaceDeclaration;

    internal NodeArray<TypeParameterDeclaration> TypeParameters { get; set; } = default!;
    internal NodeArray<HeritageClause> HeritageClauses { get; set; } = default!;
    internal NodeArray<ITypeElement> Members { get; set; } = default!;
}