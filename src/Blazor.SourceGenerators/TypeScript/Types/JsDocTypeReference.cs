// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocTypeReference : JsDocType
{
    public JsDocTypeReference()
    {
        Kind = TypeScriptSyntaxKind.JsDocTypeReference;
    }

    public IEntityName Name { get; set; }
    public NodeArray<IJsDocType> TypeArguments { get; set; }
}