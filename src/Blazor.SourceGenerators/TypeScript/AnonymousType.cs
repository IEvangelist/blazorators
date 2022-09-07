// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class AnonymousType : ObjectType
{
    internal AnonymousType Target { get; set; } = default!;
    internal TypeMapper Mapper { get; set; } = default!;
}