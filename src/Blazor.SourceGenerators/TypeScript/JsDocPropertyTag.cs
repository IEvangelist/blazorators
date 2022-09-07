// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class JsDocPropertyTag : Node, IJsDocTag, ITypeElement
{
    internal JsDocPropertyTag() => ((INode)this).Kind = CommentKind.JsDocPropertyTag;

    internal JsDocTypeExpression TypeExpression { get; set; } = default!;
    AtToken IJsDocTag.AtToken { get; set; } = default!;
    Identifier IJsDocTag.TagName { get; set; } = default!;
    string IJsDocTag.Comment { get; set; } = default!;
    object ITypeElement.TypeElementBrand { get; set; } = default!;
    INode IDeclaration.Name { get; set; } = default!;
    QuestionToken ITypeElement.QuestionToken { get; set; } = default!;
    object IDeclaration.DeclarationBrand { get; set; } = default!;
}