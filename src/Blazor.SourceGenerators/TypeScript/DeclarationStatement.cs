// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class DeclarationStatement : Node, IDeclarationStatement, IDeclaration, IStatement
{
    object IDeclaration.DeclarationBrand { get; set; } = default!;
    INode IDeclaration.Name { get; set; } = default!;
    object IStatement.StatementBrand { get; set; } = default!;
}
