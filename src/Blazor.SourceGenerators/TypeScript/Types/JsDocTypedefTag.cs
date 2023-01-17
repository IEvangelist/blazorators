// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocTypedefTag : Node, IJsDocTag, IDeclaration
{
    public JsDocTypedefTag()
    {
        Kind = TypeScriptSyntaxKind.JsDocTypedefTag;
    }

    public INode FullName { get; set; }
    public JsDocTypeExpression TypeExpression { get; set; }
    public JsDocTypeLiteral JsDocTypeLiteral { get; set; }
    public object DeclarationBrand { get; set; }
    public INode Name { get; set; }
    public AtToken AtToken { get; set; }
    public Identifier TagName { get; set; }
    public string Comment { get; set; }
}