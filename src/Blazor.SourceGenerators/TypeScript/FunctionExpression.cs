// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class FunctionExpression : Node, IPrimaryExpression, IFunctionLikeDeclaration
{
    internal FunctionExpression() => ((INode)this).Kind = CommentKind.FunctionExpression;

    public NodeArray<TypeParameterDeclaration> TypeParameters { get; set; } = default!;
    public NodeArray<ParameterDeclaration> Parameters { get; set; } = default!;
    public ITypeNode Type { get; set; } = default!;
    object IPrimaryExpression.PrimaryExpressionBrand { get; set; } = default!;
    object IMemberExpression.MemberExpressionBrand { get; set; } = default!;
    object ILeftHandSideExpression.LeftHandSideExpressionBrand { get; set; } = default!;
    object IIncrementExpression.IncrementExpressionBrand { get; set; } = default!;
    object IUnaryExpression.UnaryExpressionBrand { get; set; } = default!;
    object IExpression.ExpressionBrand { get; set; } = default!;
    object IFunctionLikeDeclaration.FunctionLikeDeclarationBrand { get; set; } = default!;
    AsteriskToken IFunctionLikeDeclaration.AsteriskToken { get; set; } = default!;
    QuestionToken IFunctionLikeDeclaration.QuestionToken { get; set; } = default!;
    IBlockOrExpression IFunctionLikeDeclaration.Body { get; set; } = default!;
    object IDeclaration.DeclarationBrand { get; set; } = default!;
    INode IDeclaration.Name { get; set; } = default!;
}