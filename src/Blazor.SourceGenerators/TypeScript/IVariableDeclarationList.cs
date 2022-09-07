// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal interface IVariableDeclarationList : INode, IVariableDeclarationListOrExpression
{
    NodeArray<VariableDeclaration> Declarations { get; set; }
}
