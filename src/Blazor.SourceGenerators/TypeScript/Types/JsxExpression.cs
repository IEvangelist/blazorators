// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsxExpression : Expression, IJsxChild
{
    public JsxExpression()
    {
        Kind = TypeScriptSyntaxKind.JsxExpression;
    }

    public Token DotDotDotToken { get; set; }
    public IExpression Expression { get; set; }
}