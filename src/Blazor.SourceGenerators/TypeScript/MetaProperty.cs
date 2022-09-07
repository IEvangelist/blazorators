// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class MetaProperty : PrimaryExpression
{
    internal MetaProperty() => ((INode)this).Kind = CommentKind.MetaProperty;

    internal CommentKind KeywordToken { get; set; }
    internal Identifier Name { get; set; }
}