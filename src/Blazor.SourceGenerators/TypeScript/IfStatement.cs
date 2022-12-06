// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class IfStatement : Statement
{
    public IfStatement() => ((INode)this).Kind = TypeScriptSyntaxKind.IfStatement;

    public IExpression Expression { get; set; }
    public IStatement ThenStatement { get; set; }
    public IStatement ElseStatement { get; set; }
}