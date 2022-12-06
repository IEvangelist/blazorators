// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ObjectLiteralExpression : ObjectLiteralExpressionBase<IObjectLiteralElementLike>
{
    public ObjectLiteralExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.ObjectLiteralExpression;

    public bool MultiLine { get; set; }
}