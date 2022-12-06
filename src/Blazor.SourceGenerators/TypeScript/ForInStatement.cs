// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ForInStatement : IterationStatement
{
    public ForInStatement() => ((INode)this).Kind = TypeScriptSyntaxKind.ForInStatement;

    public /*ForInitializer*/IVariableDeclarationListOrExpression? Initializer { get; set; }
    public IExpression? Expression { get; set; }
}