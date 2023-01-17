// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class CommandLineOptionBase
{
    public string Name { get; set; }

    //  "string" | "number" | "boolean" | "object" | "list" | Map<number | string>
    public object Type { get; set; }
    public bool IsFilePath { get; set; }
    public string ShortName { get; set; }
    public DiagnosticMessage Description { get; set; }
    public DiagnosticMessage ParamType { get; set; }
    public bool IsTsConfigOnly { get; set; }
    public bool IsCommandLineOnly { get; set; }
    public bool ShowInSimplifiedHelpView { get; set; }
    public DiagnosticMessage Category { get; set; }
}