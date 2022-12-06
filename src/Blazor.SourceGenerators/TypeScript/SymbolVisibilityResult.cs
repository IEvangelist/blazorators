// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class SymbolVisibilityResult
{
    public SymbolAccessibility Accessibility { get; set; }
    public IAnyImportSyntax[]? AliasesToMakeVisible { get; set; }
    public string? ErrorSymbolName { get; set; }
    public Node? ErrorNode { get; set; }
}