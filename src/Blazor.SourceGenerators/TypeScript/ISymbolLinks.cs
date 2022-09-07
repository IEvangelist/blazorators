// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal interface ISymbolLinks
{
    Symbol Target { get; set; }
    TsType Type { get; set; }
    TsType DeclaredType { get; set; }
    TypeParameter[] TypeParameters { get; set; }
    TsType InferredClassType { get; set; }
    Map<TsType> Instantiations { get; set; }
    TypeMapper Mapper { get; set; }
    bool Referenced { get; set; }
    UnionOrIntersectionType ContainingType { get; set; }
    Symbol LeftSpread { get; set; }
    Symbol RightSpread { get; set; }
    Symbol MappedTypeOrigin { get; set; }
    bool IsDiscriminantProperty { get; set; }
    SymbolTable ResolvedExports { get; set; }
    bool ExportsChecked { get; set; }
    bool TypeParametersChecked { get; set; }
    bool IsDeclarationWithCollidingName { get; set; }
    BindingElement BindingElement { get; set; }
    bool ExportsSomeValue { get; set; }
}