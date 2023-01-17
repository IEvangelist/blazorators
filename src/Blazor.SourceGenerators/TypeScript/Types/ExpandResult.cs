// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ExpandResult
{
    public string[] FileNames { get; set; }
    public Map<WatchDirectoryFlags> WildcardDirectories { get; set; }
}