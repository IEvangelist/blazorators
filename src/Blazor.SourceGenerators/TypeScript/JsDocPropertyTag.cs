// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocPropertyTag : Node, IJsDocTag, ITypeElement
{
    public JsDocPropertyTag() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocPropertyTag;

    public JsDocTypeExpression? TypeExpression? { get; set; }
    AtToken IJsDocTag.AtToken? { get; set; }
    Identifier IJsDocTag.TagName? { get; set; }
    string IJsDocTag.Comment? { get; set; }
    object ITypeElement.TypeElementBrand? { get; set; }
    INode? IDeclaration.Name? { get; set; }
    QuestionToken ITypeElement.QuestionToken? { get; set; }
    object IDeclaration.DeclarationBrand? { get; set; }
}