// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal interface IClassLikeDeclaration : IDeclaration
{
    internal NodeArray<TypeParameterDeclaration>? TypeParameters { get; set; }
    internal NodeArray<HeritageClause>? HeritageClauses { get; set; }
    internal NodeArray<IClassElement>? Members { get; set; }
}