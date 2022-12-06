// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsxExpression : Expression, IJsxChild
{
    public JsxExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.JsxExpression;

    public Token DotDotDotToken { get; set; }
    public IExpression Expression { get; set; }
}