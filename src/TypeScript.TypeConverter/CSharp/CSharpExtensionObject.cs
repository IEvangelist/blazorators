// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace TypeScript.TypeConverter.CSharp;

internal record CSharpExtensionObject(
    string RawName,
    string RawTypeName,
    List<CSharpProperty>? Properties = null,
    List<CSharpMethod>? Methods = null) : CSharpType(RawName, RawTypeName);
