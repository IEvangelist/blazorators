// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using TypeScript.TypeConverter.JavaScript;

namespace TypeScript.TypeConverter.CSharp
{
    public record CSharpMethod(
        string RawName,
        string RawReturnTypeName,
        List<CSharpType> ParameterDefinitions,
        JavaScriptMethod? JavaScriptMethodDependency = null)
    {
        public bool IsPureJavaScriptInvocation =>
            JavaScriptMethodDependency is { IsPure: true };
    }
}