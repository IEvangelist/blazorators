// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class LiteralType : TsType
{
    public string? Text { get; set; }
    public LiteralType? FreshType { get; set; }
    public LiteralType? RegularType { get; set; }
}