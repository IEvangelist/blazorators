// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ClassLikeDeclaration : Declaration, IClassLikeDeclaration
{
    public NodeArray<TypeParameterDeclaration> TypeParameters { get; set; }
    public NodeArray<HeritageClause> HeritageClauses { get; set; }
    public NodeArray<IClassElement> Members { get; set; }
}