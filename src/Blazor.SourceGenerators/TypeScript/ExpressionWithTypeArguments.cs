// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ExpressionWithTypeArguments : TypeNode
{
    public ExpressionWithTypeArguments() => ((INode)this).Kind = TypeScriptSyntaxKind.ExpressionWithTypeArguments;

    public IExpression? Expression { get; set; }
    public NodeArray<ITypeNode> TypeArguments { get; set; } = NodeArray<ITypeNode>.Empty;
}