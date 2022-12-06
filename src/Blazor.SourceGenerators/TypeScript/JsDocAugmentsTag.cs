// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocAugmentsTag : JsDocTag
{
    public JsDocAugmentsTag() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocAugmentsTag;

    public JsDocTypeExpression? TypeExpression { get; set; }
}