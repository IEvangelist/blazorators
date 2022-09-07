// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class CustomTransformers
{
    internal TransformerFactory<SourceFile>[] Before { get; set; }
    internal TransformerFactory<SourceFile>[] After { get; set; }
}