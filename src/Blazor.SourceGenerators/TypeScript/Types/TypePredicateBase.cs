// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class TypePredicateBase
{
    public TypePredicateKind Kind { get; set; }
    public TypeScriptType Type { get; set; }
}