// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public interface IVariableDeclarationList : IVariableDeclarationListOrExpression
{
    NodeArray<VariableDeclaration> Declarations { get; set; }
}