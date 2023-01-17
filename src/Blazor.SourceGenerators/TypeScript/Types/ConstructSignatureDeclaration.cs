// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ConstructSignatureDeclaration : Declaration, ISignatureDeclaration, ITypeElement
{
    public ConstructSignatureDeclaration()
    {
        Kind = TypeScriptSyntaxKind.ConstructSignature;
    }

    public NodeArray<TypeParameterDeclaration> TypeParameters { get; set; }
    public NodeArray<ParameterDeclaration> Parameters { get; set; }
    public ITypeNode Type { get; set; }
    public object TypeElementBrand { get; set; }
    public QuestionToken QuestionToken { get; set; }
}