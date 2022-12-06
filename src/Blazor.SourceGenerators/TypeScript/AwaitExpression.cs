// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class AwaitExpression : UnaryExpression
{
    public AwaitExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.AwaitExpression;

    public IExpression Expression? { get; set; }
}