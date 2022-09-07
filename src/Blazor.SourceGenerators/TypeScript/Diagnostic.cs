// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class Diagnostic
{
    internal SourceFile File { get; set; }
    internal int Start { get; set; }
    internal int Length { get; set; }
    internal object MessageText { get; set; }
    internal DiagnosticCategory Category { get; set; }
    internal int Code { get; set; }
}