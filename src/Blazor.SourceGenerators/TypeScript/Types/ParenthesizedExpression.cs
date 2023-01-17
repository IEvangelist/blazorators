// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ParenthesizedExpression : PrimaryExpression
{
    public ParenthesizedExpression()
    {
        Kind = TypeScriptSyntaxKind.ParenthesizedExpression;
    }

    public IExpression Expression { get; set; }
}