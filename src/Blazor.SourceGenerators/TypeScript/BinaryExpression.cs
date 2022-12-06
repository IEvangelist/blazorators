// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;


public class BinaryExpression : Node, IExpression, IDeclaration
{
    public BinaryExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.BinaryExpression;

    object IExpression.ExpressionBrand? { get; set; }
    object IDeclaration.DeclarationBrand? { get; set; }
    INode? IDeclaration.Name? { get; set; }
}