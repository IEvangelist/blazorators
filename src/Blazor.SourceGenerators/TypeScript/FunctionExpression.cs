// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class FunctionExpression : Node, IPrimaryExpression, IFunctionLikeDeclaration
{
    public FunctionExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.FunctionExpression;

    public NodeArray<TypeParameterDeclaration> TypeParameters? { get; set; }
    public NodeArray<ParameterDeclaration> Parameters? { get; set; }
    public ITypeNode Type? { get; set; }
    object IPrimaryExpression.PrimaryExpressionBrand? { get; set; }
    object IMemberExpression.MemberExpressionBrand? { get; set; }
    object ILeftHandSideExpression.LeftHandSideExpressionBrand? { get; set; }
    object IIncrementExpression.IncrementExpressionBrand? { get; set; }
    object IUnaryExpression.UnaryExpressionBrand? { get; set; }
    object IExpression.ExpressionBrand? { get; set; }
    object IFunctionLikeDeclaration.FunctionLikeDeclarationBrand? { get; set; }
    AsteriskToken IFunctionLikeDeclaration.AsteriskToken? { get; set; }
    QuestionToken IFunctionLikeDeclaration.QuestionToken? { get; set; }
    IBlockOrExpression IFunctionLikeDeclaration.Body? { get; set; }
    object IDeclaration.DeclarationBrand? { get; set; }
    INode? IDeclaration.Name? { get; set; }
}