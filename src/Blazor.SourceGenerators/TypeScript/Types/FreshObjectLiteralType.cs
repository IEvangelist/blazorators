// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class FreshObjectLiteralType : ResolvedType
{
    public ResolvedType RegularType { get; set; }
}