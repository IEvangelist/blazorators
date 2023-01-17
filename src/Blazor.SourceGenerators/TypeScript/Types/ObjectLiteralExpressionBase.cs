// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class ObjectLiteralExpressionBase<T> : Node, IPrimaryExpression, IDeclaration
{
    public NodeArray<T> Properties { get; set; }
    public object DeclarationBrand { get; set; }
    public INode Name { get; set; }
    public object PrimaryExpressionBrand { get; set; }
    public object MemberExpressionBrand { get; set; }
    public object LeftHandSideExpressionBrand { get; set; }
    public object IncrementExpressionBrand { get; set; }
    public object UnaryExpressionBrand { get; set; }
    public object ExpressionBrand { get; set; }
}