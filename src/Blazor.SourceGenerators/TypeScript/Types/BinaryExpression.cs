// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class BinaryExpression : Node, IExpression, IDeclaration
{
    public BinaryExpression()
    {
        Kind = TypeScriptSyntaxKind.BinaryExpression;
    }

    public IExpression Left { get; set; }
    public Token OperatorToken { get; set; }
    public IExpression Right { get; set; }
    public object DeclarationBrand { get; set; }
    public INode Name { get; set; }
    public object ExpressionBrand { get; set; }
}