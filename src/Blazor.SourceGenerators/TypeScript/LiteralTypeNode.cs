// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class LiteralTypeNode : TypeNode
{
    public LiteralTypeNode() => ((INode)this).Kind = TypeScriptSyntaxKind.LiteralType;

    public IExpression Literal { get; set; }
}