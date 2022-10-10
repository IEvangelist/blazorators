// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocTypedefTag : Node, IJsDocTag, IDeclaration
{
    internal JsDocTypedefTag() => ((INode)this).Kind = SyntaxKind.JsDocTypedefTag;

    internal INode? FullName { get; set; }
    internal JsDocTypeExpression? TypeExpression { get; set; } = default!;
    internal JsDocTypeLiteral JsDocTypeLiteral { get; set; } = default!;
    object IDeclaration.DeclarationBrand { get; set; } = default!;
    INode? IDeclaration.Name { get; set; }
    AtToken IJsDocTag.AtToken { get; set; } = default!;
    Identifier IJsDocTag.TagName { get; set; } = default!;
    string IJsDocTag.Comment { get; set; } = default!;
}