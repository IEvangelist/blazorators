// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public interface IJsDocTag : INode
{
    public AtToken? AtToken { get; set; }
    public Identifier? TagName { get; set; }
    public string? Comment { get; set; }
}