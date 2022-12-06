// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TypeAssertion : UnaryExpression
{
    public TypeAssertion() => ((INode)this).Kind = TypeScriptSyntaxKind.TypeAssertionExpression;

    public ITypeNode Type { get; set; }
    public IExpression Expression { get; set; }
}