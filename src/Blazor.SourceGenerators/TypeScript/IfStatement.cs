// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class IfStatement : Statement
{
    internal IfStatement() => ((INode)this).Kind = SyntaxKind.IfStatement;

    internal IExpression Expression { get; set; }
    internal IStatement ThenStatement { get; set; }
    internal IStatement ElseStatement { get; set; }
}