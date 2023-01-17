// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ThrowStatement : Statement
{
    public ThrowStatement()
    {
        Kind = TypeScriptSyntaxKind.ThrowStatement;
    }

    public IExpression Expression { get; set; }
}