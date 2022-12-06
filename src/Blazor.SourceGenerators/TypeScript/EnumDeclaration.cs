// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class EnumDeclaration : DeclarationStatement
{
    public EnumDeclaration() => ((INode)this).Kind = TypeScriptSyntaxKind.EnumDeclaration;

    public NodeArray<EnumMember> Members { get; set; } = NodeArray<EnumMember>.Empty;
}