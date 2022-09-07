// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsxAttributes : ObjectLiteralExpressionBase<ObjectLiteralElement>
{
    internal JsxAttributes() => ((INode)this).Kind = CommentKind.JsxAttributes;
}