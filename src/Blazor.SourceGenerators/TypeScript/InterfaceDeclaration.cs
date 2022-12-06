// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class InterfaceDeclaration : DeclarationStatement
{
    public InterfaceDeclaration() => ((INode)this).Kind = TypeScriptSyntaxKind.InterfaceDeclaration;

    public NodeArray<TypeParameterDeclaration> TypeParameters? { get; set; }
    public NodeArray<HeritageClause> HeritageClauses? { get; set; }
    public NodeArray<ITypeElement> Members? { get; set; }
}