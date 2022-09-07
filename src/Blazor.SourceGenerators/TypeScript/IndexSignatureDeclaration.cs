// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class IndexSignatureDeclaration : Declaration,
    ISignatureDeclaration,
    IClassElement,
    ITypeElement
{
    internal IndexSignatureDeclaration() => ((INode)this).Kind = CommentKind.IndexSignature;

    NodeArray<TypeParameterDeclaration> ISignatureDeclaration.TypeParameters { get; set; } = default!;
    NodeArray<ParameterDeclaration> ISignatureDeclaration.Parameters { get; set; } = default!;
    ITypeNode ISignatureDeclaration.Type { get; set; } = default!;
    object IClassElement.ClassElementBrand { get; set; } = default!;
    object ITypeElement.TypeElementBrand { get; set; } = default!;
    QuestionToken ITypeElement.QuestionToken { get; set; } = default!;
}
