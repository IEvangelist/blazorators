// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class DiscoverTypingsInfo
{
    public string[] FileNames { get; set; } = Array.Empty<string>();
    public string? ProjectRootPath { get; set; }
    public string? SafeListPath { get; set; }
    public Map<string> PackageNameToTypingLocation { get; set; } = Map<string>.Empty;
    public TypeAcquisition? TypeAcquisition { get; set; }
    public CompilerOptions? CompilerOptions { get; set; }
    public ReadonlyArray<string> UnresolvedImports { get; set; } = ReadonlyArray<string>.Empty;
}