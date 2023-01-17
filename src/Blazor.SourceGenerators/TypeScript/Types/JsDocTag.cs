// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class JsDocTag : Node, IJsDocTag
{
    public JsDocTag()
    {
        Kind = TypeScriptSyntaxKind.JsDocTag;
    }

    public AtToken AtToken { get; set; }
    public Identifier TagName { get; set; }
    public string Comment { get; set; }
}