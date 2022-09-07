// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class SignatureDeclaration : Declaration, ISignatureDeclaration
{
    internal NodeArray<TypeParameterDeclaration> TypeParameters { get; set; }
    internal NodeArray<ParameterDeclaration> Parameters { get; set; }
    internal ITypeNode Type { get; set; }
}
