// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TransientSymbol : ISymbol, ISymbolLinks
{
    internal CheckFlags CheckFlags { get; set; }
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
    internal Symbol Target { get; set; }
    internal TsType Type { get; set; }
    internal TsType DeclaredType { get; set; }
    internal TypeParameter[] TypeParameters { get; set; }
    internal TsType InferredClassType { get; set; }
    internal Map<TsType> Instantiations { get; set; }
    internal TypeMapper Mapper { get; set; }
    internal bool Referenced { get; set; }
    internal UnionOrIntersectionType ContainingType { get; set; }
    internal Symbol LeftSpread { get; set; }
    internal Symbol RightSpread { get; set; }
    internal Symbol MappedTypeOrigin { get; set; }
    internal bool IsDiscriminantProperty { get; set; }
    internal SymbolTable ResolvedExports { get; set; }
    internal bool ExportsChecked { get; set; }
    internal bool TypeParametersChecked { get; set; }
    internal bool IsDeclarationWithCollidingName { get; set; }
    internal BindingElement BindingElement { get; set; }
    internal bool ExportsSomeValue { get; set; }
}