// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class InferenceContext
{
    internal Signature Signature { get; set; }
    internal bool InferUnionTypes { get; set; }
    internal TypeInferences[] Inferences { get; set; }
    internal TsType[] InferredTypes { get; set; }
    internal TypeMapper Mapper { get; set; }
    internal int FailedTypeParameterIndex { get; set; }
    internal bool UseAnyForNoInferences { get; set; }
}