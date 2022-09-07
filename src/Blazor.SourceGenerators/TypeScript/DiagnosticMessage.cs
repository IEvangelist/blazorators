// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class DiagnosticMessage
{
    internal string Key { get; set; }
    internal DiagnosticCategory Category { get; set; }
    internal int Code { get; set; }
    internal string Message { get; set; }
}