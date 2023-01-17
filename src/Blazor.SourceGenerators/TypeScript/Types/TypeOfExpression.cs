// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TypeOfExpression : UnaryExpression
{
    public TypeOfExpression()
    {
        Kind = TypeScriptSyntaxKind.TypeOfExpression;
    }

    public IExpression Expression { get; set; }
}