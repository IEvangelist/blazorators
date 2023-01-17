// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocReturnTag : JsDocTag
{
    public JsDocReturnTag()
    {
        Kind = TypeScriptSyntaxKind.JsDocReturnTag;
    }

    public JsDocTypeExpression TypeExpression { get; set; }
}