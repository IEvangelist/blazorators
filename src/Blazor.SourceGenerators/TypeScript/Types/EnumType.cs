// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class EnumType : TypeScriptType
{
    public EnumLiteralType[] MemberTypes { get; set; }
}