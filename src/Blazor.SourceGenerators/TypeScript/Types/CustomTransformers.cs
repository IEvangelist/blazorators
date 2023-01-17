// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class CustomTransformers
{
    public TransformerFactory<SourceFile>[] Before { get; set; }
    public TransformerFactory<SourceFile>[] After { get; set; }
}