// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocTemplateTag : JsDocTag
{
    public JsDocTemplateTag()
    {
        Kind = TypeScriptSyntaxKind.JsDocTemplateTag;
    }

    public NodeArray<TypeParameterDeclaration> TypeParameters { get; set; }
}