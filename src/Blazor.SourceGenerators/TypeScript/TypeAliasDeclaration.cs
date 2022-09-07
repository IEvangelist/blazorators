// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TypeAliasDeclaration : DeclarationStatement
{
    internal TypeAliasDeclaration() => ((INode)this).Kind = CommentKind.TypeAliasDeclaration;

    internal NodeArray<TypeParameterDeclaration> TypeParameters { get; set; }
    internal ITypeNode Type { get; set; }
}