// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public interface ITextRange
{
    int? Pos { get; set; }
    int? End { get; set; }
}
