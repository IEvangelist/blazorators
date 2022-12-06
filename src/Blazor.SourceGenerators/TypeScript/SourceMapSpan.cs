// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class SourceMapSpan
{
    public int EmittedLine { get; set; }
    public int EmittedColumn { get; set; }
    public int SourceLine { get; set; }
    public int SourceColumn { get; set; }
    public int NameIndex { get; set; }
    public int SourceIndex { get; set; }
}