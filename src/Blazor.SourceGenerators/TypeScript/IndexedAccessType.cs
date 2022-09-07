// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class IndexedAccessType : TypeVariable
{
    internal TsType ObjectType { get; set; }
    internal TsType IndexType { get; set; }
    internal TsType Constraint { get; set; }
}