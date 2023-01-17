// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class AwaitExpression : UnaryExpression
{
    public AwaitExpression()
    {
        Kind = TypeScriptSyntaxKind.AwaitExpression;
    }

    public IExpression Expression { get; set; }
}