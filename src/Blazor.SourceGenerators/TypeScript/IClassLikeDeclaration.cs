// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public interface IClassLikeDeclaration : IDeclaration
{
    public NodeArray<TypeParameterDeclaration>? TypeParameters { get; set; }
    public NodeArray<HeritageClause>? HeritageClauses { get; set; }
    public NodeArray<IClassElement>? Members { get; set; }
}