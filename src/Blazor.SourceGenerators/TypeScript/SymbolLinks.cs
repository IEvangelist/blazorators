// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class SymbolLinks : ISymbolLinks
{
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