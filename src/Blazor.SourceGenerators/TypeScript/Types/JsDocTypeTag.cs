// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocTypeTag : JsDocTag
{
    public JsDocTypeTag()
    {
        Kind = TypeScriptSyntaxKind.JsDocTypeTag;
    }

    public JsDocTypeExpression TypeExpression { get; set; }
}