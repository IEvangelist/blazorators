// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public interface ISourceFileLike
{
    string Text { get; set; }
    int[] LineMap { get; set; }
}