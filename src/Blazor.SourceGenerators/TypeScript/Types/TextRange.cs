// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class TextRange : ITextRange
{
    public int? Pos { get; set; }
    public int? End { get; set; }
}