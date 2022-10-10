// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public interface ISourceFileLike
{
    internal string Text { get; set; }
    internal int[] LineMap { get; set; }
}