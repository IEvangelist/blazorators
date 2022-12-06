// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocTemplateTag : JsDocTag
{
    public JsDocTemplateTag() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocTemplateTag;

    public NodeArray<TypeParameterDeclaration?>? TypeParameters { get; set; }
}