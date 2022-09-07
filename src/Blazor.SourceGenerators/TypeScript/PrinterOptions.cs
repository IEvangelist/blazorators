// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class PrinterOptions
{
    internal ScriptTarget Target { get; set; }
    internal bool RemoveComments { get; set; }
    internal NewLineKind NewLine { get; set; }
    internal bool SourceMap { get; set; }
    internal bool InlineSourceMap { get; set; }
    internal bool ExtendedDiagnostics { get; set; }
}