// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class DiagnosticMessageChain
{
    internal string MessageText { get; set; }
    internal DiagnosticCategory Category { get; set; }
    internal int Code { get; set; }
    internal DiagnosticMessageChain Next { get; set; }
}