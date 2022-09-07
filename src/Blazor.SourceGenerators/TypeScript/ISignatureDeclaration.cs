// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal interface ISignatureDeclaration : IDeclaration
{
    NodeArray<TypeParameterDeclaration> TypeParameters { get; set; }
    NodeArray<ParameterDeclaration> Parameters { get; set; }
    ITypeNode Type { get; set; }
}
