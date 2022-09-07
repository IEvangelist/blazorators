// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TypeMapper
{
    internal TsType[] MappedTypes { get; set; }
    internal TsType[] Instantiations { get; set; }
    internal InferenceContext Context { get; set; }
}