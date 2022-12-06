// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class DeclarationStatement : Node, IDeclarationStatement, IDeclaration, IStatement
{
    object IDeclaration.DeclarationBrand? { get; set; }
    INode? IDeclaration.Name? { get; set; }
    object IStatement.StatementBrand? { get; set; }
}
