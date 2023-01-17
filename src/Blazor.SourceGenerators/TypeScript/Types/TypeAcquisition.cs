// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TypeAcquisition
{
    public bool EnableAutoDiscovery { get; set; }
    public bool Enable { get; set; }
    public string[] Include { get; set; }
    public string[] Exclude { get; set; }
}