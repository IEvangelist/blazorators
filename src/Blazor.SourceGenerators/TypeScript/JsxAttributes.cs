// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsxAttributes : ObjectLiteralExpressionBase<ObjectLiteralElement>
{
    public JsxAttributes() => ((INode)this).Kind = TypeScriptSyntaxKind.JsxAttributes;
}