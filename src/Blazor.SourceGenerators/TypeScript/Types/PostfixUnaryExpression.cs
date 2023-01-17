// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class PostfixUnaryExpression : IncrementExpression
{
    public PostfixUnaryExpression()
    {
        Kind = TypeScriptSyntaxKind.PostfixUnaryExpression;
    }

    public IExpression Operand { get; set; }
    public TypeScriptSyntaxKind Operator { get; set; }
}