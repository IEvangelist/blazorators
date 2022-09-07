// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocParameterTag : JsDocTag
{
    internal JsDocParameterTag() => ((INode)this).Kind = CommentKind.JsDocParameterTag;

    internal Identifier PreParameterName { get; set; }
    internal JsDocTypeExpression TypeExpression { get; set; }
    internal Identifier PostParameterName { get; set; }
    internal Identifier ParameterName { get; set; }
    internal bool IsBracketed { get; set; }
}