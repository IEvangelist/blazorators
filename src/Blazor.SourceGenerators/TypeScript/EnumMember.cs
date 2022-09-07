// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class EnumMember : Declaration
{
    internal EnumMember() => ((INode)this).Kind = CommentKind.EnumMember;

    internal IExpression Initializer { get; set; }
}