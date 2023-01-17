// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class PartiallyEmittedExpression : LeftHandSideExpression
{
    public PartiallyEmittedExpression()
    {
        Kind = TypeScriptSyntaxKind.PartiallyEmittedExpression;
    }

    public IExpression Expression { get; set; }
}