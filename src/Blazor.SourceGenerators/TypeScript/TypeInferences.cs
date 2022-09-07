// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TypeInferences
{
    internal TsType[] Primary { get; set; }
    internal TsType[] Secondary { get; set; }
    internal bool TopLevel { get; set; }
    internal bool IsFixed { get; set; }
}