// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class InterfaceTypeWithDeclaredMembers : InterfaceType
{
    internal Symbol[] DeclaredProperties { get; set; }
    internal Signature[] DeclaredCallSignatures { get; set; }
    internal Signature[] DeclaredConstructSignatures { get; set; }
    internal IndexInfo DeclaredStringIndexInfo { get; set; }
    internal IndexInfo DeclaredNumberIndexInfo { get; set; }
}