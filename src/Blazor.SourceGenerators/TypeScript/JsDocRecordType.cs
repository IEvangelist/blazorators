// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocRecordType : JsDocType
{
    public JsDocRecordType() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocRecordType;

    public TypeLiteralNode Literal { get; set; }
}