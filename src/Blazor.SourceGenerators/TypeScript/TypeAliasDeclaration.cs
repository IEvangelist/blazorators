// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TypeAliasDeclaration : DeclarationStatement
{
    public TypeAliasDeclaration() => ((INode)this).Kind = TypeScriptSyntaxKind.TypeAliasDeclaration;

    public NodeArray<TypeParameterDeclaration> TypeParameters { get; set; }
    public ITypeNode Type { get; set; }
}