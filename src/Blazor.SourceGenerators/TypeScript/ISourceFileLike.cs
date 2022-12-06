// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public interface ISourceFileLike
{
    public string Text { get; set; }
    public int[] LineMap { get; set; }
}