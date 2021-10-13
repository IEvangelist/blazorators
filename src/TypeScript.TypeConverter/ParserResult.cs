// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace TypeScript.TypeConverter;

public record ParserResult<T>(ParserResultStatus Status) where T : class
{
    public string? Error { get; init; } = default!;

    public T? Result { get; init; } = default!;
}
