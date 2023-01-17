// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ArrayLiteralExpression : PrimaryExpression
{
    public ArrayLiteralExpression()
    {
        Kind = TypeScriptSyntaxKind.ArrayLiteralExpression;
    }

    public NodeArray<IExpression> Elements { get; set; }
    public bool MultiLine { get; set; }
}