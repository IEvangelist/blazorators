// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ResolvedModuleWithFailedLookupLocations
{
    public ResolvedModule ResolvedModule { get; set; }
    public string[] FailedLookupLocations { get; set; }
}