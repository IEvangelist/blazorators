// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;


public class JsDocTupleType : JsDocType
{
    public JsDocTupleType() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocTupleType;

    public NodeArray<IJsDocType> Types { get; set; }
}