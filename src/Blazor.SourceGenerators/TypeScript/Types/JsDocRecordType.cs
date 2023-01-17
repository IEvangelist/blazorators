// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocRecordType : JsDocType
{
    public JsDocRecordType()
    {
        Kind = TypeScriptSyntaxKind.JsDocRecordType;
    }

    public TypeLiteralNode Literal { get; set; }
}