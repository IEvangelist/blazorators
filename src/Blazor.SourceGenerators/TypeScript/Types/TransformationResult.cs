// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript.Compiler;

// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TransformationResult<T>
{
    public T[] Transformed { get; set; }
    public TypeScriptDiagnostic[] Diagnostics { get; set; }
}