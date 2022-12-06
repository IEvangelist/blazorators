// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ElementAccessExpression : MemberExpression
{
    public ElementAccessExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.ElementAccessExpression;

    public IExpression Expression? { get; set; }
    public IExpression ArgumentExpression? { get; set; }
}