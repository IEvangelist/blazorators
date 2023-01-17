// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class SpreadElement : Expression
{
    public SpreadElement()
    {
        Kind = TypeScriptSyntaxKind.SpreadElement;
    }

    public IExpression Expression { get; set; }
}