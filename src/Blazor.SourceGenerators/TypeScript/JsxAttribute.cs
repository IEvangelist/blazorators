// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsxAttribute : ObjectLiteralElement
{
    internal JsxAttribute() => ((INode)this).Kind = CommentKind.JsxAttribute;

    internal Node Initializer { get; set; }
}