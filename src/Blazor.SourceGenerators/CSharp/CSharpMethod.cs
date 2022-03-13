// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.CSharp;

internal record CSharpMethod(
    string RawName,
    string RawReturnTypeName,
    List<CSharpType> ParameterDefinitions,
    JavaScriptMethod? JavaScriptMethodDependency = null)
{
    public bool IsPureJavaScriptInvocation =>
        JavaScriptMethodDependency is { IsPure: true };

    public bool IsReturnTypeNullable =>
        RawReturnTypeName.Contains("null");

    public bool IsVoid => RawReturnTypeName == "void";
}
