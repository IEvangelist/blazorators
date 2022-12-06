// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsxAttribute : ObjectLiteralElement
{
    public JsxAttribute() => ((INode)this).Kind = TypeScriptSyntaxKind.JsxAttribute;

    public Node Initializer { get; set; }
}