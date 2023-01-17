// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class DoStatement : IterationStatement
{
    public DoStatement()
    {
        Kind = TypeScriptSyntaxKind.DoStatement;
    }

    public IExpression Expression { get; set; }
}