// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ObjectLiteralExpression : ObjectLiteralExpressionBase<IObjectLiteralElementLike>
{
    internal ObjectLiteralExpression() => ((INode)this).Kind = SyntaxKind.ObjectLiteralExpression;

    internal bool MultiLine { get; set; }
}