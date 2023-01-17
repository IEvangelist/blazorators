// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class Symbol : ISymbol
{
    public SymbolFlags Flags { get; set; }
    public string Name { get; set; }
    public Declaration[] Declarations { get; set; }
    public Declaration ValueDeclaration { get; set; }
    public SymbolTable Members { get; set; }
    public SymbolTable Exports { get; set; }
    public SymbolTable GlobalExports { get; set; }
    public int Id { get; set; }
    public int MergeId { get; set; }
    public Symbol Parent { get; set; }
    public Symbol ExportSymbol { get; set; }
    public bool ConstEnumOnlyModule { get; set; }
    public bool IsReferenced { get; set; }
    public bool IsReplaceableByMethod { get; set; }
    public bool IsAssigned { get; set; }
}