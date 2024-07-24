// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public interface IDeclarationStatement : IDeclaration, IStatement
{
    // Node Name { get; set; } // Identifier | StringLiteral | NumericLiteral
}