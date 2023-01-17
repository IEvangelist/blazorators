// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ReturnStatement : Statement
{
    public ReturnStatement()
    {
        Kind = TypeScriptSyntaxKind.ReturnStatement;
    }

    public IExpression Expression { get; set; }
}