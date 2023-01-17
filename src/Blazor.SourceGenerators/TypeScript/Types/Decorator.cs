// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class Decorator : Node
{
    public Decorator()
    {
        Kind = TypeScriptSyntaxKind.Decorator;
    }

    public IExpression Expression { get; set; }
}