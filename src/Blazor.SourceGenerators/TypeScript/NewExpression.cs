// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class NewExpression : CallExpression, IPrimaryExpression, IDeclaration
{
    public NewExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.NewExpression;

    object IPrimaryExpression.PrimaryExpressionBrand? { get; set; }
}