// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocTypeReference : JsDocType
{
    public JsDocTypeReference() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocTypeReference;

    public IEntityName? Name { get; set; }
    public NodeArray<IJsDocType> TypeArguments? { get; set; }
}