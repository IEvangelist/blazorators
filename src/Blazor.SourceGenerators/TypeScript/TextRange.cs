// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class TextRange : ITextRange
{
    int? ITextRange.Pos { get; set; }
    int? ITextRange.End { get; set; }
}
