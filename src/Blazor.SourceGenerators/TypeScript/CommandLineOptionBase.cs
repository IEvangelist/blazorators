// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class CommandLineOptionBase
{
    public string Name? { get; set; }
    public object Type? { get; set; } //  "string" | "number" | "boolean" | "object" | "list" | Map<number | string>
    public bool IsFilePath { get; set; }
    public string ShortName? { get; set; }
    //public DiagnosticMessage Description { get; set; }
    //public DiagnosticMessage ParamType { get; set; }
    public bool IsTsConfigOnly { get; set; }
    public bool IsCommandLineOnly { get; set; }
    public bool ShowInSimplifiedHelpView { get; set; }
    //public DiagnosticMessage Category { get; set; }
}