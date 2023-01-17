// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class JsDocFunctionType : Node, IJsDocType, ISignatureDeclaration
{
    public JsDocFunctionType()
    {
        Kind = TypeScriptSyntaxKind.JsDocFunctionType;
    }

    public object JsDocTypeBrand { get; set; }
    public object TypeNodeBrand { get; set; }
    public INode Name { get; set; }
    public NodeArray<TypeParameterDeclaration> TypeParameters { get; set; }
    public NodeArray<ParameterDeclaration> Parameters { get; set; }
    public ITypeNode Type { get; set; }
    public object DeclarationBrand { get; set; }
}