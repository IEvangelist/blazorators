// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ClassLikeDeclaration : Declaration, IClassLikeDeclaration
{
    NodeArray<TypeParameterDeclaration> IClassLikeDeclaration.TypeParameters { get; set; } = default!;
    NodeArray<HeritageClause> IClassLikeDeclaration.HeritageClauses { get; set; } = default!;
    NodeArray<IClassElement> IClassLikeDeclaration.Members { get; set; } = default!;
}