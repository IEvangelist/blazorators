// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ForOfStatement : IterationStatement
{
    internal ForOfStatement() => ((INode)this).Kind = CommentKind.ForOfStatement;

    internal AwaitKeywordToken AwaitModifier { get; set; }
    internal /*ForInitializer*/IVariableDeclarationListOrExpression Initializer { get; set; }
    internal IExpression Expression { get; set; }
}