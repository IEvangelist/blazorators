// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class NonNullExpression : /*LeftHandSideExpression*/MemberExpression
{
    public NonNullExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.NonNullExpression;

    public IExpression Expression { get; set; }
}