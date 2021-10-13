// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using TypeScript.TypeConverter.CSharp;

namespace TypeScript.TypeConverter.JavaScript;

internal record JavaScriptMethod(
    string InvokableMethodName,
    string JavaScriptApiMethodName,
    List<CSharpType> Parameters);
