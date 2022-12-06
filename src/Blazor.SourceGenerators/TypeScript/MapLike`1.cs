// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class MapLike<T> : List<T>
{
    public static MapLike<T> Empty => new();
}
