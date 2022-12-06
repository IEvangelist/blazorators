// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class Decorator : Node
{
    public Decorator() => ((INode)this).Kind = TypeScriptSyntaxKind.Decorator;

    public /*LeftHandSideExpression*/IExpression? Expression { get; set; }
}
