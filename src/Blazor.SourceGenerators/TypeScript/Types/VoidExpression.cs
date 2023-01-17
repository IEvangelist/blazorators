// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class VoidExpression : UnaryExpression
{
    public VoidExpression()
    {
        Kind = TypeScriptSyntaxKind.VoidExpression;
    }

    public IExpression Expression { get; set; }
}