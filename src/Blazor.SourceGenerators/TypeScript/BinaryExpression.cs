// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;


internal class BinaryExpression : Node, IExpression, IDeclaration
{
    internal BinaryExpression() => ((INode)this).Kind = CommentKind.BinaryExpression;

    object IExpression.ExpressionBrand { get; set; } = default!;
    object IDeclaration.DeclarationBrand { get; set; } = default!;
    INode IDeclaration.Name { get; set; } = default!;
}