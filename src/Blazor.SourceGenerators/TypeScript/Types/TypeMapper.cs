// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TypeMapper
{
    public TypeScriptType[] MappedTypes { get; set; }
    public TypeScriptType[] Instantiations { get; set; }
    public InferenceContext Context { get; set; }
}