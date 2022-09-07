// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class DiscoverTypingsInfo
{
    internal string[] FileNames { get; set; }
    internal string ProjectRootPath { get; set; }
    internal string SafeListPath { get; set; }
    internal Map<string> PackageNameToTypingLocation { get; set; }
    internal TypeAcquisition TypeAcquisition { get; set; }
    internal CompilerOptions CompilerOptions { get; set; }
    internal ReadonlyArray<string> UnresolvedImports { get; set; }
}