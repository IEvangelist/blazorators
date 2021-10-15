// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Parsers
{
    public record ParserResult<T>(ParserResultStatus Status) where T : class
    {
        public T? Value { get; init; } = default!;

        public string? Error { get; init; } = default!;
    }
}