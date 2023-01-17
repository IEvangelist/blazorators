// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocPropertyTag : Node, IJsDocTag, ITypeElement
{
    public JsDocPropertyTag()
    {
        Kind = TypeScriptSyntaxKind.JsDocPropertyTag;
    }

    public JsDocTypeExpression TypeExpression { get; set; }
    public AtToken AtToken { get; set; }
    public Identifier TagName { get; set; }
    public string Comment { get; set; }
    public object TypeElementBrand { get; set; }
    public INode Name { get; set; }
    public QuestionToken QuestionToken { get; set; }
    public object DeclarationBrand { get; set; }
}