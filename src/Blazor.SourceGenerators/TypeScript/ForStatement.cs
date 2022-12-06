// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ForStatement : IterationStatement
{
    public ForStatement() => ((INode)this).Kind = TypeScriptSyntaxKind.ForStatement;

    public /*ForInitializer*/IVariableDeclarationListOrExpression Initializer { get; set; }
    public IExpression Condition { get; set; }
    public IExpression Incrementor { get; set; }
}