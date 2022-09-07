// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocTemplateTag : JsDocTag
{
    internal JsDocTemplateTag() => ((INode)this).Kind = CommentKind.JsDocTemplateTag;

    internal NodeArray<TypeParameterDeclaration> TypeParameters { get; set; }
}