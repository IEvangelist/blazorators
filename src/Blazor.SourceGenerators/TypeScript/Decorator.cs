// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class Decorator : Node
{
    internal Decorator() => ((INode)this).Kind = CommentKind.Decorator;

    internal /*LeftHandSideExpression*/IExpression Expression { get; set; }
}
