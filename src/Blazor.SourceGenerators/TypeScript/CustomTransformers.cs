// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class CustomTransformers
{
    public TransformerFactory<SourceFile>[]? Before { get; set; }
    public TransformerFactory<SourceFile>[]? After { get; set; }
}