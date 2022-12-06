// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocUnknownType : JsDocType
{
    public JsDocUnknownType() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocUnknownType;
}