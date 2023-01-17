// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public interface ISymbolLinks
{
    Symbol Target { get; set; }
    TypeScriptType Type { get; set; }
    TypeScriptType DeclaredType { get; set; }
    TypeParameter[] TypeParameters { get; set; }
    TypeScriptType InferredClassType { get; set; }
    Map<TypeScriptType> Instantiations { get; set; }
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