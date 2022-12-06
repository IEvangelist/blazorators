// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ForOfStatement : IterationStatement
{
    public ForOfStatement() => ((INode)this).Kind = TypeScriptSyntaxKind.ForOfStatement;

    public AwaitKeywordToken AwaitModifier { get; set; }
    public /*ForInitializer*/IVariableDeclarationListOrExpression Initializer { get; set; }
    public IExpression Expression { get; set; }
}