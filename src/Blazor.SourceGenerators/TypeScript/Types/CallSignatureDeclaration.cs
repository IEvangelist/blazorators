// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class CallSignatureDeclaration : Declaration, ISignatureDeclaration, ITypeElement
{
    public CallSignatureDeclaration()
    {
        Kind = TypeScriptSyntaxKind.CallSignature;
    }

    public NodeArray<TypeParameterDeclaration> TypeParameters { get; set; }
    public NodeArray<ParameterDeclaration> Parameters { get; set; }
    public ITypeNode Type { get; set; }
    public object TypeElementBrand { get; set; }
    public QuestionToken QuestionToken { get; set; }
}