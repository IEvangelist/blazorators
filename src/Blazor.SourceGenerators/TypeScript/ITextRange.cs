// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal interface ITextRange
{
    int? Pos { get; set; }
    int? End { get; set; }
}
