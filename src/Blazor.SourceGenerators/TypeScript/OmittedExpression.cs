// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class OmittedExpression : Expression, IArrayBindingElement
{
    public OmittedExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.OmittedExpression;
}