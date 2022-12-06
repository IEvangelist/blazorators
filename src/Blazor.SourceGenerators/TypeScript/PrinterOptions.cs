// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class PrinterOptions
{
    public ScriptTarget Target { get; set; }
    public bool RemoveComments { get; set; }
    public NewLineKind NewLine { get; set; }
    public bool SourceMap { get; set; }
    public bool InlineSourceMap { get; set; }
    public bool ExtendedDiagnostics { get; set; }
}