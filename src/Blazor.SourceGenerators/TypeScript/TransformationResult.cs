// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TransformationResult<T>
{
    internal T[] Transformed { get; set; }
    internal Diagnostic[] Diagnostics { get; set; }
}