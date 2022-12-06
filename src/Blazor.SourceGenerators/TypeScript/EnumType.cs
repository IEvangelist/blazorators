// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class EnumType : TsType
{
    public EnumLiteralType[] MemberTypes { get; set; } = Array.Empty<EnumLiteralType>();
}