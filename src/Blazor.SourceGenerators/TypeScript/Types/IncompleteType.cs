// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class IncompleteType
{
    public TypeFlags Flags { get; set; }
    public TypeScriptType Type { get; set; }
}