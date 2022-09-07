// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class LiteralType : TsType
{
    internal string Text { get; set; }
    internal LiteralType FreshType { get; set; }
    internal LiteralType RegularType { get; set; }
}