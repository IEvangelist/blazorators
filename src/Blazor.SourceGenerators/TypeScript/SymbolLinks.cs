// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class SymbolLinks : ISymbolLinks
{
    public Symbol? Target { get; set; }
    public TsType? Type { get; set; }
    public TsType? DeclaredType { get; set; }
    public TypeParameter[]? TypeParameters { get; set; }
    public TsType? InferredClassType { get; set; }
    public Map<TsType>? Instantiations { get; set; }
    public TypeMapper? Mapper { get; set; }
    public bool Referenced { get; set; }
    public UnionOrIntersectionType? ContainingType { get; set; }
    public Symbol? LeftSpread { get; set; }
    public Symbol? RightSpread { get; set; }
    public Symbol? MappedTypeOrigin { get; set; }
    public bool IsDiscriminantProperty { get; set; }
    public SymbolTable? ResolvedExports { get; set; }
    public bool ExportsChecked { get; set; }
    public bool TypeParametersChecked { get; set; }
    public bool IsDeclarationWithCollidingName { get; set; }
    public BindingElement? BindingElement { get; set; }
    public bool ExportsSomeValue { get; set; }
}