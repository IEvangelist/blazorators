// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ClassLikeDeclaration : Declaration, IClassLikeDeclaration
{
    NodeArray<TypeParameterDeclaration>? IClassLikeDeclaration.TypeParameters? { get; set; }
    NodeArray<HeritageClause>? IClassLikeDeclaration.HeritageClauses? { get; set; }
    NodeArray<IClassElement>? IClassLikeDeclaration.Members? { get; set; }
}