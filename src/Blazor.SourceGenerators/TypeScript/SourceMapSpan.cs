// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class SourceMapSpan
{
    internal int EmittedLine { get; set; }
    internal int EmittedColumn { get; set; }
    internal int SourceLine { get; set; }
    internal int SourceColumn { get; set; }
    internal int NameIndex { get; set; }
    internal int SourceIndex { get; set; }
}