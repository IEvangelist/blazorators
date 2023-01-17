// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class NewExpression : CallExpression, IPrimaryExpression, IDeclaration
{
    public NewExpression()
    {
        Kind = TypeScriptSyntaxKind.NewExpression;
    }

    public object PrimaryExpressionBrand { get; set; }
}