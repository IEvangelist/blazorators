// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ForInStatement : IterationStatement
{
    internal ForInStatement() => ((INode)this).Kind = SyntaxKind.ForInStatement;

    internal /*ForInitializer*/IVariableDeclarationListOrExpression Initializer { get; set; }
    internal IExpression Expression { get; set; }
}