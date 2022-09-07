// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ResolvedModuleWithFailedLookupLocations
{
    internal ResolvedModule ResolvedModule { get; set; } // ResolvedModuleFull
    internal string[] FailedLookupLocations { get; set; }
}