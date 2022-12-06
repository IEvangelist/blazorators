// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocRecordMember : PropertySignature
{
    public JsDocRecordMember() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocRecordMember;
}