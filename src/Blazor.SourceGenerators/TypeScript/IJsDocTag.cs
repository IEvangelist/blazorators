// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal interface IJsDocTag : INode
{
    internal AtToken AtToken { get; set; }
    internal Identifier TagName { get; set; }
    internal string Comment { get; set; }
}