// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class EmitHelper
{
    public string Name { get; set; }
    public bool Scoped { get; set; }
    public string Text { get; set; }
    public int Priority { get; set; }
}