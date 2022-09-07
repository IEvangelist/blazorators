// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal interface ISourceFileLike
{
    string Text { get; set; }
    int[] LineMap { get; set; }
}