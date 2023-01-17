// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class InferenceContext
{
    public Signature Signature { get; set; }
    public bool InferUnionTypes { get; set; }
    public TypeInferences[] Inferences { get; set; }
    public TypeScriptType[] InferredTypes { get; set; }
    public TypeMapper Mapper { get; set; }
    public int FailedTypeParameterIndex { get; set; }
    public bool UseAnyForNoInferences { get; set; }
}