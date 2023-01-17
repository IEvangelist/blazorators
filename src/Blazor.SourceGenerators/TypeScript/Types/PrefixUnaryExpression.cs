// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class PrefixUnaryExpression : IncrementExpression
{
    public PrefixUnaryExpression()
    {
        Kind = TypeScriptSyntaxKind.PrefixUnaryExpression;
    }

    public TypeScriptSyntaxKind Operator { get; set; }
    public IExpression Operand { get; set; }
}