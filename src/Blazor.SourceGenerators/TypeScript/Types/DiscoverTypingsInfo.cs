// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class DiscoverTypingsInfo
{
    public string[] FileNames { get; set; }
    public string ProjectRootPath { get; set; }
    public string SafeListPath { get; set; }
    public Map<string> PackageNameToTypingLocation { get; set; }
    public TypeAcquisition TypeAcquisition { get; set; }
    public CompilerOptions CompilerOptions { get; set; }
    public IReadOnlyList<string> UnresolvedImports { get; set; }
}