// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal sealed class AmdDependency
{
    internal string Path { get; set; } = default!;
    internal string Name { get; set; } = default!;
}