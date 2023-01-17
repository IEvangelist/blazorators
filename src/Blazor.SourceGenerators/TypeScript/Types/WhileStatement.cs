// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class WhileStatement : IterationStatement
{
    public WhileStatement()
    {
        Kind = TypeScriptSyntaxKind.WhileStatement;
    }

    public IExpression Expression { get; set; }
}