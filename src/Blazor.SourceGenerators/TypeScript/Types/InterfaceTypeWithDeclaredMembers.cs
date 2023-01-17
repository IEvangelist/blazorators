// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class InterfaceTypeWithDeclaredMembers : InterfaceType
{
    public Symbol[] DeclaredProperties { get; set; }
    public Signature[] DeclaredCallSignatures { get; set; }
    public Signature[] DeclaredConstructSignatures { get; set; }
    public IndexInfo DeclaredStringIndexInfo { get; set; }
    public IndexInfo DeclaredNumberIndexInfo { get; set; }
}