// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public readonly record struct DiagnosticMessage
{
    public string Key { get; init; }
    public DiagnosticCategory Category { get; init; }
    public int Code { get; init; }
    public string Message { get; init; }

    public static DiagnosticMessage Warning(string key, int code, string message = default)
    {
        return new()
        {
            Key = key,
            Category = DiagnosticCategory.Warning,
            Code = code,
            Message = key ?? message
        };
    }

    public static DiagnosticMessage Error(string key, int code, string message = default)
    {
        return new()
        {
            Key = key,
            Category = DiagnosticCategory.Error,
            Code = code,
            Message = key ?? message
        };
    }

    public static DiagnosticMessage Info(string key, int code, string message = default)
    {
        return new()
        {
            Key = key,
            Category = DiagnosticCategory.Message,
            Code = code,
            Message = key ?? message
        };
    }
}