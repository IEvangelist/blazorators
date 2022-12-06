// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsxSpreadAttribute : ObjectLiteralElement
{
    public JsxSpreadAttribute() => ((INode)this).Kind = TypeScriptSyntaxKind.JsxSpreadAttribute;

    public IExpression Expression { get; set; }
}