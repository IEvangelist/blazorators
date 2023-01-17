// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class ExpressionStatement : Statement
{
    public ExpressionStatement()
    {
        Kind = TypeScriptSyntaxKind.ExpressionStatement;
    }

    public IExpression Expression { get; set; }
}