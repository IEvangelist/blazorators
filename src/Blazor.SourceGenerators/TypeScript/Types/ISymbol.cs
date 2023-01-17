// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public interface ISymbol
{
    SymbolFlags Flags { get; set; }
    string Name { get; set; }
    Declaration[] Declarations { get; set; }
    Declaration ValueDeclaration { get; set; }
    SymbolTable Members { get; set; }
    SymbolTable Exports { get; set; }
    SymbolTable GlobalExports { get; set; }
    int Id { get; set; }
    int MergeId { get; set; }
    Symbol Parent { get; set; }
    Symbol ExportSymbol { get; set; }
    bool ConstEnumOnlyModule { get; set; }
    bool IsReferenced { get; set; }
    bool IsReplaceableByMethod { get; set; }
    bool IsAssigned { get; set; }
}