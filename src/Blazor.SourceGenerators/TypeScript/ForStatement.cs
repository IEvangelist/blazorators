// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ForStatement : IterationStatement
{
    internal ForStatement() => ((INode)this).Kind = CommentKind.ForStatement;

    internal /*ForInitializer*/IVariableDeclarationListOrExpression Initializer { get; set; }
    internal IExpression Condition { get; set; }
    internal IExpression Incrementor { get; set; }
}