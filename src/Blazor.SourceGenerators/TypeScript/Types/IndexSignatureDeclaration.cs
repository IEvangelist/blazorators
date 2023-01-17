// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class IndexSignatureDeclaration : Declaration, ISignatureDeclaration, IClassElement, ITypeElement
{
    public IndexSignatureDeclaration()
    {
        Kind = TypeScriptSyntaxKind.IndexSignature;
    }

    public object ClassElementBrand { get; set; }
    public NodeArray<TypeParameterDeclaration> TypeParameters { get; set; }
    public NodeArray<ParameterDeclaration> Parameters { get; set; }
    public ITypeNode Type { get; set; }
    public object TypeElementBrand { get; set; }
    public QuestionToken QuestionToken { get; set; }
}