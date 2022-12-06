// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class JsDocTypedefTag : Node, IJsDocTag, IDeclaration
{
    public JsDocTypedefTag() => ((INode)this).Kind = TypeScriptSyntaxKind.JsDocTypedefTag;

    public INode? FullName { get; set; }
    public JsDocTypeExpression? TypeExpression? { get; set; }
    public JsDocTypeLiteral JsDocTypeLiteral? { get; set; }
    object IDeclaration.DeclarationBrand? { get; set; }
    INode? IDeclaration.Name { get; set; }
    AtToken IJsDocTag.AtToken? { get; set; }
    Identifier IJsDocTag.TagName? { get; set; }
    string IJsDocTag.Comment? { get; set; }
}