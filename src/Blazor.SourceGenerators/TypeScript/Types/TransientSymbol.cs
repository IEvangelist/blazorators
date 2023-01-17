// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TransientSymbol : ISymbol, ISymbolLinks
{
    public CheckFlags CheckFlags { get; set; }
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
    public Symbol Target { get; set; }
    public TypeScriptType Type { get; set; }
    public TypeScriptType DeclaredType { get; set; }
    public TypeParameter[] TypeParameters { get; set; }
    public TypeScriptType InferredClassType { get; set; }
    public Map<TypeScriptType> Instantiations { get; set; }
    public TypeMapper Mapper { get; set; }
    public bool Referenced { get; set; }
    public UnionOrIntersectionType ContainingType { get; set; }
    public Symbol LeftSpread { get; set; }
    public Symbol RightSpread { get; set; }
    public Symbol MappedTypeOrigin { get; set; }
    public bool IsDiscriminantProperty { get; set; }
    public SymbolTable ResolvedExports { get; set; }
    public bool ExportsChecked { get; set; }
    public bool TypeParametersChecked { get; set; }
    public bool IsDeclarationWithCollidingName { get; set; }
    public BindingElement BindingElement { get; set; }
    public bool ExportsSomeValue { get; set; }
}