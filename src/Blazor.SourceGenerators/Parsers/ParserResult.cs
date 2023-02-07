// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Parsers;

internal readonly record struct ParserResult<T>(ParserResultStatus Status) where T : class
{
    internal T? Value { get; init; } = default!;

    internal string? Error { get; init; } = default!;
}
