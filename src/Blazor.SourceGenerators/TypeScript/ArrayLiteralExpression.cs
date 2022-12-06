// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ArrayLiteralExpression : PrimaryExpression
{
    public ArrayLiteralExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.ArrayLiteralExpression;

    public NodeArray<IExpression> Elements? { get; set; }
    public bool MultiLine { get; set; }
}