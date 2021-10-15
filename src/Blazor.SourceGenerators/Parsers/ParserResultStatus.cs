// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Parsers
{
    public enum ParserResultStatus
    {
        Unknown,
        TargetTypeNotFound,
        SuccessfullyParsed,
        ErrorParsing
    };
}