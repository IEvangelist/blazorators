// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class CommandLineOptionBase
{
    internal string Name { get; set; }
    internal object Type { get; set; } //  "string" | "number" | "boolean" | "object" | "list" | Map<number | string>
    internal bool IsFilePath { get; set; }
    internal string ShortName { get; set; }
    internal DiagnosticMessage Description { get; set; }
    internal DiagnosticMessage ParamType { get; set; }
    internal bool IsTsConfigOnly { get; set; }
    internal bool IsCommandLineOnly { get; set; }
    internal bool ShowInSimplifiedHelpView { get; set; }
    internal DiagnosticMessage Category { get; set; }
}