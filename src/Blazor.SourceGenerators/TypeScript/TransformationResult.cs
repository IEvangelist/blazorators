// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TransformationResult<T>
{
    public T[]? Transformed { get; set; }
    public TypeScriptDiagnostic[]? Diagnostics { get; set; }
}