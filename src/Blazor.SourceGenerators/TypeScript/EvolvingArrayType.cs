// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class EvolvingArrayType : ObjectType
{
    public TsType? ElementType { get; set; }
    public TsType? FinalArrayType { get; set; }
}