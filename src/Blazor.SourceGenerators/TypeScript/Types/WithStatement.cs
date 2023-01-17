// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class WithStatement : Statement
{
    public WithStatement()
    {
        Kind = TypeScriptSyntaxKind.WithStatement;
    }

    public IExpression Expression { get; set; }
    public IStatement Statement { get; set; }
}