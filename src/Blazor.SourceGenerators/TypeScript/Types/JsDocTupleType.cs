// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocTupleType : JsDocType
{
    public JsDocTupleType()
    {
        Kind = TypeScriptSyntaxKind.JsDocTupleType;
    }

    public NodeArray<IJsDocType> Types { get; set; }
}