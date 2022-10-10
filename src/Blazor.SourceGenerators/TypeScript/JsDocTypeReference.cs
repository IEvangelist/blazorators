// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocTypeReference : JsDocType
{
    internal JsDocTypeReference() => ((INode)this).Kind = SyntaxKind.JsDocTypeReference;

    internal IEntityName? Name { get; set; }
    internal NodeArray<IJsDocType> TypeArguments { get; set; } = default!;
}