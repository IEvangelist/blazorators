// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class Symbol : ISymbol
{
    internal SymbolFlags Flags { get; set; }
    internal string Name { get; set; }
    internal Declaration[] Declarations { get; set; }
    internal Declaration ValueDeclaration { get; set; }
    internal SymbolTable Members { get; set; }
    internal SymbolTable Exports { get; set; }
    internal SymbolTable GlobalExports { get; set; }
    internal int Id { get; set; }
    internal int MergeId { get; set; }
    internal Symbol Parent { get; set; }
    internal Symbol ExportSymbol { get; set; }
    internal bool ConstEnumOnlyModule { get; set; }
    internal bool IsReferenced { get; set; }
    internal bool IsReplaceableByMethod { get; set; }
    internal bool IsAssigned { get; set; }
}