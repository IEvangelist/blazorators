// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class InferenceContext
{
    public Signature? Signature { get; set; }
    public bool InferUnionTypes { get; set; }
    public TypeInferences[]? Inferences { get; set; }
    public TsType[]? InferredTypes { get; set; }
    public TypeMapper? Mapper { get; set; }
    public int FailedTypeParameterIndex { get; set; }
    public bool UseAnyForNoInferences { get; set; }
}