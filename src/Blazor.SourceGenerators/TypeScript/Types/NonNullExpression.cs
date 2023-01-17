// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class NonNullExpression : MemberExpression
{
    public NonNullExpression()
    {
        Kind = TypeScriptSyntaxKind.NonNullExpression;
    }

    public IExpression Expression { get; set; }
}