// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class EvolvingArrayType : ObjectType
{
    internal TsType ElementType { get; set; }
    internal TsType FinalArrayType { get; set; }
}