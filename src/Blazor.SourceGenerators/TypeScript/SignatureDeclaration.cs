// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class SignatureDeclaration : Declaration, ISignatureDeclaration
{
    public NodeArray<TypeParameterDeclaration>? TypeParameters { get; set; }
    public NodeArray<ParameterDeclaration>? Parameters { get; set; }
    public ITypeNode? Type { get; set; }
}
