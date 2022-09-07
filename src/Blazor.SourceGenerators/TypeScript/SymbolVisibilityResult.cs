// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class SymbolVisibilityResult
{
    internal SymbolAccessibility Accessibility { get; set; }
    internal IAnyImportSyntax[] AliasesToMakeVisible { get; set; }
    internal string ErrorSymbolName { get; set; }
    internal Node ErrorNode { get; set; }
}