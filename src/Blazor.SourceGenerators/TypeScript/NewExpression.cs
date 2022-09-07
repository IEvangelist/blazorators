// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class NewExpression : CallExpression, IPrimaryExpression, IDeclaration
{
    internal NewExpression() => ((INode)this).Kind = CommentKind.NewExpression;

    object IPrimaryExpression.PrimaryExpressionBrand { get; set; } = default!;
}