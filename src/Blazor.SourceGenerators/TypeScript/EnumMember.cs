// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class EnumMember : Declaration
{
    internal EnumMember() => ((INode)this).Kind = SyntaxKind.EnumMember;

    internal IExpression Initializer { get; set; }
}