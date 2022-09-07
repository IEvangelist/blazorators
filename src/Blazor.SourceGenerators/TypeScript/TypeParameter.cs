// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TypeParameter : TypeVariable
{
    internal TsType Constraint { get; set; }
    internal TsType Default { get; set; }
    internal TypeParameter Target { get; set; }
    internal TypeMapper Mapper { get; set; }
    internal bool IsThisType { get; set; }
    internal TsType ResolvedDefaultType { get; set; }
}