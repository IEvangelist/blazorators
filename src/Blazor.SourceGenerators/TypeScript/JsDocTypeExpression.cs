// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;
public class JsDocTypeExpression : Node
{
    public JsDocTypeExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocTypeExpression;

    public IJsDocType Type { get; set; }
}