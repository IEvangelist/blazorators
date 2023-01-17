// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class DeleteExpression : UnaryExpression
{
    public DeleteExpression()
    {
        Kind = TypeScriptSyntaxKind.DeleteExpression;
    }

    public IExpression Expression { get; set; }
}