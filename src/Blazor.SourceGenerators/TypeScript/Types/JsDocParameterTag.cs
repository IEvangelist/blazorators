// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocParameterTag : JsDocTag
{
    public JsDocParameterTag()
    {
        Kind = TypeScriptSyntaxKind.JsDocParameterTag;
    }

    public Identifier PreParameterName { get; set; }
    public JsDocTypeExpression TypeExpression { get; set; }
    public Identifier PostParameterName { get; set; }
    public Identifier ParameterName { get; set; }
    public bool IsBracketed { get; set; }
}