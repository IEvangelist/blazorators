// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TypeVariable : TsType
{
    internal TsType ResolvedBaseConstraint { get; set; }
    internal IndexType ResolvedIndexType { get; set; }
}