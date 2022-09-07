// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class EnumDeclaration : DeclarationStatement
{
    internal EnumDeclaration() => ((INode)this).Kind = CommentKind.EnumDeclaration;

    internal NodeArray<EnumMember> Members { get; set; }
}