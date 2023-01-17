// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class CallExpression : Node, IMemberExpression, IDeclaration
{
    public CallExpression()
    {
        Kind = TypeScriptSyntaxKind.CallExpression;
    }

    public IExpression Expression { get; set; }
    public NodeArray<ITypeNode> TypeArguments { get; set; }
    public NodeArray<IExpression> Arguments { get; set; }
    public object DeclarationBrand { get; set; }
    public INode Name { get; set; }
    public object LeftHandSideExpressionBrand { get; set; }
    public object IncrementExpressionBrand { get; set; }
    public object UnaryExpressionBrand { get; set; }
    public object ExpressionBrand { get; set; }
    public object MemberExpressionBrand { get; set; }
}