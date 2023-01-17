// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class ResolvedModule
{
    public string ResolvedFileName { get; set; }
    public bool IsExternalLibraryImport { get; set; }
}